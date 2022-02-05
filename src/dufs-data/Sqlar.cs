using LibDeflate;
using Microsoft.Data.Sqlite;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace dufs_data
{
    public record SqlarFile(string name, int mode, int mtime, int sz, byte[] data)
    {
        public bool IsCompressed => sz > data.Length;

        public SqlarFile Decompress()
        {
            if(!IsCompressed)
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
            if(IsCompressed)
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

    public class Sqlar : IDisposable, IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private bool disposedValue;

        public Sqlar(SqliteConnection connection)
        {
            _connection = connection;
            _connection.Open();

            EnsureSqlar();
        }

        public void Add(SqlarFile value)
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

            return new(name: reader.GetString(0),
                       mode: reader.GetInt32(1),
                       mtime: reader.GetInt32(2),
                       sz: reader.GetInt32(3),
                       data: reader.GetFieldValue<byte[]>(4));//((SqliteBlob)reader.GetStream(4)).;

            [DoesNotReturn]
            static void ThrowHelperNoResult() => throw new InvalidOperationException("TODO: writeme");
        }

        public SqlarFile this[string name]
        {
            get => Get(name);
        }

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

        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (disposedValue)
            {
                return;
            }

            await _connection.DisposeAsync();

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore();
            Dispose(disposing: false);
            GC.SuppressFinalize(this);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}