namespace WasmSharp.Tests.Fixtures;

public sealed class ChunkedReadStream : Stream
{
    private readonly Stream inner_;
    private readonly int chunkSize_;

    public override bool CanRead => inner_.CanRead;
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
        inner_ = inner;
        chunkSize_ = chunkSize;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        return inner_.Read(buffer[..Math.Min(buffer.Length, chunkSize_)]);
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
            inner_.Dispose();
        }

        base.Dispose(disposing);
    }
}
