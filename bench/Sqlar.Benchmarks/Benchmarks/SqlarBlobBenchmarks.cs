using BenchmarkDotNet.Attributes;
using System.Buffers;
using System.Data.SQLite;

namespace Sqlar.Benchmarks;

public class SqlarBlobBenchmarks
{
    //public SQLiteConnection Connection { get; } = new SQLiteConnection("Data Source=:memory:");

    //public IEnumerable<SQLiteConnection> Args
    //{
    //    get
    //    {
    //        yield return Connection;
    //    }
    //}

    [Benchmark]
    //[ArgumentsSource(nameof(Args))]
    public void BufferInsert()
    {
        using var conn = new SQLiteConnection("Data Source=:memory:");
        using var sqlar = new Sqlar(conn);

        byte[]? buf = null;
        try
        {
            buf = ArrayPool<byte>.Shared.Rent(0x1000);
            Random.Shared.NextBytes(buf);

            var file = new SqlarFile(Guid.NewGuid().ToString(), 0, 0, buf.Length, buf);
            sqlar.Add(file);
        }
        finally
        {
            if (buf is not null)
            {
                ArrayPool<byte>.Shared.Return(buf);
            }
        }
    }
}
