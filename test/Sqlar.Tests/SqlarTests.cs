using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;
#if MICROSOFT_SQLITE
using Microsoft.Data.Sqlite;
using LiteConnection = Microsoft.Data.Sqlite.SqliteConnection;
using LiteException = Microsoft.Data.Sqlite.SqliteException;
#elif OFFICIAL_SQLITE
using LiteConnection = System.Data.SQLite.SQLiteConnection;
using LiteException = System.Data.SQLite.SQLiteException;
#endif

namespace Sqlar.Tests
{
    public class SqlarTests
    {
        private static LiteConnection GetConnection([CallerMemberName] string dbName = "", bool deleteExisting = true, bool readOnly = false)
        {
            var db = $"{dbName}.db";
            if (deleteExisting)
            {
                File.Delete(db);
            }
            string readOnlyOption = readOnly ?
#if MICROSOFT_SQLITE
                "Mode=ReadOnly;"
#elif OFFICIAL_SQLITE
                "Read Only=true;"
#endif
                : "";
            return new($"Data Source={db};{readOnlyOption}");
        }

        private static byte[] SampleText => Encoding.UTF8.GetBytes("Hello, world!");
        private static SqlarFile SampleFile => new("boogie.txt", 0, DateTimeOffset.Now.ToUnixTimeSeconds(), SampleText.Length, SampleText);

        [Fact]
        public void SqlarConnectionTest()
        {
            using var conn = GetConnection();
            using var sqlar = new Sqlar(conn);
        }

        [Fact]
        public void SqlarReadOnlyDbTest()
        {
            using var conn = GetConnection(deleteExisting: false, readOnly: true);
            using var sqlar = new Sqlar(conn);
            var file = SampleFile;
            Assert.Throws<LiteException>(() => sqlar.Add(file));
        }

        [Fact]
        public void SqlarAddFile()
        {
            using var conn = GetConnection();
            using var sqlar = new Sqlar(conn);

            var file = SampleFile;
            sqlar.Add(file);
        }

        [Fact]
        public void SqlarGetCount()
        {
            using var conn = GetConnection();
            using var sqlar = new Sqlar(conn);

            Assert.Equal(0, sqlar.Count);

            var file = SampleFile;
            sqlar.Add(file);

            Assert.Equal(1, sqlar.Count);
        }

        [Fact]
        public void SqlarClear()
        {
            using var conn = GetConnection();
            using var sqlar = new Sqlar(conn);

            var file = SampleFile;

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

            var file = SampleFile;

            Assert.False(sqlar.Contains(file.name));

            sqlar.Add(file);

            Assert.True(sqlar.Contains(file.name));
        }

        [Fact]
        public void SqlarRemove()
        {
            using var conn = GetConnection();
            using var sqlar = new Sqlar(conn);

            var innocent = SampleFile with { name = "innocent.txt" };
            var victim = innocent with { name = "victim.txt" };

            sqlar.Add(innocent);
            sqlar.Add(victim);

            sqlar.Remove(victim.name);

            Assert.Equal(1, sqlar.Count);
        }

        [Fact]
        public void SqlarEnumerate()
        {
            static IEnumerable<SqlarFile> GetTestFiles()
            {
                var file1 = SampleFile with { name = "boogie1.txt" };
                yield return file1;
                var file2 = file1 with { name = "boogie2.txt" };
                yield return file2;
                var file3 = file1 with { name = "boogie3.txt" };
                yield return file3;
            }

            using var conn = GetConnection();
            using var sqlar = new Sqlar(conn);

            var testFiles = GetTestFiles().ToArray();
            foreach (var file in testFiles)
            {
                sqlar.Add(file);
            }

            Assert.Equal(testFiles.Select(rec => rec.name), sqlar.Select(rec => rec.name));
        }

        [Fact]
        public void SqlarGetFile()
        {
            using var conn = GetConnection();
            using var sqlar = new Sqlar(conn);

            SqlarFile expected = SampleFile;
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
            using var conn = GetConnection();
            using var sqlar = new Sqlar(conn);

            const string lipsum = @"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Vivamus ullamcorper metus tellus, vel faucibus lorem egestas a. In hac habitasse platea dictumst. Sed fermentum dignissim sapien, maximus faucibus orci efficitur et. Ut tristique luctus lacus aliquam varius. Aliquam ullamcorper semper libero a tincidunt. Curabitur in diam tincidunt, ultricies ante id, auctor magna. Quisque rhoncus scelerisque mi, interdum lobortis felis porttitor nec. Maecenas molestie non mauris in efficitur. Maecenas eu rhoncus arcu. Ut dapibus placerat risus, in efficitur massa consequat sed. Morbi dapibus laoreet eros, vitae dapibus turpis cursus id. Donec sem elit, consequat facilisis vehicula eu, euismod vel nisl. Maecenas ligula odio, luctus vitae sollicitudin quis, porttitor quis orci. Class aptent taciti sociosqu ad litora torquent per conubia nostra, per inceptos himenaeos. Vestibulum porta posuere neque eget interdum.";
            byte[] text = Encoding.UTF8.GetBytes(lipsum);

            SqlarFile expected = SampleFile with { name = "lipsum.txt", sz = text.Length, data = text };

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

            SqlarFile expected = SampleFile;

            sqlar.Add(expected);

            SqlarFile actual = sqlar[expected.name];
            Assert.Equal(expected.name, actual.name);
            Assert.Equal(expected.mode, actual.mode);
            Assert.Equal(expected.mtime, actual.mtime);
            Assert.Equal(expected.sz, actual.sz);
            Assert.Equal(expected.data, actual.data);

            Assert.Throws<LiteException>(() => sqlar.Add(expected));

            byte[] text2 = Encoding.UTF8.GetBytes("Goodbye, world.");
            SqlarFile expected2 = expected with { mode = 1, mtime = expected.mtime + 1, sz = text2.Length, data = text2 };
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