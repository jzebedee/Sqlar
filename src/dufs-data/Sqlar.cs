using LibDeflate;
using Microsoft.Data.Sqlite;
using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace dufs_data
{
    public record SqlarFile(string name, int mode, int mtime, int sz, byte[] data)
    {
        public bool IsCompressed => sz > data.Length;

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
        private readonly SqliteConnection _connection;
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

        public Sqlar(SqliteConnection connection)
        {
            _connection = connection;
            _connection.Open();

            var sqliteConnectionString = new SqliteConnectionStringBuilder(_connection.ConnectionString);
            IsReadOnly = sqliteConnectionString.Mode == SqliteOpenMode.ReadOnly;

            EnsureSqlar();
        }

        public void Add(SqlarFile value)
            => SetCore(value, upsert: false);

        public void Set(SqlarFile value)
            => SetCore(value, upsert: true);

        private SqlarFile ReadCore(SqliteDataReader reader)
            => new(name: reader.GetString(0),
                   mode: reader.GetInt32(1),
                   mtime: reader.GetInt32(2),
                   sz: reader.GetInt32(3),
                   data: reader.GetFieldValue<byte[]>(4));

        private void SetCore(SqlarFile value, bool upsert)
        {
            using var cmd = _connection.CreateCommand();

#pragma warning disable CS8624 // Argument cannot be used as an output for parameter due to differences in the nullability of reference types.
            (
                cmd.Parameters.Add("@name", SqliteType.Text).Value,
                cmd.Parameters.Add("@mode", SqliteType.Integer).Value,
                cmd.Parameters.Add("@mtime", SqliteType.Integer).Value,
                cmd.Parameters.Add("@sz", SqliteType.Integer).Value,
                cmd.Parameters.Add("@data", SqliteType.Blob).Value
            ) = value.Compress();
#pragma warning restore CS8624 // Argument cannot be used as an output for parameter due to differences in the nullability of reference types.

            cmd.CommandText = "INSERT INTO sqlar(name,mode,mtime,sz,data) VALUES(@name,@mode,@mtime,@sz,@data)";
            if (upsert)
            {
                cmd.CommandText += " ON CONFLICT(name) DO UPDATE SET mode=@mode,mtime=@mtime,sz=@sz,data=@data";
            }
            cmd.ExecuteNonQuery();
        }

        public SqlarFile Get(string name)
        {
            using var cmd = _connection.CreateCommand();

            cmd.Parameters.Add("@name", SqliteType.Text).Value = name;

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

            cmd.Parameters.Add("@name", SqliteType.Text).Value = name;

            cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM sqlar WHERE name==@name)";

            //returns long
            return Convert.ToBoolean(cmd.ExecuteScalar());
        }

        public bool Remove(string name)
        {
            using var cmd = _connection.CreateCommand();

            cmd.Parameters.Add("@name", SqliteType.Text).Value = name;

            cmd.CommandText = "DELETE FROM sqlar WHERE name==@name";

            //returns long
            return cmd.ExecuteNonQuery() > 0;
        }

        public IEnumerator<SqlarFile> GetEnumerator()
        {
            using var cmd = _connection.CreateCommand();

            cmd.CommandText = "SELECT name,mode,mtime,sz,data FROM sqlar";

            using var reader = cmd.ExecuteReader();
            while(reader.Read())
            {
                yield return ReadCore(reader);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }
}