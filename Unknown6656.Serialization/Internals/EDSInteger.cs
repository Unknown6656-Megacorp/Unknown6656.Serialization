namespace Unknown6656.EDS.Internals;


internal enum EDSIntegerFlags
    : byte
{
    Null = 0b_0000_0000,

    MASK_Integer = 0b_1110_0000,
    VALUE_NotNull = 0b_0100_0000,
    VALUE_Negative = 0b_0110_0000,

    MASK_FirstFollowingBytes = 0b_0001_0000,

    MASK_FollowingBytes = 0b_1000_0000,
}

internal sealed unsafe class EDSInteger
    : EDSObject
    , EDSObject<EDSInteger>
{
    public static EDSInteger Zero { get; } = new(false, Array.Empty<byte>());
    public static EDSInteger One { get; } = FromUInt128(1);

    public static new EDSType Type { get; } = EDSType.Integer;


    public bool IsNegative { get; }
    public byte[] BigEndianBytes { get; }
    public int Size => BigEndianBytes.Length;


    private EDSInteger(bool negative, byte[] bytes)
    {
        IsNegative = negative;
        BigEndianBytes = bytes;
    }

    public override string ToJSON() => ToInt128().ToString();

    public sbyte ToInt8() => (sbyte)(ToUInt128() & 0xff);

    public byte ToUInt8() => (byte)(ToInt128() & 0xff);

    public short ToInt16() => (short)(ToInt128() & 0xffff);

    public ushort ToUInt16() => (ushort)(ToUInt128() & 0xffff);

    public int ToInt32() => (int)(ToInt128() & 0xffffffff);

    public uint ToUInt32() => (uint)(ToUInt128() & 0xffffffff);

    public nint ToNInt() => (nint)ToInt64();

    public nuint ToNUInt() => (nuint)ToUInt64();

    public long ToInt64() => (long)(ToInt128() & 0xffffffffffffffff);

    public ulong ToUInt64() => (ulong)(ToUInt128() & 0xffffffffffffffff);

    public Int128 ToInt128()
    {
        Int128 value = 0;

        for (int i = 0; i < BigEndianBytes.Length; ++i)
            value |= (Int128)BigEndianBytes[i] << (8 * i);

        return IsNegative ? -value : value;
    }

    public UInt128 ToUInt128()
    {
        UInt128 value = 0;

        for (int i = 0; i < BigEndianBytes.Length; ++i)
            value |= (UInt128)BigEndianBytes[i] << (8 * i);

        return value;
    }

    public override void Write(Stream stream, SerializerOptions options)
    {
        int significant_bit_count = 0;

        for (int byte_index = 0; byte_index < BigEndianBytes.Length; ++byte_index)
            for (int bit_index = 0; bit_index < 8; ++bit_index)
                if ((BigEndianBytes[byte_index] & (1 << bit_index)) != 0)
                    significant_bit_count = byte_index * 8 + bit_index + 1;

        byte current_out_byte = (byte)(IsNegative ? EDSIntegerFlags.VALUE_Negative : EDSIntegerFlags.VALUE_NotNull);
        int current_out_bit_shift = 4;

        for (int bit_index = 0; bit_index < significant_bit_count; ++bit_index)
        {
            if (current_out_bit_shift == 0)
            {
                stream.WriteByte(current_out_byte);
                current_out_byte = 0;
                current_out_bit_shift = 7;
            }

            if ((significant_bit_count - bit_index) > current_out_bit_shift)
                current_out_byte |= (byte)(bit_index < 4 ? EDSIntegerFlags.MASK_FirstFollowingBytes : EDSIntegerFlags.MASK_FollowingBytes);

            --current_out_bit_shift;

            bool bit = (BigEndianBytes[bit_index / 8] & (1 << (bit_index % 8))) != 0;

            if (bit)
                current_out_byte |= (byte)(1 << current_out_bit_shift);
        }

        stream.WriteByte(current_out_byte);
    }

    public static new EDSInteger? Read(Stream stream, byte first_byte, SerializerOptions options)
    {
        if ((EDSType)(first_byte >> 5) is not (EDSType.Integer or EDSType.Integer_N))
            return Read<EDSInteger>(stream, first_byte, options);

        List<byte> big_endian_bytes = new();
        bool negative = ((EDSIntegerFlags)first_byte & EDSIntegerFlags.MASK_Integer) == EDSIntegerFlags.VALUE_Negative;
        int bit_shift_offset = 4;
        int out_bit_index = 0;
        byte current_out_byte = 0;

        do
        {
            for (int bit_index = 0; bit_index < bit_shift_offset; ++bit_index)
            {
                bool bit = (first_byte & (1 << (bit_shift_offset - 1 - bit_index))) != 0;

                if (bit)
                    current_out_byte |= (byte)(1 << out_bit_index);

                ++out_bit_index;

                if (out_bit_index == 8)
                {
                    big_endian_bytes.Add(current_out_byte);
                    current_out_byte = 0;
                    out_bit_index = 0;
                }
            }

            bool following = (first_byte & (byte)(bit_shift_offset == 4 ? EDSIntegerFlags.MASK_FirstFollowingBytes : EDSIntegerFlags.MASK_FollowingBytes)) != 0;

            if (following && stream.ReadByte() is int in_byte and >= 0)
            {
                first_byte = (byte)in_byte;
                bit_shift_offset = 7;
            }
            else
                break;
        }
        while (true);

        if (current_out_byte != 0)
            big_endian_bytes.Add(current_out_byte);

        return new(negative, big_endian_bytes.ToArray());
    }

    public static EDSInteger? Cast(EDSObject @object)
    {
        switch (@object)
        {
            case null or EDSNull:
                return null;
            case EDSInteger integer:
                return integer;
            case EDSFloat @float:
                if (@float.ToFixed128() is decimal f128)
                    return FromInt128((Int128)f128);
                else if (@float.ToFloat64() is double f64)
                    return FromInt128((Int128)f64);
                else if (@float.ToFloat32() is float f32)
                    return FromInt128((Int128)f32);
                else if (@float.ToFloat16() is Half f16)
                    return FromInt128((Int128)f16);
                else
                    return null;
            case EDSString @string:
                return @string.ToString()?.Trim()?.ToLowerInvariant() switch
                {
                    null or "null" or "undefined" => null,
                    "zero" => Zero,
                    "one" => FromUInt128(1),
                    "e" or "two" => FromUInt128(2),
                    "pi" => FromUInt128(3),
                    "-pi" => FromInt128(-3),
                    "-e" => FromInt128(-2),
                    "-tau" => FromInt128(-6),
                    "tau" => FromUInt128(6),
                    string str => Int128.TryParse(str, out Int128 i128) ? FromInt128(i128) : null,
                };
            case EDSBoolean boolean:
                return boolean.Value ? One : Zero;
            case EDSArray { Length: > 0 } array:
                return Cast(array[0]);
            default:
                return null;
        }
    }

    public static EDSInteger FromInt8(sbyte value) => FromSUInt128(value < 0, (UInt128)Math.Abs(value));

    public static EDSInteger FromUInt8(byte value) => FromSUInt128(false, value);

    public static EDSInteger FromInt16(short value) => FromSUInt128(value < 0, (UInt128)Math.Abs(value));

    public static EDSInteger FromUInt16(ushort value) => FromSUInt128(false, value);

    public static EDSInteger FromInt32(int value) => FromSUInt128(value < 0, (UInt128)Math.Abs(value));

    public static EDSInteger FromUInt32(uint value) => FromSUInt128(false, value);

    public static EDSInteger FromNInt(nint value) => FromInt64(value);

    public static EDSInteger FromNUInt(nuint value) => FromUInt64(value);

    public static EDSInteger FromInt64(long value) => FromSUInt128(value < 0, (UInt128)Math.Abs(value));

    public static EDSInteger FromUInt64(ulong value) => FromSUInt128(false, value);

    public static EDSInteger FromInt128(Int128 value) => FromSUInt128(value < 0, (UInt128)Int128.Abs(value));

    public static EDSInteger FromUInt128(UInt128 value) => FromSUInt128(false, value);

    public static EDSInteger FromSUInt128(bool negative, UInt128 value)
    {
        byte[] big_endian_bytes = new byte[sizeof(UInt128)];

        for (int i = 0; i < big_endian_bytes.Length; ++i)
            big_endian_bytes[i] = (byte)((value >> (8 * i)) & 0xff);

        return new(negative, big_endian_bytes);
    }
}

// TODO : support for BigInteger
