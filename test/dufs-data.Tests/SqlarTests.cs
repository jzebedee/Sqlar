using Microsoft.Data.Sqlite;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

namespace dufs_data.Tests
{
    public class SqlarTests
    {
        private SqliteConnection GetConnection([CallerMemberName] string dbName = "", bool deleteExisting = true)
        {
            var db = $"{dbName}.db";
            if (deleteExisting)
            {
                File.Delete(db);
            }
            return new($"Data Source={db}");
        }

        [Fact]
        public void SqlarConnectionTest()
        {
            using var conn = GetConnection();
            using var sqlar = new Sqlar(conn);
        }

        [Fact]
        public void SqlarAddFile()
        {
            using var conn = GetConnection();
            using var sqlar = new Sqlar(conn);

            byte[] text = Encoding.UTF8.GetBytes("Hello, world!");
            sqlar.Add("boogie.txt", new("boogie.txt", 0, 0, text.Length, text));
        }
    }
}