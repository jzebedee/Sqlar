namespace Sqlar;

internal static class Utility
{
    private const int NanosecondsPerTick = 100;

    public static DateTimeOffset UnixTimeToDateTimeOffset(long seconds, long nanoseconds)
    {
        return DateTimeOffset.FromUnixTimeSeconds(seconds).AddTicks(nanoseconds / NanosecondsPerTick);
    }

    public static string ConvertPath(ReadOnlySpan<char> path)
    {
        Span<char> buf = stackalloc char[path.Length];

        int pathStart = 0;
        do
        {
            ReadOnlySpan<char> currentDir = Path.GetDirectoryName(path);
            int segLen = path.Length - currentDir.Length;
            Span<char> dest = buf[^(pathStart + segLen)..^pathStart];
            path[^segLen..].CopyTo(dest);
            if (!currentDir.IsEmpty)
            {
                dest[0] = '/';
            }
            path = path[..^segLen];
            pathStart += segLen;
        } while (!path.IsEmpty);

        return new(buf[^pathStart..]);
    }

}
