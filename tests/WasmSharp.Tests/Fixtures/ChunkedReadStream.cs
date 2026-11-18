namespace WasmSharp.Tests.Fixtures;

internal sealed class ChunkedReadStream : Stream
{
    private readonly Stream _inner;
    private readonly int _chunkSize;

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public ChunkedReadStream(Stream inner, int chunkSize = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);
        _inner = inner;
        _chunkSize = chunkSize;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        return _inner.Read(buffer[..Math.Min(buffer.Length, _chunkSize)]);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override void Flush()
    {
        throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
