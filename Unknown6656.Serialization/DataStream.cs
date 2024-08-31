using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;
using System.Net.Http;
using System.Net.Mail;
using System.Net.Mime;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System;

using Unknown6656.Mathematics.LinearAlgebra;
using Unknown6656.Mathematics.Cryptography;
using Unknown6656.Mathematics.Numerics;
using Unknown6656.Generics;
using Unknown6656.Common;
using Unknown6656.Runtime;
using Unknown6656.Serialization;
using System.Runtime.Versioning;

namespace Unknown6656.Serialization;

// TODO : obj file format
// TODO : YAML file format
// TODO : memory mapped files
// DATA:
//   - bitmap
//   - qr code
//   - dictionary
//   - anonymous obj
// REPR:
//   - json
//   - xml
// TODO : add async methods


/// <summary>
/// A class containing serialization/deserialization functions.
/// </summary>
public unsafe class DataStream
    : MemoryStream
    , IDisposable
    , IEnumerable<byte>
{
    #region STATIC FIELDS / PROPERTIES

    private static readonly Regex FTP_PROTOCOL_REGEX = new(@"^(?<protocol>ftps?):\/\/(?<uname>[^:]+)(:(?<passw>[^@]+))?@(?<url>.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BASE64_REGEX = new(@"^.\s*data:\s*[^\w\/\-\+]+\s*;(\s*base64\s*,)?(?<data>(?:[a-z0-9+/]{4})*(?:[a-z0-9+/]{2}==|[a-z0-9+/]{3}=)?)$", RegexOptions.Compiled | RegexOptions.Compiled);
    private static readonly FieldInfo _MEMORYSTREAM_ORIGIN;
    private static readonly FieldInfo _MEMORYSTREAM_BUFFER;


    public static DataStream Empty { get; } = new([]);

    public static JsonSerializerOptions DefaultJSONOptions { get; } = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        IncludeFields = true,
    };

    public static Encoding DefaultDataStreamEncoding = Encoding.Default; // BytewiseEncoding.Instance;

    #endregion
    #region INSTANCE PROPERTIES

    private int MemoryStreamOrigin => (int)(_MEMORYSTREAM_ORIGIN.GetValue(this) ?? throw new InvalidOperationException());

    public Span<byte> Data => new(GetBuffer(), MemoryStreamOrigin, (int)Length);

    public DataStream this[Range range] => Slice(range);

    public DataStream this[Index start, Index end] => Slice(start, end);

    public ref byte this[Index index] => ref Data[index];

    #endregion
    #region .CTOR / .DTOR

    static DataStream()
    {
        InvalidProgramException ex = new($"The internal layout of the type '{typeof(MemoryStream)}' seems to have changed. Please contact https://github.com/unknown6656/!");

        _MEMORYSTREAM_ORIGIN = typeof(MemoryStream).GetField("_origin", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw ex;
        _MEMORYSTREAM_BUFFER = typeof(MemoryStream).GetField("_buffer", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw ex;
    }

    public DataStream()
        : this([])
    {
    }

    public DataStream(Stream ms) : this([]) => ms.CopyTo(this);

    public DataStream(IEnumerable<byte>? data)
        : this(data as byte[] ?? data?.ToArray())
    {
    }

    public DataStream(params byte[]? data)
        : base(data?.Length ?? 0)
    {
        if (data?.Length is int and > 0)
            base.Write(data, 0, data.Length);
    }

    #endregion
    #region INSTANCE METHODS

    public override byte[] GetBuffer() => (byte[])(_MEMORYSTREAM_BUFFER.GetValue(this) ?? throw new InvalidOperationException());

    public T ReadAt<T>(long index)
        where T : unmanaged
    {
        byte[] bytes = new byte[sizeof(T)];
        long pos = Position;

        SeekBeginning(index);
        Read(bytes, 0, bytes.Length);
        SeekBeginning(pos);

        fixed (byte* ptr = bytes)
            return *(T*)ptr;
    }

    public T ReadAt<T>(Index index) where T : unmanaged => ReadAt<T>((long)index.GetOffset((int)Length));

    public DataStream ReadAt<T>(long index, out T value)
        where T : unmanaged
    {
        value = ReadAt<T>(index);

        return this;
    }

    public DataStream ReadAt<T>(Index index, out T value) where T : unmanaged => ReadAt((long)index.GetOffset((int)Length), out value);

    public DataStream WriteAt<T>(Index index, T value) where T : unmanaged => WriteAt((long)index.GetOffset((int)Length), value);

    public DataStream WriteAt<T>(long index, T value)
        where T : unmanaged
    {
        byte[] bytes = new byte[sizeof(T)];
        byte* ptr = (byte*)&value;

        for (int i = 0; i < bytes.Length; ++i)
            bytes[i] = ptr[i];

        long pos = Position;

        SeekBeginning(index);
        Write(bytes, 0, bytes.Length);
        SeekBeginning(pos);

        return this;
    }

    public bool GetBit(long index) => (Data[(int)(index / 8)] & (1 << (int)(index % 8))) != 0;

    public DataStream SeekBeginning(long index = 0)
    {
        Seek(index, SeekOrigin.Begin);

        return this;
    }

    public DataStream GetBit(long index, out bool bit)
    {
        bit = GetBit(index);

        return this;
    }

    public DataStream SetBit(long index, bool new_value)
    {
        byte mask = (byte)(1 << (int)(index % 8));

        if (new_value)
            Data[(int)(index / 8)] |= mask;
        else
            Data[(int)(index / 8)] &= (byte)~mask;

        return this;
    }

    public DataStream SetBit(long index, bool new_value, out bool old_value)
    {
        old_value = GetBit(index);

        return SetBit(index, new_value);
    }

    public bool FlipBit(long index)
    {
        byte mask = (byte)(1 << (int)(index % 8));
        ref byte data = ref Data[(int)(index / 8)];

        data ^= mask;

        return (data & mask) != 0;
    }

    public DataStream FlipBit(long index, out bool new_value)
    {
        new_value = FlipBit(index);

        return this;
    }

    public void Transform(Func<byte, byte> transformer_func, bool parallel = true)
    {
        byte[] buffer = ToBytes();

        if (parallel)
            Parallel.For(0, buffer.LongLength, i => buffer[i] = transformer_func(buffer[i]));
        else
            for (long i = 0; i < buffer.LongLength; ++i)
                buffer[i] = transformer_func(buffer[i]);
    }

    public void Transform(Func<byte, long, byte> transformer_func, bool parallel = true)
    {
        byte[] buffer = ToBytes();

        if (parallel)
            Parallel.For(0, buffer.LongLength, i => buffer[i] = transformer_func(buffer[i], i));
        else
            for (long i = 0; i < buffer.LongLength; ++i)
                buffer[i] = transformer_func(buffer[i], i);
    }

    public DataStream ChangeEncoding(Encoding from, Encoding to) => FromString(ToString(from), to);

    public DataStream Compress(CompressionFunction algorithm) => ToBytes().Compress(algorithm);

    public DataStream Uncompress(CompressionFunction algorithm) => ToBytes().Uncompress(algorithm);

    public DataStream Encrypt(BinaryCipher algorithm, byte[] key) => ToBytes().Encrypt(algorithm, key);

    public DataStream Decrypt(BinaryCipher algorithm, byte[] key) => ToBytes().Decrypt(algorithm, key);

    public DataStream Hex() => FromString(ToHexString());

    public DataStream UnHex() => FromHex(ToString());

    public DataStream Hash<T>(T hash_function) where T : HashFunction<T> => hash_function.Hash(ToBytes());

    public DataStream Hash<T>() where T : HashFunction<T>, new() => Hash(new T());

    public DataStream HexDump(HexDumpOptions? options = null) => HexDump(Console.Out, options);

    public DataStream HexDump(TextWriter writer, HexDumpOptions? options = null)
    {
        Serialization.HexDump.Dump(ToBytes(), writer, options);

        return this;
    }

    public DataStream Slice(Index start, Index end) => Slice(start..end);

    public DataStream Slice(Range range) => FromBytes(ToBytes()[range]);

    public DataStream Concat(params DataStream[] others) => Concat(others.Prepend(this));

    public DataStream Append(params DataStream[] others) => Concat(others.Prepend(this));

    public DataStream Prepend(DataStream first) => DataStream.Concat(new[] { first, this });

    public DataStream Where(Func<byte, bool> predicate) => ToBytes().ToArrayWhere(predicate);

    public DataStream Select(Func<byte, byte> function) => ToBytes().ToArray(function);

    public DataStream Reverse() => ToBytes().Reverse().ToArray();

    public IEnumerator<byte> GetEnumerator() => ((IEnumerable<byte>)ToBytes()).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion
    #region READ / WRITE

    public char ReadChar() => ReadUnmanaged<char>();

    public string ReadUTF16String() => new(ReadCollection<char>());

    public bool ReadBoolean() => ReadUnmanaged<bool>();

    public sbyte ReadSByte() => ReadUnmanaged<sbyte>();

    public short ReadShort() => ReadUnmanaged<short>();

    public ushort ReadUShort() => ReadUnmanaged<ushort>();

    public int ReadInt() => ReadUnmanaged<int>();

    public uint ReadUInt() => ReadUnmanaged<uint>();

    public nint ReadNInt() => ReadUnmanaged<nint>();

    public nuint ReadNUInt() => ReadUnmanaged<nuint>();

    public long ReadLong() => ReadUnmanaged<long>();

    public ulong ReadULong() => ReadUnmanaged<ulong>();

    public float ReadFloat() => ReadUnmanaged<float>();

    public double ReadDouble() => ReadUnmanaged<double>();

    public decimal ReadDecimal() => ReadUnmanaged<decimal>();

    public UInt128 ReadUInt128() => ReadUnmanaged<UInt128>();

    public DateTime ReadDateTime() => ReadUnmanaged<DateTime>();

    public DateTimeOffset ReadDateTimeOffset() => ReadUnmanaged<DateTimeOffset>();

    public TimeSpan ReadTimeSpan() => ReadUnmanaged<TimeSpan>();

    public Guid ReadGuid() => ReadUnmanaged<Guid>();

    public void Write(byte u8) => WriteUnmanaged(u8);

    public void Write(bool @bool) => WriteUnmanaged(@bool);

    public void Write(sbyte i8) => WriteUnmanaged(i8);

    public void Write(short i16) => WriteUnmanaged(i16);

    public void Write(ushort u16) => WriteUnmanaged(u16);

    public void Write(int i32) => WriteUnmanaged(i32);

    public void Write(uint u32) => WriteUnmanaged(u32);

    public void Write(nint n32) => WriteUnmanaged(n32);

    public void Write(nuint nu32) => WriteUnmanaged(nu32);

    public void Write(long i64) => WriteUnmanaged(i64);

    public void Write(ulong u64) => WriteUnmanaged(u64);

    public void Write(float f32) => WriteUnmanaged(f32);

    public void Write(double f64) => WriteUnmanaged(f64);

    public void Write(decimal f128) => WriteUnmanaged(f128);

    public void Write(UInt128 u128) => WriteUnmanaged(u128);

    public void Write(DateTime dt) => WriteUnmanaged(dt);

    public void Write(DateTimeOffset dto) => WriteUnmanaged(dto);

    public void Write(TimeSpan sp) => WriteUnmanaged(sp);

    public void Write(Guid guid) => WriteUnmanaged(guid);

    public void WriteChar(char @char) => WriteUnmanaged(@char);

    public void WriteUTF16String(string @string) => WriteCollection(@string);

    public void WriteNullable(string? data)
    {
        if (data is { })
        {
            Write(true);
            WriteUTF16String(data);
        }
        else
            Write(false);
    }

    public string? ReadNullable() => ReadBoolean() ? ReadUTF16String() : null;

    public void WriteNullable<T>(T? data)
        where T : unmanaged
    {
        Write(data.HasValue);

        if (data.HasValue)
            WriteUnmanaged(data.Value);
    }

    public T? ReadNullable<T>() where T : unmanaged => ReadBoolean() ? ReadUnmanaged<T>() : null;

    public unsafe void WriteUnmanaged<T>(T data)
        where T : unmanaged
    {
        byte* ptr = (byte*)&data;
        ReadOnlySpan<byte> rspan = new(ptr, sizeof(T));

        Write(rspan);
    }

    public unsafe T ReadUnmanaged<T>()
        where T : unmanaged
    {
        Span<byte> span = new byte[sizeof(T)];

        Read(span);

        fixed (byte* ptr = span)
            return *(T*)ptr;
    }

    public unsafe void WriteCollection<T>(IEnumerable<T> data)
        where T : unmanaged
    {
        T[] array = data as T[] ?? data.ToArray();

        Write(array.Length);

        foreach (T item in array)
            WriteUnmanaged(item);
    }

    public unsafe void WriteCollection<T>(IEnumerable<IEnumerable<T>> data)
        where T : unmanaged
    {
        IEnumerable<T>[] array = data as IEnumerable<T>[] ?? data.ToArray();

        Write(array.Length);

        foreach (IEnumerable<T> collecion in array)
            WriteCollection(collecion);
    }

    public unsafe void WriteCollection<T>(IEnumerable<IEnumerable<IEnumerable<T>>> data)
        where T : unmanaged
    {
        IEnumerable<IEnumerable<T>>[] array = data as IEnumerable<IEnumerable<T>>[] ?? data.ToArray();

        Write(array.Length);

        foreach (IEnumerable<IEnumerable<T>> collecion in array)
            WriteCollection(collecion);
    }

    public unsafe T[] ReadCollection<T>()
        where T : unmanaged
    {
        T[] array = new T[ReadInt()];

        for (int i = 0; i < array.Length; ++i)
            array[i] = ReadUnmanaged<T>();

        return array;
    }

    public unsafe T[][] ReadJaggedCollection2D<T>()
        where T : unmanaged
    {
        T[][] array = new T[ReadInt()][];

        for (int i = 0; i < array.Length; ++i)
            array[i] = ReadCollection<T>();

        return array;
    }

    public unsafe T[][][] ReadJaggedCollection3D<T>()
        where T : unmanaged
    {
        T[][][] array = new T[ReadInt()][][];

        for (int i = 0; i < array.Length; ++i)
            array[i] = ReadJaggedCollection2D<T>();

        return array;
    }

    #endregion
    #region DESERIALIZATION

    public override string ToString() => ToString(DefaultDataStreamEncoding);

    public string ToString(Encoding encoding) => encoding.GetString(Data);

    public StringBuilder ToStringBuilder() => ToStringBuilder(DefaultDataStreamEncoding);

    public StringBuilder ToStringBuilder(Encoding encoding) => new(ToString(encoding));

    public void SendAsEMailBody(string smtp_server, string email_address, string password, string recipient_email, string subject, ushort smtp_port = 587, bool ssl = true, bool body_as_html = false)
    {
        using SmtpClient client = new(smtp_server)
        {
            Credentials = new NetworkCredential(email_address, password),
            Port = smtp_port,
            EnableSsl = ssl,
        };

        client.Send(new MailMessage(email_address, recipient_email, subject, ToString())
        {
            IsBodyHtml = body_as_html,
        });
    }

    public void SendAsEMailAttachment(string smtp_server, string email_address, string password, string recipient_email, string subject, string body, ContentType attachment_type, ushort smtp_port = 587, bool ssl = true, bool body_as_html = false)
    {
        using SmtpClient client = new(smtp_server)
        {
            Credentials = new NetworkCredential(email_address, password),
            Port = smtp_port,
            EnableSsl = ssl,
        };
        MailMessage email = new(email_address, recipient_email, subject, body)
        {
            IsBodyHtml = body_as_html,
        };
        email.Attachments.Add(new Attachment(this, attachment_type));

        client.Send(email);
    }

    public string[] ToLines(string separator = "\n") => ToLines(DefaultDataStreamEncoding, separator);

    public string[] ToLines(Encoding enc, string separator = "\n") => ToString(enc).SplitIntoLines(separator);

    public string ToDrunkBishop(DrunkBishopOptions? options = null) => DrunkBishop.Print(ToBytes(), options);

    public string ToHexString(bool uppercase = true, bool spacing = false) => string.Join(spacing ? " " : "", ToBytes().Select(b => b.ToString(uppercase ? "X2" : "x2")));

    public string ToBaseString(int @base)
    {
        if (@base == 16)
            return ToHexString(false, false);
        else if (@base == 64)
            return ToBase64();
        else if (@base == 2)
            ToBytes().Select(b => Convert.ToString(b, 2).PadLeft(8, '0')).StringJoin("");
        else
            ; // TODO : make use of bitstreams?


        throw new NotImplementedException();
    }

    public string ToBase64() => Convert.ToBase64String(Data);

    public string ToDataURI(string mime = "application/octet-stream") => $"data:{mime};base64,{ToBase64()}";

    public BinaryReader ToBinaryReader() => new(this);

    public void CopyToBinaryWriter(BinaryWriter writer) => writer.Write(Data);

    public void ToFile(string path) => ToFile(path, FileMode.Create);

    public void ToFile(string path, FileMode mode, FileAccess access = FileAccess.Write, FileShare share = FileShare.Read)
    {
        using FileStream fs = new(path, mode, access, share);

        fs.Write(Data);
        fs.Flush();
        fs.Close();
        fs.Dispose();
    }

    public void ToFile(Uri path) => ToFile(Path.GetFileName(path.LocalPath));

    public void ToFile(Uri path, FileMode mode, FileAccess access = FileAccess.Write, FileShare share = FileShare.Read) =>
        ToFile(Path.GetFileName(path.LocalPath), mode, access, share);

    public void ToFile(FileInfo file) => ToFile(file.FullName);

    public void ToFile(FileInfo file, FileMode mode, FileAccess access = FileAccess.Write, FileShare share = FileShare.Read) =>
        ToFile(file.FullName, mode, access, share);

    [SupportedOSPlatform(OS.WIN)]
    public Bitmap ToBitmap() => (Bitmap)Image.FromStream(this);

    public Bitmap ToQOIFBitmap() => QOIF.LoadQOIFImage(this);

    public Bitmap ToRGBAEncodedBitmap()
    {
        RGBAColor[] pixels = ToArray<RGBAColor>();
        int len = pixels.Length;
        int i = (int)Math.Sqrt(len);
        int fac = 1;

        while (i-- > 1)
            if (len % i == 0)
            {
                fac = i;

                break;
            }

        Bitmap bitmap = new(fac, len / fac, PixelFormat.Format32bppArgb);

        bitmap.LockRGBAPixels((ptr, _, _) => pixels.CopyTo(ptr));

        return bitmap;
    }

    public void ToPointer<T>(T* pointer)
        where T : unmanaged
    {
        byte* dst = (byte*)pointer;

        for (int i = 0, l = Data.Length; i < l; ++i)
            dst[i] = Data[i];
    }

    public byte[] ToBytes() => Data.ToArray();

    public T ToUnmanaged<T>()
        where T : unmanaged
    {
        T t = default;

        ToPointer(&t);

        return t;
    }

    public T[] ToArray<T>()
        where T : unmanaged
    {
        byte[] arr = ToBytes();

        fixed (byte* ptr = arr)
        {
            int len = *(int*)ptr;
            T[] res = new T[len];
            T* src = (T*)(ptr + 4);

            for (int i = 0; i < len; ++i)
                res[i] = src[i];

            return res;
        }
    }

    public T[][] ToJaggedArray2D<T>() where T : unmanaged => ToBinaryReader().ReadJaggedCollection2D<T>();

    public T[][][] ToJaggedArray3D<T>() where T : unmanaged => ToBinaryReader().ReadJaggedCollection3D<T>();

    public T[][][][] ToJaggedArray4D<T>()
        where T : unmanaged
    {
        BinaryReader reader = ToBinaryReader();
        int size = reader.ReadInt32();
        T[][][][] arrays = new T[size][][][];

        for (int i = 0; i < size; ++i)
            arrays[i] = reader.ReadJaggedCollection3D<T>();

        return arrays;
    }

    public T[,] ToMultiDimensionalArray2D<T>()
        where T : unmanaged
    {
        DataStream[] sources = ToArrayOfSources();
        int dim0 = sources[0].ToUnmanaged<int>();
        int dim1 = sources[1].ToUnmanaged<int>();
        T[] flat = sources[2].ToArray<T>();
        T[,] array = new T[dim0, dim1];

        Parallel.For(0, flat.Length, i => array[i / dim0, i % dim0] = flat[i]);

        return array;
    }

    public T[,,] ToMultiDimensionalArray3D<T>()
        where T : unmanaged
    {
        DataStream[] sources = ToArrayOfSources();
        int dim0 = sources[0].ToUnmanaged<int>();
        int dim1 = sources[1].ToUnmanaged<int>();
        int dim2 = sources[2].ToUnmanaged<int>();
        T[] flat = sources[3].ToArray<T>();
        T[,,] array = new T[dim0, dim1, dim2];

        Parallel.For(0, flat.Length, i => array[i / (dim2 * dim1), i / dim2 % dim1, i % dim2] = flat[i]);

        return array;
    }

    public T[,,,] ToMultiDimensionalArray4D<T>()
        where T : unmanaged
    {
        DataStream[] sources = ToArrayOfSources();
        int dim0 = sources[0].ToUnmanaged<int>();
        int dim1 = sources[1].ToUnmanaged<int>();
        int dim2 = sources[2].ToUnmanaged<int>();
        int dim3 = sources[3].ToUnmanaged<int>();
        T[] flat = sources[4].ToArray<T>();
        T[,,,] array = new T[dim0, dim1, dim2, dim3];

        Parallel.For(0, flat.Length, i => array[i / (dim3 * dim2 * dim1), i / (dim3 * dim2) % dim1, i / dim3 % dim2, i % dim3] = flat[i]);

        return array;
    }

    public DataStream[] ToArrayOfSources() => ToJaggedArray2D<byte>().ToArray(bytes => new DataStream(bytes));

    public Span<T> ToSpan<T>() where T : unmanaged => ToArray<T>().AsSpan();

    public ReadOnlySpan<T> ToReadOnlySpan<T>() where T : unmanaged => new(ToArray<T>());

    public Memory<T> ToMemory<T>() where T : unmanaged => new(ToArray<T>());

    public ReadOnlyMemory<T> ToReadOnlyMemory<T>() where T : unmanaged => new(ToArray<T>());

    public Field[,] ToCompressedMatrix<Field>() where Field : unmanaged, IField<Field> => ToCompressedStorageFormat<Field>().ToMatrix();

    public CompressedStorageFormat<Field> ToCompressedStorageFormat<Field>()
        where Field : unmanaged, IField<Field> => CompressedStorageFormat<Field>.FromBytes(ToBytes());

    public UnsafeFunctionPointer ToFunctionPointer()
    {
        byte[] bytes = ToBytes();
        void* buffer;

#pragma warning disable CA1416 // Validate platform compatibility
        if (OS.IsWindows)
        {
            buffer = NativeInterop.VirtualAlloc(null, bytes.Length, 0x1000, 4);

            int dummy;

            Marshal.Copy(bytes, 0, (nint)buffer, bytes.Length);
            NativeInterop.VirtualProtect(buffer, bytes.Length, 0x20, &dummy);
        }
        else if (OS.IsPosix)
        {
            NativeInterop.posix_memalign(&buffer, 4096, bytes.Length);
            Marshal.Copy(bytes, 0, (nint)buffer, bytes.Length);
            NativeInterop.mprotect(buffer, bytes.Length, 0b_0000_0111); // rwx
        }
#pragma warning restore CA1416
        else
            throw new InvalidOperationException("The current OS execution platform is unsupported. Try running it again on Windows/Linux/MacOSX/FreeBSD.");

        return new UnsafeFunctionPointer(buffer, bytes.Length);
    }

    public INIFile ToINI() => ToINI(DefaultDataStreamEncoding);

    public INIFile ToINI(Encoding encoding) => INIFile.FromINIString(ToString(encoding));

    public T? ToJSON<T>(JsonSerializerOptions? options = null) => ToJSON<T>(DefaultDataStreamEncoding, options);

    public T? ToJSON<T>(Encoding enc, JsonSerializerOptions? options = null) => (T)ToJSON(typeof(T), enc, options)!;

    public object? ToJSON(Type type, JsonSerializerOptions? options = null) => ToJSON(type, DefaultDataStreamEncoding, options);

    public object? ToJSON(Type type, Encoding enc, JsonSerializerOptions? options = null) => JsonSerializer.Deserialize(ToString(enc), type, options ?? DefaultJSONOptions);

    public string Disassemble(Disassembler disassembler) => disassembler.Disassemble(ToBytes());

    public string Disassemble<Disassembler, Config>(Disassembler disassembler, Config config)
        where Disassembler : Disassembler<Config> => disassembler.Disassemble(ToBytes(), config);

    public string Disassemble<Disassembler, Config>(Config config) where Disassembler : Disassembler<Config>, new() => Disassemble(new Disassembler(), config);

    public string DisassembleIL(Module module_context) => Disassemble<ILDisassembler, Module>(module_context);

    public string DisassembleARM() => Disassemble(new ARMDisassembler());

    public string DisassembleX86() => Disassemble(new X86Disassembler());

    // [Obsolete("See https://aka.ms/binaryformatter", true)]
    // public object ToSerializable()
    // {
    //     BinaryFormatter fmt = new();
    //     using MemoryStream ms = new();
    //
    //     ToStream(ms);
    //     ms.Seek(0, SeekOrigin.Begin);
    //
    //     return fmt.Deserialize(ms);
    // }
    //
    // [Obsolete("See https://aka.ms/binaryformatter", true)]
    // public T ToSerializable<T>() => (T)ToSerializable();

    public object? ToObject() => LINQ.TryDo(() =>
    {
        DataStream[] sources = ToArrayOfSources();

        return sources[1].ToJSON(sources[0].ToType());
    }, null);

    public Type? ToType()
    {
        DataStream[] sources = ToArrayOfSources();
        Guid clsid = sources[0].ToUnmanaged<Guid>();
        string name = sources[1].ToString();

        return Type.GetTypeFromCLSID(clsid) ?? Type.GetType(name);
    }

    #endregion
    #region STATIC METHODS

    public static DataStream Concat(IEnumerable<DataStream?>? sources)
    {
        DataStream s = new();

        foreach (DataStream? source in sources ?? Array.Empty<DataStream>())
            if (source?.ToBytes() is byte[] data)
                s.Write(data, 0, data.Length);

        return s.SeekBeginning();
    }

    #endregion
    #region SERIALIZATION

    public static DataStream FromUnmanaged<T>(T data) where T : unmanaged => FromPointer(&data);

    public static DataStream FromPointer<T>(T* data) where T : unmanaged => FromPointer(data, sizeof(T));

    public static DataStream FromPointer(nint pointer, int byte_count) => FromPointer((void*)pointer, byte_count);

    public static DataStream FromPointer(void* data, int byte_count) => FromPointer((byte*)data, byte_count);

    public static DataStream FromPointer<T>(T* data, int byte_count)
        where T : unmanaged
    {
        byte[] arr = new byte[byte_count];
        byte* ptr = (byte*)data;

        for (int i = 0; i < byte_count; ++i)
            arr[i] = ptr[i];

        return FromBytes(arr);
    }

    public static DataStream FromArray<T>(IEnumerable<T> collection)
        where T : unmanaged
    {
        T[] array = collection as T[] ?? collection.ToArray();
        byte[] bytes = new byte[array.Length * sizeof(T) + 4];

        fixed (byte* ptr = bytes)
        {
            *(int*)ptr = array.Length;
            T* dst = (T*)(ptr + 4);

            for (int i = 0; i < array.Length; ++i)
                dst[i] = array[i];
        }

        return FromBytes(bytes);
    }

    public static DataStream FromArrayOfSources(IEnumerable<DataStream> sources) => FromArrayOfSources(sources.ToArray());

    public static DataStream FromArrayOfSources(params DataStream[] sources) => FromJaggedArray(sources.ToArray(s => s.ToArray()));

    public static DataStream FromJaggedArray<T>(T[][] array)
        where T : unmanaged
    {
        DataStream[] arrays = array.ToArray(FromArray);

        return FromUnmanaged(arrays.Length).Append(arrays);
    }

    public static DataStream FromJaggedArray<T>(T[][][] array)
        where T : unmanaged
    {
        DataStream[] arrays = array.ToArray(FromJaggedArray);

        return FromUnmanaged(arrays.Length).Append(arrays);
    }

    public static DataStream FromJaggedArray<T>(T[][][][] array)
        where T : unmanaged
    {
        DataStream[] arrays = array.ToArray(FromJaggedArray);

        return FromUnmanaged(arrays.Length).Append(arrays);
    }

    public static DataStream FromMultiDimensionalArray<T>(T[,] array)
        where T : unmanaged
    {
        int dim0 = array.GetLength(0);
        int dim1 = array.GetLength(1);
        T[] flat = new T[dim0 * dim1];

        Parallel.For(0, flat.Length, i => flat[i] = array[i / dim0, i % dim0]);

        return FromArrayOfSources(
            FromUnmanaged(dim0),
            FromUnmanaged(dim1),
            FromArray(flat)
        );
    }

    public static DataStream FromMultiDimensionalArray<T>(T[,,] array)
        where T : unmanaged
    {
        int dim0 = array.GetLength(0);
        int dim1 = array.GetLength(1);
        int dim2 = array.GetLength(2);
        T[] flat = new T[dim0 * dim1 * dim2];

        Parallel.For(0, flat.Length, i => flat[i] = array[i / (dim2 * dim1), i / dim2 % dim1, i % dim2]);

        return FromArrayOfSources(
            FromUnmanaged(dim0),
            FromUnmanaged(dim1),
            FromUnmanaged(dim2),
            FromArray(flat)
        );
    }

    public static DataStream FromMultiDimensionalArray<T>(T[,,,] array)
        where T : unmanaged
    {
        int dim0 = array.GetLength(0);
        int dim1 = array.GetLength(1);
        int dim2 = array.GetLength(2);
        int dim3 = array.GetLength(3);
        T[] flat = new T[dim0 * dim1 * dim2 * dim3];

        Parallel.For(0, flat.Length, i => flat[i] = array[i / (dim3 * dim2 * dim1), i / (dim3 * dim2) % dim1, i / dim3 % dim2, i % dim3]);

        return FromArrayOfSources(
            FromUnmanaged(dim0),
            FromUnmanaged(dim1),
            FromUnmanaged(dim2),
            FromUnmanaged(dim3),
            FromArray(flat)
        );
    }

    public static DataStream FromMultiDimensionalArray<T>(Array array, int dimensions)
    {
        int[] dims = Enumerable.Range(0, dimensions).ToArray(array.GetLength);

        // Todo

        throw new NotImplementedException();
    }

    public static DataStream FromBitmapAsRGBAEncoded(Bitmap bitmap) => FromArray(bitmap.ToPixelArray());

    public static DataStream FromQOIFBitmap(Bitmap bitmap, QOIFVersion format_version = QOIFVersion.Original)
    {
        DataStream ds = new();

        QOIF.SaveQOIFImage(bitmap, ds, format_version);

        return ds;
    }

    public static DataStream FromBitmap(Bitmap bitmap) => FromBitmap(bitmap, ImageFormat.Png);

    public static DataStream FromBitmap(Bitmap bitmap, ImageFormat format)
    {
        using MemoryStream ms = new();

        bitmap.Save(ms, format);

        return FromStream(ms);
    }

    public static DataStream FromStream(Stream stream, bool seek_beginning = true)
    {
        if (seek_beginning && stream.CanSeek)
            stream.Seek(0, SeekOrigin.Begin);

        return new DataStream(stream).SeekBeginning();
    }

    public static DataStream FromString(object? obj) => FromString(obj, DefaultDataStreamEncoding);

    public static DataStream FromString(object? obj, Encoding enc) => FromString(obj?.ToString() ?? "", enc);

    public static DataStream FromString(string str) => FromString(str, DefaultDataStreamEncoding);

    public static DataStream FromString(string str, Encoding enc) => FromBytes(enc.GetBytes(str));

    public static DataStream FromINI(INISection ini_section) => FromINI(ini_section, DefaultDataStreamEncoding);

    public static DataStream FromINI(INISection ini_section, Encoding enc) => FromINI(new INIFile() { [string.Empty] = ini_section }, enc);

    public static DataStream FromINI(INIFile ini) => FromINI(ini, DefaultDataStreamEncoding);

    public static DataStream FromINI(INIFile ini, Encoding enc) => FromString(ini.Serialize(), enc);

    public static DataStream FromObjectAsJSON(object? obj, JsonSerializerOptions? options = null) => FromObjectAsJSON(obj, DefaultDataStreamEncoding, options);

    public static DataStream FromObjectAsJSON(object? obj, Encoding enc, JsonSerializerOptions? options = null) => FromString(JsonSerializer.Serialize(obj, options ?? DefaultJSONOptions), enc);

    [Obsolete("See https://aka.ms/binaryformatter", true)]
    public static DataStream FromSerializable(object serializable)
    {
        BinaryFormatter fmt = new();
        MemoryStream ms = new();

        fmt.Serialize(ms, serializable);

        return FromStream(ms);
    }

    public static DataStream FromObject(object? obj)
    {
        if (obj is null)
            return Empty;
        else
            return FromArrayOfSources(
                FromType(obj.GetType()),
                FromObjectAsJSON(obj)
            );
    }

    public static DataStream FromStringBuilder(StringBuilder sb) => FromStringBuilder(sb, DefaultDataStreamEncoding);

    public static DataStream FromStringBuilder(StringBuilder sb, Encoding enc) => FromString(sb.ToString(), enc);

    public static DataStream FromTextLines(IEnumerable<string> lines, string separator = "\n") => FromTextLines(lines, DefaultDataStreamEncoding, separator);

    public static DataStream FromTextLines(IEnumerable<string> lines, Encoding enc, string separator = "\n") => FromString(string.Join(separator, lines), enc);

    public static DataStream FromBase64(string str) => FromBytes(Convert.FromBase64String(str));

    public static DataStream FromFile(string path) => FromFile(new FileInfo(path));

    public static DataStream FromFile(string path, FileMode mode, FileAccess access = FileAccess.Read, FileShare share = FileShare.Read) =>
        FromFile(new FileInfo(path), mode, access, share);

    public static DataStream FromFile(FileInfo file) => FromFile(file, FileMode.Open);

    public static DataStream FromFile(FileInfo file, FileMode mode, FileAccess access = FileAccess.Read, FileShare share = FileShare.Read)
    {
        using FileStream fs = new(file.FullName, mode, access, share);
        DataStream data = FromStream(fs);

        fs.Close();
        fs.Dispose();

        return data.SeekBeginning();
    }

    public static DataStream FromFile(Uri path) => FromFile(Path.GetFileName(path.LocalPath));

    public static DataStream FromFile(Uri path, FileMode mode, FileAccess access = FileAccess.Read, FileShare share = FileShare.Read) =>
        FromFile(Path.GetFileName(path.LocalPath), mode, access, share);

    public static DataStream FromCompressedMatrix<Field>(Field[,] matrix)
        where Field : unmanaged, IField<Field> => FromCompressedStorageFormat(new CompressedStorageFormat<Field>(matrix));

    public static DataStream FromCompressedMatrix<Field>(Algebra<Field>.IComposite2D matrix)
        where Field : unmanaged, IField<Field> => FromCompressedStorageFormat(new CompressedStorageFormat<Field>(matrix));

    public static DataStream FromCompressedStorageFormat<Field>(CompressedStorageFormat<Field> compressed) where Field : unmanaged, IField<Field> => FromBytes(compressed.ToBytes());

    public static DataStream FromWebResource(Uri uri)
    {
        using WebClient wc = new();

        return FromBytes(wc.DownloadData(uri));
    }

    public static DataStream FromWebResource(string uri)
    {
        using WebClient wc = new();

        return FromBytes(wc.DownloadData(uri));
    }

    public static DataStream FromHTTP(string uri)
    {
        using HttpClient hc = new()
        {
            Timeout = new TimeSpan(0, 0, 15)
        };

        return FromBytes(hc.GetByteArrayAsync(uri)
                       .ConfigureAwait(false)
                       .GetAwaiter()
                       .GetResult());
    }

    public static DataStream FromFTP(string uri)
    {
        FtpWebRequest req;
        byte[] content;

        if (uri.Match(FTP_PROTOCOL_REGEX, out ReadOnlyIndexer<string, string>? g))
        {
            req = (FtpWebRequest)WebRequest.Create($"{g["protocol"]}://{g["url"]}");
            req.Method = WebRequestMethods.Ftp.DownloadFile;
            req.Credentials = new NetworkCredential(g["uname"], g["passw"]);
        }
        else
        {
            req = (FtpWebRequest)WebRequest.Create(uri);
            req.Method = WebRequestMethods.Ftp.DownloadFile;
        }

        using (FtpWebResponse resp = (FtpWebResponse)req.GetResponse())
        using (Stream s = resp.GetResponseStream())
            return FromStream(s);
    }

    public static DataStream FromDataURI(string uri)
    {
        if (uri.Match(BASE64_REGEX, out ReadOnlyIndexer<string, string>? groups))
            return FromBase64(groups!["data"]);

        throw new ArgumentException("Invalid data URI.");
    }

    public static DataStream FromHex(string str)
    {
        str = new string(str.ToLower().Where(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')).ToArray());

        if ((str.Length % 2) == 1)
            str = '0' + str;

        byte[] data = new byte[str.Length / 2];

        for (int i = 0; i < data.Length; ++i)
            data[i] = byte.Parse(str[(i * 2)..((i + 1) * 2)], NumberStyles.HexNumber);

        return FromBytes(data);
    }

    public static DataStream FromBytes(IEnumerable<byte>? bytes) => FromBytes(bytes?.ToArray());

    public static DataStream FromBytes(params byte[]? bytes) => new(bytes ?? []);

    public static DataStream FromBytes(byte[] bytes, int offset) => FromBytes(bytes[offset..]);

    public static DataStream FromBytes(byte[] bytes, int offset, int count) => FromBytes(bytes[offset..(offset + count)]);

    public static DataStream FromSpan<T>(Span<T> bytes) where T : unmanaged => FromArray(bytes.ToArray());

    public static DataStream FromSpan<T>(ReadOnlySpan<T> bytes) where T : unmanaged => FromArray(bytes.ToArray());

    public static DataStream FromMemory<T>(Memory<T> bytes) where T : unmanaged => FromArray(bytes.ToArray());

    public static DataStream FromMemory<T>(ReadOnlyMemory<T> bytes) where T : unmanaged => FromArray(bytes.ToArray());

    public static DataStream FromType(Type type) => FromArrayOfSources(FromUnmanaged(type.GUID), FromString(type.AssemblyQualifiedName ?? type.FullName ?? type.ToString()));

    public static DataStream FromType<T>() => FromType(typeof(T));

    // public static DataStream FromMemoryMappedFile(MemoryMapped)

    [SkipLocalsInit]
    public static DataStream FromGhostStackFrame(int offset, int size)
    {
        byte __start;

        return FromPointer(&__start + offset, size);
    }

    #endregion
    #region OPERATORS

    public static DataStream operator +(DataStream first, DataStream second) => first.Concat(second);

    public static implicit operator byte[](DataStream data) => data.ToBytes();

    public static implicit operator DataStream(byte[] bytes) => new(bytes);

    #endregion
}

public unsafe sealed partial class UnsafeFunctionPointer
    : IDisposable
{
    public bool IsDisposed { get; private set; } = false;

    public void* BufferAddress { get; }

    public int BufferSize { get; }

    public ReadOnlySpan<byte> InstructionBytes => new(BufferAddress, BufferSize);


    internal UnsafeFunctionPointer(void* buffer, int size)
    {
        BufferAddress = buffer;
        BufferSize = size;
    }

    ~UnsafeFunctionPointer() => Dispose(false);

    private void Dispose(bool managed)
    {
        if (!IsDisposed)
        {
            if (managed)
            {
                // TODO: dispose managed state (managed objects)
            }

#pragma warning disable CA1416 // Validate platform compatibility
            if (OS.IsWindows)
                NativeInterop.VirtualFree(BufferAddress, 0, 0x8000);
            else
                NativeInterop.free(BufferAddress);
#pragma warning restore CA1416

            IsDisposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(managed: true);

        GC.SuppressFinalize(this);
    }

    public static UnsafeFunctionPointer FromBuffer(Span<byte> buffer) => DataStream.FromSpan(buffer).ToFunctionPointer();

    public static UnsafeFunctionPointer FromBuffer(ReadOnlySpan<byte> buffer) => DataStream.FromSpan(buffer).ToFunctionPointer();

    public static UnsafeFunctionPointer FromBuffer(Memory<byte> buffer) => DataStream.FromMemory(buffer).ToFunctionPointer();

    public static UnsafeFunctionPointer FromBuffer(ReadOnlyMemory<byte> buffer) => DataStream.FromMemory(buffer).ToFunctionPointer();

    public static UnsafeFunctionPointer FromBuffer(IEnumerable<byte> bytes) => FromBuffer(new Span<byte>(bytes.ToArray()));
}
