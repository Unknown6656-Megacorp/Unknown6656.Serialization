using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

namespace Unknown6656.Serialization;


public static class BinaryStreamExtensions
{
    public static byte[] Compress(this byte[] data, CompressionFunction algorithm) => algorithm.CompressData(data);

    public static byte[] Uncompress(this byte[] data, CompressionFunction algorithm) => algorithm.UncompressData(data);

    public static byte[] SerializeCallback(this Action<BinaryWriter> callback)
    {
        using MemoryStream ms = new();
        using BinaryWriter wr = new(ms);

        callback(wr);
        ms.Seek(0, SeekOrigin.Begin);

        return ms.ToArray();
    }

    public static void WriteNullable(this BinaryWriter writer, string? data)
    {
        if (data is { })
        {
            writer.Write(true);
            writer.Write(data);
        }
        else
            writer.Write(false);
    }

    public static string? ReadNullable(this BinaryReader reader) => reader.ReadBoolean() ? reader.ReadString() : null;

    public static unsafe void WriteNullable<T>(this BinaryWriter writer, T? data)
        where T : unmanaged
    {
        writer.Write(data is { });
        writer.WriteNative(data ?? default);
    }

    public static unsafe T? ReadNullable<T>(this BinaryReader reader)
        where T : unmanaged
    {
        bool some = reader.ReadBoolean();
        T data = reader.ReadNative<T>();

        return some ? (T?)data : null;
    }

    public static unsafe void WriteNative<T>(this BinaryWriter writer, T data)
        where T : unmanaged
    {
        byte* ptr = (byte*)&data;
        ReadOnlySpan<byte> rspan = new(ptr, sizeof(T));

        writer.Write(rspan);
    }

    public static unsafe T ReadNative<T>(this BinaryReader reader)
        where T : unmanaged
    {
        Span<byte> span = new byte[sizeof(T)];

        reader.Read(span);

        fixed (byte* ptr = span)
            return *(T*)ptr;
    }

    public static unsafe void WriteCollection<T>(this BinaryWriter writer, IEnumerable<T> data)
        where T : unmanaged
    {
        T[] array = data as T[] ?? data.ToArray();

        writer.Write(array.Length);

        foreach (T item in array)
            writer.WriteNative(item);
    }

    public static unsafe void WriteCollection<T>(this BinaryWriter writer, IEnumerable<IEnumerable<T>> data)
        where T : unmanaged
    {
        IEnumerable<T>[] array = data as IEnumerable<T>[] ?? data.ToArray();

        foreach (IEnumerable<T> collecion in array)
            writer.WriteCollection(collecion);
    }

    public static unsafe void WriteCollection<T>(this BinaryWriter writer, IEnumerable<IEnumerable<IEnumerable<T>>> data)
        where T : unmanaged
    {
        IEnumerable<IEnumerable<T>>[] array = data as IEnumerable<IEnumerable<T>>[] ?? data.ToArray();

        foreach (IEnumerable<IEnumerable<T>> collecion in array)
            writer.WriteCollection(collecion);
    }

    public static unsafe T[] ReadCollection<T>(this BinaryReader reader)
        where T : unmanaged
    {
        T[] array = new T[reader.ReadInt32()];

        for (int i = 0; i < array.Length; ++i)
            array[i] = reader.ReadNative<T>();

        return array;
    }

    public static unsafe T[][] ReadJaggedCollection2D<T>(this BinaryReader reader)
        where T : unmanaged
    {
        T[][] array = new T[reader.ReadInt32()][];

        for (int i = 0; i < array.Length; ++i)
            array[i] = reader.ReadCollection<T>();

        return array;
    }

    public static unsafe T[][][] ReadJaggedCollection3D<T>(this BinaryReader reader)
        where T : unmanaged
    {
        T[][][] array = new T[reader.ReadInt32()][][];

        for (int i = 0; i < array.Length; ++i)
            array[i] = reader.ReadJaggedCollection2D<T>();

        return array;
    }
}
