using Microsoft.Data.Sqlite;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

namespace dufs_data.Tests
{
    public class SqlarTests
    {
        private SqliteConnection GetConnection([CallerMemberName] string dbName = "")
            => new($"Data Source={dbName}.db");

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