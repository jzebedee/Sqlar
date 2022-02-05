using Microsoft.Data.Sqlite;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace dufs_data
{
    public record SqlarFile(string name, int mode, int mtime, int sz, byte[] data);

    public class Sqlar : IDictionary<string, SqlarFile>, IDisposable, IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private bool disposedValue;

        public Sqlar(SqliteConnection connection)
        {
            _connection = connection;
            _connection.Open();

            EnsureSqlar();
        }

        public SqlarFile this[string key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public ICollection<string> Keys => throw new NotImplementedException();

        public ICollection<SqlarFile> Values => throw new NotImplementedException();

        public int Count => throw new NotImplementedException();

        public bool IsReadOnly => throw new NotImplementedException();

        public void Add(string key, SqlarFile value)
        {
            using var cmd = _connection.CreateCommand();

#pragma warning disable CS8624 // Argument cannot be used as an output for parameter due to differences in the nullability of reference types.
            (
                cmd.Parameters.Add($"@{nameof(SqlarFile.name)}", SqliteType.Text).Value,
                cmd.Parameters.Add($"@{nameof(SqlarFile.mode)}", SqliteType.Integer).Value,
                cmd.Parameters.Add($"@{nameof(SqlarFile.mtime)}", SqliteType.Integer).Value,
                cmd.Parameters.Add($"@{nameof(SqlarFile.sz)}", SqliteType.Integer).Value,
                cmd.Parameters.Add($"@{nameof(SqlarFile.data)}", SqliteType.Blob).Value
            ) = value;
#pragma warning restore CS8624 // Argument cannot be used as an output for parameter due to differences in the nullability of reference types.

            cmd.CommandText = "INSERT INTO sqlar(name,mode,mtime,sz,data) VALUES(@name,@mode,@mtime,@sz,@data)";
            cmd.ExecuteNonQuery();
        }

        public void Add(KeyValuePair<string, SqlarFile> item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(KeyValuePair<string, SqlarFile> item)
        {
            throw new NotImplementedException();
        }

        public bool ContainsKey(string key)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(KeyValuePair<string, SqlarFile>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<string, SqlarFile>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public bool Remove(string key)
        {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<string, SqlarFile> item)
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out SqlarFile value)
        {
            throw new NotImplementedException();
        }

        private void EnsureSqlar()
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS sqlar(
  name TEXT PRIMARY KEY,  -- name of the file
  mode INT,               -- access permissions
  mtime INT,              -- last modification time
  sz INT,                 -- original file size
  data BLOB               -- compressed content
);";
            cmd.ExecuteNonQuery();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
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