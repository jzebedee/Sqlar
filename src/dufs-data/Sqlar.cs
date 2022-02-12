using LibDeflate;
using System.Buffers;
using System.Collections;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;

namespace dufs_data;

/// <summary>
/// The content and metadata of a single file in SQLAR representation.
/// <para>
/// The filename (the full pathname relative to the root of the archive) is in the "name" field. 
/// The "mode" field is an integer which is the unix-style access permissions for the file. 
/// "mtime" is the modification time of the file in seconds since 1970. 
/// "sz" is the original uncompressed size of the file. 
/// The "data" field contains the file content. 
/// The content is usually compressed using Deflate, though not always. 
/// If the "sz" field is equal to the size of the "data" field, then the content is stored uncompressed. 
/// </para>
/// </summary>
/// <param name="name">Name of the file</param>
/// <param name="mode">Access permissions</param>
/// <param name="mtime">Last modification time</param>
/// <param name="sz">Original file size</param>
/// <param name="data">(Possibly) compressed content</param>
public record SqlarFile(string name, int mode, long mtime, long sz, byte[] data)
{
    public bool IsCompressed => sz != data.Length;

    public SqlarFile Decompress()
    {
        if (!IsCompressed)
        {
            ThrowHelperNotCompressed();
        }

        using var decompressor = new ZlibDecompressor();

        var inflatedData = new byte[sz];
        return decompressor.Decompress(data, inflatedData, out int bytesWritten) switch
        {
            OperationStatus.Done => this with { data = inflatedData },
            _ => ThrowHelperDecompressFailed()
        };

        [DoesNotReturn]
        static SqlarFile ThrowHelperDecompressFailed() => throw new InvalidOperationException("TODO: writeme");

        [DoesNotReturn]
        static void ThrowHelperNotCompressed() => throw new InvalidOperationException("TODO: writeme");
    }

    public SqlarFile Compress(int compressionLevel = 6)
    {
        if (IsCompressed)
        {
            ThrowHelperAlreadyCompressed();
        }

        using var compressor = new ZlibCompressor(compressionLevel);
        using var result = compressor.Compress(data);
        return result switch
        {
            IMemoryOwner<byte> deflatedOwner => this with { data = deflatedOwner.Memory.ToArray() },
            null => this
        };

        [DoesNotReturn]
        static void ThrowHelperAlreadyCompressed() => throw new InvalidOperationException("TODO: writeme");
    }
}

public class Sqlar : IEnumerable<SqlarFile>, IDisposable
{
    private readonly SQLiteConnection _connection;
    private bool disposedValue;

    public int Count => checked((int)LongCount);
    public long LongCount
    {
        get
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(1) FROM sqlar";

            return cmd.ExecuteScalar() switch
            {
                long count => count,
                _ => ThrowHelperNoResult()
            };

            [DoesNotReturn]
            static int ThrowHelperNoResult() => throw new InvalidOperationException("TODO: writeme");
        }
    }

    public bool IsReadOnly { get; }

    public Sqlar(SQLiteConnection connection)
    {
        _connection = connection;
        _connection.Open();

        var sqliteConnectionString = new SQLiteConnectionStringBuilder(_connection.ConnectionString);
        IsReadOnly = sqliteConnectionString.ReadOnly;

        EnsureSqlar();
    }

    public void Add(SqlarFile value)
        => SetCore(value, upsert: false);

    public void AddRange(IEnumerable<SqlarFile> files)
        => SetRangeCore(files, upsert: false);

    public void Set(SqlarFile value)
        => SetCore(value, upsert: true);

    public void SetRange(IEnumerable<SqlarFile> files)
        => SetRangeCore(files, upsert: true);

    private SqlarFile ReadCore(SQLiteDataReader reader)
        => new(name: reader.GetString(0),
               mode: reader.GetInt32(1),
               mtime: reader.GetInt64(2),
               sz: reader.GetInt64(3),
               data: reader.GetFieldValue<byte[]>(4));

    private SqlarFile ReadCoreBlob(SQLiteDataReader reader, int bufferSize = 0x1000)
    {
        var (name, mode, mtime, sz, rowid) = (reader.GetString(0),
               reader.GetInt32(1),
               reader.GetInt64(2),
               reader.GetInt64(3),
               reader.GetInt64(4));

        var blob = SQLiteBlob.Create(_connection, _connection.Database, "sqlar", "data", rowid, true);
        var blobStream = new BlobStream(blob, true);

        var ms = new MemoryStream((int)sz);
        var zlibStream = new ZLibStream(blobStream, CompressionMode.Decompress);
        zlibStream.CopyTo(ms);

        return new(name, mode, mtime, sz, ms.ToArray());
    }

    private SQLiteCommand CreateSetCommand(bool upsert/*, out (SQLiteParameter name, SQLiteParameter mode, SQLiteParameter mtime, SQLiteParameter sz, SQLiteParameter data) parameters*/)
    {
        const string InsertCommand = "INSERT INTO sqlar(name,mode,mtime,sz,data) VALUES(@name,@mode,@mtime,@sz,@data)";
        const string UpsertCommand = InsertCommand + " ON CONFLICT(name) DO UPDATE SET mode=@mode,mtime=@mtime,sz=@sz,data=@data";

        var cmd = _connection.CreateCommand();

        //(SQLiteParameter name, SQLiteParameter mode, SQLiteParameter mtime, SQLiteParameter sz, SQLiteParameter data) = cmd;
        //parameters = (name, mode, mtime, sz, data);

        cmd.CommandText = upsert ? UpsertCommand : InsertCommand;
        return cmd;
    }

    private void SetCore(SqlarFile value, bool upsert)
    {
        using var cmd = CreateSetCommand(upsert);
        (SQLiteParameter name, SQLiteParameter mode, SQLiteParameter mtime, SQLiteParameter sz, SQLiteParameter data) = cmd;
#pragma warning disable CS8624 // Argument cannot be used as an output for parameter due to differences in the nullability of reference types.
        (name.Value, mode.Value, mtime.Value, sz.Value, data.Value) = value.Compress();
#pragma warning restore CS8624 // Argument cannot be used as an output for parameter due to differences in the nullability of reference types.
        cmd.ExecuteNonQuery();
    }

    private void SetRangeCore(IEnumerable<SqlarFile> files, bool upsert)
    {
        using var bulkAddTrans = _connection.BeginTransaction();
        using var cmd = CreateSetCommand(upsert);
        (SQLiteParameter name, SQLiteParameter mode, SQLiteParameter mtime, SQLiteParameter sz, SQLiteParameter data) = cmd;
        foreach (var file in files)
        {
#pragma warning disable CS8624 // Argument cannot be used as an output for parameter due to differences in the nullability of reference types.
            (name.Value, mode.Value, mtime.Value, sz.Value, data.Value) = file.Compress();
#pragma warning restore CS8624 // Argument cannot be used as an output for parameter due to differences in the nullability of reference types.
            cmd.ExecuteNonQuery();
        }
        bulkAddTrans.Commit();
    }

    public SqlarFile Get(string name)
    {
        using var cmd = _connection.CreateCommand();

        cmd.Parameters.Add("@name", DbType.String).Value = name;

        cmd.CommandText = "SELECT name,mode,mtime,sz,data FROM sqlar WHERE name==@name";

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            ThrowHelperNoResult();
        }

        return ReadCore(reader);

        [DoesNotReturn]
        static void ThrowHelperNoResult() => throw new InvalidOperationException("TODO: writeme");
    }

    public SqlarFile GetBlob(string name)
    {
        using var cmd = _connection.CreateCommand();

        cmd.Parameters.Add("@name", DbType.String).Value = name;

        cmd.CommandText = "SELECT name,mode,mtime,sz,rowid FROM sqlar WHERE name = @name";

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            ThrowHelperNoResult();
        }

        return ReadCoreBlob(reader);

        [DoesNotReturn]
        static void ThrowHelperNoResult() => throw new InvalidOperationException("TODO: writeme");
    }

    //we don't provide a setter here because the caller
    //could provide one name in the indexer and another
    //in the SqlarFile record, potentially inserting
    //when they expected to update
    public SqlarFile this[string name] => Get(name);

    private void EnsureSqlar()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS sqlar(name TEXT PRIMARY KEY,mode INT,mtime INT,sz INT,data BLOB)";
        cmd.ExecuteNonQuery();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposedValue)
        {
            return;
        }

        if (disposing)
        {
            _connection.Dispose();
        }

        // TODO: free unmanaged resources (unmanaged objects) and override finalizer
        // TODO: set large fields to null
        disposedValue = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public void Clear()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"DELETE FROM sqlar";
        cmd.ExecuteNonQuery();
    }

    public bool Contains(string name)
    {
        using var cmd = _connection.CreateCommand();

        cmd.Parameters.Add("@name", DbType.String).Value = name;

        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM sqlar WHERE name==@name)";

        //returns long
        return Convert.ToBoolean(cmd.ExecuteScalar());
    }

    public bool Remove(string name)
    {
        using var cmd = _connection.CreateCommand();

        cmd.Parameters.Add("@name", DbType.String).Value = name;

        cmd.CommandText = "DELETE FROM sqlar WHERE name==@name";

        //returns long
        return cmd.ExecuteNonQuery() > 0;
    }

    public IEnumerator<SqlarFile> GetEnumerator()
    {
        using var cmd = _connection.CreateCommand();

        cmd.CommandText = "SELECT name,mode,mtime,sz,data FROM sqlar";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            yield return ReadCore(reader);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}
