using Unknown6656.EDS.Internals;

namespace Unknown6656.EDS;


public static partial class Serializer
{
    public static byte[] Serialize(object? value, Type type) => Serialize(value, type, SerializerOptions.DefaultOptions);

    public static byte[] Serialize(object? value, Type type, SerializerOptions options)
    {
        using MemoryStream ms = new();

        Serialize(value, type, ms, options);

        ms.Seek(0, SeekOrigin.Begin);

        return ms.ToArray();
    }

    public static byte[] Serialize<T>(T? value) => Serialize(value, SerializerOptions.DefaultOptions);

    public static byte[] Serialize<T>(T? value, SerializerOptions options) => Serialize(value, typeof(T), options);

    public static void Serialize<T>(T? value, Stream stream) => Serialize(value, stream, SerializerOptions.DefaultOptions);

    public static void Serialize<T>(T? value, Stream stream, SerializerOptions options) => Serialize(value, typeof(T), stream, options);

    public static void Serialize(object? value, Type type, Stream stream) => Serialize(value, type, stream, SerializerOptions.DefaultOptions);

    public static void Serialize(object? value, Type type, Stream stream, SerializerOptions options) => EDSObject.FromObject(value, type, options).Write(stream, options);

    public static T? Deserialize<T>(Stream stream) => (T?)Deserialize(stream, typeof(T), SerializerOptions.DefaultOptions);

    public static T? Deserialize<T>(Stream stream, SerializerOptions options) => (T?)Deserialize(stream, typeof(T), options);

    public static T? Deserialize<T>(ReadOnlySpan<byte> bytes) => (T?)Deserialize(bytes, typeof(T), SerializerOptions.DefaultOptions);

    public static T? Deserialize<T>(ReadOnlySpan<byte> bytes, SerializerOptions options) => (T?)Deserialize(bytes, typeof(T), options);

    public static object? Deserialize(ReadOnlySpan<byte> bytes, Type? type) => Deserialize(bytes, type, SerializerOptions.DefaultOptions);

    public static object? Deserialize(ReadOnlySpan<byte> bytes, Type? type, SerializerOptions options)
    {
        using (MemoryStream ms = new(bytes.ToArray()))
            return Deserialize(ms, type, options);
    }

    public static object? Deserialize(Stream stream, Type? type) => Deserialize(stream, type, SerializerOptions.DefaultOptions);

    public static object? Deserialize(Stream stream, Type? type, SerializerOptions options) => EDSObject.Read(stream, options).ToObject(type, options);
}
