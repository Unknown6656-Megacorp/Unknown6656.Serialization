using System.Collections.Generic;
using System.Collections;
using System.IO;
using System;

namespace Unknown6656.Serialization;


public abstract class ByteGeneratorStream
    : Stream
    , IEnumerable<byte>
{
    private long _generated = 0;


    public sealed override bool CanRead { get; } = true;

    public sealed override bool CanSeek { get; } = false;

    public sealed override bool CanWrite { get; } = false;

    public sealed override long Length => _generated;

    public sealed override long Position
    {
        get => _generated;
        set => throw new InvalidOperationException();
    }

    public static ByteGeneratorStream Zero => FromDelegate(() => 0);

    public static ByteGeneratorStream Random => FromRandom(new());


    public byte Generate()
    {
        ++_generated;

        return GetNextByte();
    }

    protected abstract byte GetNextByte();

    public override int Read(byte[] buffer, int offset, int count)
    {
        for (int i = 0; i < count; ++i)
            if (i + offset < buffer.Length)
                buffer[i + offset] = GetNextByte();
            else
                return i;

        return count;
    }

    public sealed override void Flush()
    {
    }

    public sealed override long Seek(long offset, SeekOrigin origin) =>
        (offset, origin) is (0, SeekOrigin.End or SeekOrigin.Current) ? 0 : throw new InvalidOperationException();

    public sealed override void SetLength(long value) => throw new InvalidOperationException();

    public sealed override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException();

    public IEnumerator<byte> GetEnumerator()
    {
        while (true)
            yield return GetNextByte();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static ByteGeneratorStream FromRandom(Random random) => FromDelegate(random is Unknown6656.Mathematics.Numerics.Random rng ? rng.NextByte : () => (byte)(random.Next() & 0xff));

    public static ByteGeneratorStream FromDelegate(Func<byte> generator) => new _delegated(generator);

    public static implicit operator ByteGeneratorStream(Random random) => FromRandom(random);

    public static implicit operator ByteGeneratorStream(Func<byte> func) => FromDelegate(func);


    private sealed class _delegated(Func<byte> next)
        : ByteGeneratorStream
    {
        protected override byte GetNextByte() => next();
    }
}
