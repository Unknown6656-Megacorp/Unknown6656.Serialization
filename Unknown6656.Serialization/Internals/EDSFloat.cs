namespace Unknown6656.EDS.Internals;


internal enum EDSFloatFlags
    : byte
{
    Null = 0b_0000_0000,
    Zero = 0b_1000_0000,
    PositiveInfinity = 0b_1000_0001,
    NegativeInfinity = 0b_1000_0010,
    NaN = 0b_1000_0011,

    MASK_Size = 0b_1110_0111,
    VALUE_Size16Bit = 0b_1000_0100,
    VALUE_Size32Bit = 0b_1000_0101,
    VALUE_Size64Bit = 0b_1000_0110,
    VALUE_Size128Bit = 0b_1000_0111,
}

public sealed unsafe class EDSFloat
    : EDSObject
    , EDSObject<EDSFloat>
{
    public static EDSFloat FloatNull { get; } = new(null, null, null, null);

    public static EDSFloat Zero { get; } = new(0, 0, 0, (Half)0);

    public static EDSFloat One { get; } = new(1, 1, 1, (Half)1);

    public static EDSFloat NaN { get; } = new(null, double.NaN, float.NaN, Half.NaN);

    public static EDSFloat PositiveInfinity { get; } = new(null, double.PositiveInfinity, float.PositiveInfinity, Half.PositiveInfinity);

    public static EDSFloat NegativeInfinity { get; } = new(null, double.NegativeInfinity, float.NegativeInfinity, Half.NegativeInfinity);

    public static new EDSType Type { get; } = EDSType.Float;


    private readonly decimal? _f128;
    private readonly double? _f64;
    private readonly float? _f32;
    private readonly Half? _f16;


    public override bool IsNull => this == FloatNull || base.IsNull || this is { _f128: null, _f16: null, _f32: null, _f64: null };


    private EDSFloat(decimal? f128, double? f64, float? f32, Half? f16)
    {
        _f128 = f128;
        _f64 = f64;
        _f32 = f32;
        _f16 = f16;
    }

    public override string ToJSON() => _f128?.ToString() ?? _f64?.ToString() ?? "0";

    public Half? ToFloat16() => (_f128, _f64, _f32, _f16) switch
    {
        ({ } f, _, _, _) => (Half)f,
        (_, { } f, _, _) => (Half)f,
        (_, _, { } f, _) => (Half)f,
        (_, _, _, { } f) => f,
        _ => null
    };

    public float? ToFloat32() => (_f128, _f64, _f32, _f16) switch
    {
        ({ } f, _, _, _) => (float)f,
        (_, { } f, _, _) => (float)f,
        (_, _, { } f, _) => f,
        (_, _, _, { } f) => (float)f,
        _ => null
    };

    public double? ToFloat64() => (_f128, _f64, _f32, _f16) switch
    {
        ({ } f, _, _, _) => (double)f,
        (_, { } f, _, _) => f,
        (_, _, { } f, _) => f,
        (_, _, _, { } f) => (double)f,
        _ => null
    };

    public decimal? ToFixed128()
    {
        foreach (object? obj in new object?[] { _f128, _f64, _f32, _f16 })
            if (obj is { })
                try
                {
                    return Convert.ToDecimal(obj);
                }
                catch
                {
                }

        return null;
    }

    public override void Write(Stream stream, SerializerOptions options)
    {
        decimal? __f128 = _f128;
        double? __f64 = _f64;
        float? __f32 = _f32;
        Half? __f16 = _f16;

        if (__f128 is { } d)
        {
            __f16 = (Half)d is { } _h && Half.IsFinite(_h) && (decimal)_h == d ? _h : null;
            __f32 = (float)d is { } _f && float.IsFinite(_f) && (decimal)_f == d ? _f : null;
            __f64 = (double)d is { } _d && double.IsFinite(_d) && (decimal)_d == d ? _d : null;
        }
        else if (__f64 is { } db)
        {
            __f16 = (Half)db is { } _h && Half.IsFinite(_h) && (double)_h == db ? _h : null;
            __f32 = (float)db is { } _f && float.IsFinite(_f) && (double)_f == db ? _f : null;
        }
        else if (__f32 is { } f)
            __f16 = (Half)f is { } _h && Half.IsFinite(_h) && (float)_h == f ? _h : null;

        object? value = (__f16, __f32, __f64, __f128) switch
        {
            ({ } f16, _, { } f64, _) when (double)f16 == f64 => f16,
            ({ } f16, { } f32, _, _) when (float)f16 == f32 => f16,
            (_, { } f32, { } f64, _) when (double)f32 == f64 => f32,
            ({ } f16, _, _, { } f128) when Half.IsFinite(f16) && (decimal)f16 == f128 => f16,
            (_, { } f32, _, { } f128) when float.IsFinite(f32) && (decimal)f32 == f128 => f32,
            (_, _, { } f64, { } f128) when double.IsFinite(f64) && (decimal)f64 == f128 => f64,
            ({ } f16, _, _, _) => f16,
            (_, { } f32, _, _) => f32,
            (_, _, { } f64, _) => f64,
            (_, _, _, { } f128) => f128,
            _ => null
        };

        switch (value)
        {
            case 0m or 0d or 0f:
            case Half f16 when f16 == Half.Zero:
                stream.WriteByte((byte)EDSFloatFlags.Zero);

                return;
            case Half f16 when Half.IsPositiveInfinity(f16):
            case float f32 when float.IsPositiveInfinity(f32):
            case double f64 when double.IsPositiveInfinity(f64):
                stream.WriteByte((byte)EDSFloatFlags.PositiveInfinity);

                return;
            case Half f16 when Half.IsNegativeInfinity(f16):
            case float f32 when float.IsNegativeInfinity(f32):
            case double f64 when double.IsNegativeInfinity(f64):
                stream.WriteByte((byte)EDSFloatFlags.NegativeInfinity);

                return;
            case Half f16 when !Half.IsFinite(f16):
            case float f32 when !float.IsFinite(f32):
            case double f64 when !double.IsFinite(f64):
                stream.WriteByte((byte)EDSFloatFlags.NaN);

                return;
            case Half f16:
                {
                    byte[] bytes = BitConverter.GetBytes(f16);

                    stream.WriteByte((byte)EDSFloatFlags.VALUE_Size16Bit);
                    stream.Write(bytes, 0, bytes.Length);
                }
                return;
            case float f32:
                {
                    byte[] bytes = BitConverter.GetBytes(f32);

                    stream.WriteByte((byte)EDSFloatFlags.VALUE_Size32Bit);
                    stream.Write(bytes, 0, bytes.Length);
                }
                return;
            case double f64:
                {
                    byte[] bytes = BitConverter.GetBytes(f64);

                    stream.WriteByte((byte)EDSFloatFlags.VALUE_Size64Bit);
                    stream.Write(bytes, 0, bytes.Length);
                }
                return;
            case decimal f128:
                {
                    // TODO : fix this shite

                    UInt128 raw = *(UInt128*)&f128;

                    stream.WriteByte((byte)EDSFloatFlags.VALUE_Size128Bit);

                    for (int i = 0; i < sizeof(UInt128); ++i)
                        stream.WriteByte((byte)(raw >> (i * 8)));
                }
                return;
            default:
                stream.WriteByte((byte)EDSFloatFlags.Null);

                return;
        }
    }

    public static new EDSFloat? Read(Stream stream, byte first_byte, SerializerOptions options)
    {
        switch ((EDSFloatFlags)first_byte)
        {
            case EDSFloatFlags.Null:
                return null;
            case EDSFloatFlags.Zero:
                return Zero;
            case EDSFloatFlags.NaN:
                return NaN;
            case EDSFloatFlags.PositiveInfinity:
                return PositiveInfinity;
            case EDSFloatFlags.NegativeInfinity:
                return NegativeInfinity;
            case { } flag when (flag & EDSFloatFlags.MASK_Size) == EDSFloatFlags.VALUE_Size16Bit:
                {
                    byte[] bytes = new byte[sizeof(Half)];

                    stream.Read(bytes, 0, bytes.Length);

                    return FromFloat16(BitConverter.ToHalf(bytes, 0));
                }
            case { } flag when (flag & EDSFloatFlags.MASK_Size) == EDSFloatFlags.VALUE_Size32Bit:
                {
                    byte[] bytes = new byte[sizeof(float)];

                    stream.Read(bytes, 0, bytes.Length);

                    return FromFloat32(BitConverter.ToSingle(bytes, 0));
                }
            case { } flag when (flag & EDSFloatFlags.MASK_Size) == EDSFloatFlags.VALUE_Size64Bit:
                {
                    byte[] bytes = new byte[sizeof(double)];

                    stream.Read(bytes, 0, bytes.Length);

                    return FromFloat64(BitConverter.ToDouble(bytes, 0));
                }
            case { } flag when (flag & EDSFloatFlags.MASK_Size) == EDSFloatFlags.VALUE_Size128Bit:
                {
                    UInt128 raw = 0;

                    for (int i = 0; i < sizeof(UInt128); ++i)
                        raw |= (UInt128)stream.ReadByte() << (i * 8);

                    return FromFixed128(*(decimal*)&raw);
                }
            default:
                return null;
        }
    }

    public static EDSFloat? Cast(EDSObject @object)
    {
        switch (@object)
        {
            case null or EDSNull:
                return null;
            case EDSFloat @float:
                return @float;
            case EDSBoolean boolean:
                return boolean.Value ? One : Zero;
            case EDSInteger integer:
                Int128 i128 = integer.ToInt128();

                return new((decimal)i128, (double)i128, (float)i128, (Half)i128);
            case EDSString @string:
                switch (@string.ToString()?.Trim()?.ToLowerInvariant())
                {
                    case null or "null":
                        return null;
                    case "undefined" or "nan":
                        return NaN;
                    case "zero":
                        return Zero;
                    case "one":
                        return FromFloat32(1);
                    case "two":
                        return FromFloat32(2);
                    case "-one":
                        return FromFloat32(-1);
                    case "-two":
                        return FromFloat32(-2);
                    case "-pi":
                        return FromFloat64(-Math.PI);
                    case "-e":
                        return FromFloat64(-Math.E);
                    case "pi":
                        return FromFloat64(Math.PI);
                    case "e":
                        return FromFloat64(-Math.PI);
                    case "-tau":
                        return FromFloat64(-Math.Tau);
                    case "tau":
                        return FromFloat64(Math.Tau);
                    case "inf" or "infinity" or "posinf" or "posinfinity" or "positiveinfinity":
                        return PositiveInfinity;
                    case "-inf" or "-infinity" or "neginf" or "neginfinity" or "negativeinfinity":
                        return NegativeInfinity;
                    case string str:
                        if (decimal.TryParse(str, out decimal f128))
                            return FromFixed128(f128);
                        else if (double.TryParse(str, out double f64))
                            return FromFloat64(f64);
                        else
                            return null;
                }
            case EDSArray { Length: > 0 } array:
                return Cast(array[0]);
            default:
                return null;
        }
    }

    public static EDSFloat FromFixed128(decimal value) => new(value, null, null, null);

    public static EDSFloat FromFloat64(double value) => new(null, value, null, null);

    public static EDSFloat FromFloat32(float value) => new(null, null, value, null);

    public static EDSFloat FromFloat16(Half value) => new(null, null, null, value);
}

// TODO : support for BigNumber