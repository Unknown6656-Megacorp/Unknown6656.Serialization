using System.IO.Compression;
using System.Text;

namespace Unknown6656.EDS.Internals;


internal enum EDSStringFlags
    : byte
{
    Null             = 0b_0000_0000,
    Empty            = 0b_1010_0000,
    Binary           = 0b_1010_0001,
    Codepage_ASCII   = 0b_1010_0010,
    Codepage_UTF8    = 0b_1010_0011,
    Codepage_UTF16LE = 0b_1010_0100,
    Codepage_UTF16BE = 0b_1010_0101,
    Codepage_UTF32   = 0b_1010_0110,
    Codepage_ISO8859 = 0b_1010_0111,
    Codepage_Custom  = 0b_1010_1000,
    SingleUTF16Char  = 0b_1010_1001,
    UUID_128Bit      = 0b_1010_1010,

    MASK_GZIPCompressed = 0b_0001_0000,
}

public sealed class EDSString
    : EDSObject
    , EDSObject<EDSString>
{
    public static Encoding DefaultEncoding { get; } = Encoding.UTF8;

    public static EDSString Empty { get; } = new(Array.Empty<byte>(), null);

    public static new EDSType Type { get; } = EDSType.String;



    private readonly byte[]? _bytes;

    public Encoding? Encoding { get; }

    public override bool IsNull => base.IsNull || _bytes is null;

    public bool IsEmpty => _bytes?.Length is null or 0;


    private EDSString(byte[]? bytes, Encoding? encoding)
    {
        Encoding = encoding;
        _bytes = bytes;
    }

    public char? ToChar()
    {
        if (_bytes is [byte single])
            return (char)single;
        else if (_bytes is [byte lo, byte hi])
            return (char)((hi << 8) | lo);
        else
            return ToString()?[0];
    }

    public override string ToJSON() => ToString() is string str ? $"\"{str.Replace("\\", "\\\\")
                                                                          .Replace("\"", "\\\"")
                                                                          .Replace("\r", "\\r")
                                                                          .Replace("\n", "\\n")
                                                                          .Replace("\t", "\\t")
                                                                          .Replace("\0", "\\0")
                                                                          .Replace("\b", "\\b")
                                                                          .Replace("\v", "\\v")
                                                                          .Replace("\a", "\\a")}\"" : "null";

#pragma warning disable CS8764 // Nullability of return type doesn't match overridden member
    public override string? ToString() => _bytes is { } bytes ? (Encoding ?? DefaultEncoding).GetString(bytes) : null;
#pragma warning restore CS8764

    public byte[]? ToByteArray() => _bytes;

    public unsafe Guid? ToGUID() => _bytes?.Length == sizeof(Guid) ? new Guid(_bytes) : Guid.TryParse(ToString(), out Guid guid) ? guid : null;

    public override unsafe void Write(Stream stream, SerializerOptions options)
    {
        if (IsNull)
            stream.WriteByte((byte)EDSStringFlags.Null);
        else if (IsEmpty)
            stream.WriteByte((byte)EDSStringFlags.Empty);
        else if (_bytes is [byte single])
        {
            stream.WriteByte((byte)EDSStringFlags.SingleUTF16Char);

            EDSInteger.FromUInt8(single).Write(stream, options);
        }
        else if (_bytes is [byte lo, byte hi])
        {
            int codepoint = (hi << 8) | lo;

            stream.WriteByte((byte)EDSStringFlags.SingleUTF16Char);

            EDSInteger.FromInt32(codepoint).Write(stream, options);
        }
        else if (_bytes?.Length == sizeof(Guid))
        {
            stream.WriteByte((byte)EDSStringFlags.UUID_128Bit);
            stream.Write(_bytes, 0, _bytes.Length);
        }
        else
        {
            byte[] bytes = _bytes!;

            EDSStringFlags flag = EDSStringFlags.Codepage_Custom;
            bool use_gzip = bytes.Length > 20;

            if (Encoding == Encoding.Latin1)
                flag = EDSStringFlags.Codepage_ISO8859;
            else if (Encoding == Encoding.ASCII)
                flag = EDSStringFlags.Codepage_ASCII;
            else if (Encoding == Encoding.UTF8)
                flag = EDSStringFlags.Codepage_UTF8;
            else if (Encoding == Encoding.Unicode)
                flag = EDSStringFlags.Codepage_UTF16LE;
            else if (Encoding == Encoding.BigEndianUnicode)
                flag = EDSStringFlags.Codepage_UTF16BE;
            else if (Encoding == Encoding.UTF32)
                flag = EDSStringFlags.Codepage_UTF32;
            else if (Encoding is null)
                flag = EDSStringFlags.Binary;

            if (use_gzip)
                flag |= EDSStringFlags.MASK_GZIPCompressed;

            stream.WriteByte((byte)flag);

            if (flag is EDSStringFlags.Codepage_Custom)
                EDSInteger.FromInt32(Encoding?.CodePage ?? 0).Write(stream, options);

            if (use_gzip)
                bytes = GZIPCompressData(bytes);

            EDSInteger.FromInt32(bytes.Length).Write(stream, options);
            stream.Write(bytes, 0, bytes.Length);
        }
    }

    public static new unsafe EDSString? Read(Stream stream, byte first_byte, SerializerOptions options)
    {
        EDSStringFlags flag = (EDSStringFlags)first_byte;
        bool use_gzip = (flag & EDSStringFlags.MASK_GZIPCompressed) == EDSStringFlags.MASK_GZIPCompressed;

        flag &= ~EDSStringFlags.MASK_GZIPCompressed;

        if (flag is EDSStringFlags.Empty)
            return Empty;
        else if (flag is EDSStringFlags.SingleUTF16Char)
        {
            if (Read<EDSInteger>(stream, options)?.ToUInt16() is ushort charpoint)
                return FromChar((char)charpoint);
        }
        else if (flag is EDSStringFlags.UUID_128Bit)
        {
            byte[] buffer = new byte[sizeof(Guid)];

            stream.Read(buffer, 0, buffer.Length);

            Guid guid = new(buffer);

            return FromString(guid.ToString());
        }
        else if (Read<EDSInteger>(stream, options)?.ToInt32() is int length)
        {
            Encoding? encoding = flag switch
            {
                EDSStringFlags.Codepage_ASCII => Encoding.ASCII,
                EDSStringFlags.Codepage_UTF8 => Encoding.UTF8,
                EDSStringFlags.Codepage_UTF16LE => Encoding.Unicode,
                EDSStringFlags.Codepage_UTF16BE => Encoding.BigEndianUnicode,
                EDSStringFlags.Codepage_UTF32 => Encoding.UTF32,
                EDSStringFlags.Codepage_ISO8859 => Encoding.Latin1,
                EDSStringFlags.Codepage_Custom when Read<EDSInteger>(stream, options)?.ToInt32() is int cp => Encoding.GetEncoding(cp),
                _ => null,
            };
            byte[] buffer = new byte[length];

            stream.Read(buffer, 0, buffer.Length);

            if (use_gzip)
                buffer = GZIPUncompressData(buffer);

            return new(buffer, encoding);
        }

        return null;
    }

    public static EDSString? Cast(EDSObject @object) => @object switch
    {
        null or EDSNull => null,
        EDSString @string => @string,
        EDSBoolean boolean => FromString(boolean.Value.ToString()),
        EDSInteger integer => FromString(integer.ToJSON()),
        EDSFloat @float => FromString(@float.ToJSON()),
        EDSArray { Length: > 0 } array => Cast(array[0]),
        _ => FromString(@object.ToString()),
    };

    public static EDSString FromString(string @string) => @string.Length == 1 ? FromChar(@string[0]) : FromString(@string, DefaultEncoding);

    public static EDSString FromString(string @string, Encoding encoding) => FromByteArray(encoding.GetBytes(@string), encoding);

    public static EDSString FromGUID(Guid guid) => FromByteArray(guid.ToByteArray(), null);

    public static EDSString FromByteArray(byte[] bytes) => new(bytes, null);

    public static EDSString FromByteArray(byte[] bytes, Encoding? encoding) => new(bytes, encoding);

    public static EDSString FromChar(char @char)
    {
        if (@char <= 0xff)
            return FromByteArray(new[] { (byte)@char }, Encoding.Latin1);
        else
        {
            byte lo = (byte)(@char & 0xff);
            byte hi = (byte)((@char >> 8) & 0xff);

            return FromByteArray(new[] { lo, hi }, Encoding.Unicode);
        }
    }

    internal static byte[] GZIPCompressData(byte[] plain)
    {
        using MemoryStream ms = new();
        using GZipStream gzip = new(ms, CompressionLevel.SmallestSize);

        gzip.Write(plain, 0, plain.Length);
        gzip.Flush();

        return ms.ToArray();
    }

    internal static byte[] GZIPUncompressData(byte[] compressed)
    {
        using MemoryStream msi = new(compressed);
        using GZipStream gzip = new(msi, CompressionMode.Decompress);
        using MemoryStream mso = new();

        gzip.CopyTo(mso);
        mso.Seek(0, SeekOrigin.Begin);

        return mso.ToArray();
    }
}
