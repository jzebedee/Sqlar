using Microsoft.Data.Sqlite;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

namespace dufs_data.Tests
{
    public class SqlarTests
    {
        private SqliteConnection GetConnection([CallerMemberName] string dbName = "", bool deleteExisting = true, bool readOnly = false)
        {
            var db = $"{dbName}.db";
            if (deleteExisting)
            {
                File.Delete(db);
            }
            return new($"Data Source={db};{(readOnly ? "Mode=ReadOnly;" : "")}");
        }

        [Fact]
        public void SqlarConnectionTest()
        {
            using var conn = GetConnection();
            using var sqlar = new Sqlar(conn);
        }

        [Fact]
        public void SqlarReadOnlyDbTest()
        {
            {
                using var conn = GetConnection(deleteExisting: false);
                using var sqlar = new Sqlar(conn);
                Assert.False(sqlar.IsReadOnly);
            }
            {
                using var conn = GetConnection(deleteExisting: false, readOnly: true);
                using var sqlar = new Sqlar(conn);
                Assert.True(sqlar.IsReadOnly);
            }
        }

        [Fact]
        public void SqlarAddFile()
        {
            using var conn = GetConnection();
            using var sqlar = new Sqlar(conn);

            byte[] text = Encoding.UTF8.GetBytes("Hello, world!");
            sqlar.Add(new("boogie.txt", 0, 0, text.Length, text));
        }

        [Fact]
        public void SqlarGetCount()
        {
            using var conn = GetConnection();
            using var sqlar = new Sqlar(conn);

            Assert.Equal(0, sqlar.Count);

            byte[] text = Encoding.UTF8.GetBytes("Hello, world!");
            sqlar.Add(new("boogie.txt", 0, 0, text.Length, text));

            Assert.Equal(1, sqlar.Count);
        }

        [Fact]
        public void SqlarClear()
        {
            using var conn = GetConnection();
            using var sqlar = new Sqlar(conn);

            byte[] text = Encoding.UTF8.GetBytes("Hello, world!");
            var file = new SqlarFile("boogie1.txt", 0, 0, text.Length, text);

            sqlar.Add(file);
            sqlar.Add(file with { name = "boogie2.txt" });
            sqlar.Add(file with { name = "boogie3.txt" });

            sqlar.Clear();

            Assert.Equal(0, sqlar.Count);
        }

        [Fact]
        public void SqlarContains()
        {
            using var conn = GetConnection();
            using var sqlar = new Sqlar(conn);

            byte[] text = Encoding.UTF8.GetBytes("Hello, world!");
            var file = new SqlarFile("boogie1.txt", 0, 0, text.Length, text);

            Assert.False(sqlar.Contains(file.name));

            sqlar.Add(file);

            Assert.True(sqlar.Contains(file.name));
        }

        [Fact]
        public void SqlarGetFile()
        {
            using var conn = GetConnection();
            using var sqlar = new Sqlar(conn);

            byte[] text = Encoding.UTF8.GetBytes("Hello, world!");
            SqlarFile expected = new("boogie.txt", 0, 0, text.Length, text);

            sqlar.Add(expected);

            SqlarFile actual = sqlar[expected.name];
            Assert.Equal(expected.name, actual.name);
            Assert.Equal(expected.mode, actual.mode);
            Assert.Equal(expected.mtime, actual.mtime);
            Assert.Equal(expected.sz, actual.sz);
            Assert.Equal(expected.data, actual.data);
        }

        [Fact]
        public void SqlarGetFileCompressed()
        {
            const string lipsum = @"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Vivamus ullamcorper metus tellus, vel faucibus lorem egestas a. In hac habitasse platea dictumst. Sed fermentum dignissim sapien, maximus faucibus orci efficitur et. Ut tristique luctus lacus aliquam varius. Aliquam ullamcorper semper libero a tincidunt. Curabitur in diam tincidunt, ultricies ante id, auctor magna. Quisque rhoncus scelerisque mi, interdum lobortis felis porttitor nec. Maecenas molestie non mauris in efficitur. Maecenas eu rhoncus arcu. Ut dapibus placerat risus, in efficitur massa consequat sed. Morbi dapibus laoreet eros, vitae dapibus turpis cursus id. Donec sem elit, consequat facilisis vehicula eu, euismod vel nisl. Maecenas ligula odio, luctus vitae sollicitudin quis, porttitor quis orci. Class aptent taciti sociosqu ad litora torquent per conubia nostra, per inceptos himenaeos. Vestibulum porta posuere neque eget interdum.";

            using var conn = GetConnection();
            using var sqlar = new Sqlar(conn);

            byte[] text = Encoding.UTF8.GetBytes(lipsum);
            SqlarFile expected = new("lipsum.txt", 0, 0, text.Length, text);

            sqlar.Add(expected);

            SqlarFile actual = sqlar[expected.name];
            Assert.True(actual.IsCompressed);

            var uncompressed = actual.Decompress();
            Assert.False(uncompressed.IsCompressed);

            Assert.Equal(text, uncompressed.data);
        }

        [Fact]
        public void SqlarSetFile()
        {
            using var conn = GetConnection();
            using var sqlar = new Sqlar(conn);

            byte[] text = Encoding.UTF8.GetBytes("Hello, world!");
            SqlarFile expected = new("boogie.txt", 0, 0, text.Length, text);

            sqlar.Add(expected);

            SqlarFile actual = sqlar[expected.name];
            Assert.Equal(expected.name, actual.name);
            Assert.Equal(expected.mode, actual.mode);
            Assert.Equal(expected.mtime, actual.mtime);
            Assert.Equal(expected.sz, actual.sz);
            Assert.Equal(expected.data, actual.data);

            Assert.Throws<SqliteException>(() => sqlar.Add(expected));

            byte[] text2 = Encoding.UTF8.GetBytes("Goodbye, world.");
            SqlarFile expected2 = expected with { mode = 1, mtime = 2, sz = text2.Length, data = text2 };
            sqlar.Set(expected2);
            var actual2 = sqlar[expected2.name];
            Assert.Equal(expected2.name, actual2.name);
            Assert.Equal(expected2.mode, actual2.mode);
            Assert.Equal(expected2.mtime, actual2.mtime);
            Assert.Equal(expected2.sz, actual2.sz);
            Assert.Equal(expected2.data, actual2.data);
        }


    }
}