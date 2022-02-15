using System.Data.SQLite;
using System.Diagnostics.CodeAnalysis;

namespace Sqlar;

public sealed class SQLiteBlobStream : Stream
{
    private readonly SQLiteBlob _blob;
    private readonly bool _readOnly;
    private readonly bool _leaveOpen;

    private bool disposedValue;
    private int _count;
    private int _offset;

    public SQLiteBlobStream(SQLiteBlob blob, bool readOnly, bool leaveOpen = false)
    {
        _blob = blob;
        _readOnly = readOnly;
        _leaveOpen = leaveOpen;

        _count = blob.GetCount();
        _offset = 0;
    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => !_readOnly;

    public override long Length => _count;

    public override long Position { get => _offset; set => _offset = (int)value; }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        //Span<byte> span = buffer.AsSpan(offset, count);
        if (offset != 0)
        {
            ThrowHelperBufferOffset();
        }

        var blobCount = Math.Min(count, _count - _offset);
        return blobCount switch
        {
            <= 0 => 0,
            > 0 => BlobRead()
        };

        int BlobRead()
        {
            _blob.Read(buffer, blobCount, _offset);
            _offset += blobCount;
            return blobCount;
        }

        [DoesNotReturn]
        static void ThrowHelperBufferOffset() => throw new NotImplementedException("Cannot write to buffer with offset");
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return origin switch
        {
            SeekOrigin.Begin => Position = offset,
            SeekOrigin.Current => Position += offset,
            SeekOrigin.End => Position = Length + offset,
            _ => ThrowHelperBadOrigin()
        };

        [DoesNotReturn]
        static long ThrowHelperBadOrigin() => throw new ArgumentException(nameof(origin));
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (!CanWrite)
        {
            ThrowHelperNoWrite();
        }

        _blob.Write(buffer, count, _offset);
        _offset += count;

        [DoesNotReturn]
        static void ThrowHelperNoWrite() => throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposedValue)
        {
            return;
        }

        if (disposing)
        {
            if (!_leaveOpen)
            {
                _blob.Dispose();
            }
        }

        base.Dispose(disposing);
        disposedValue = true;
    }
}
