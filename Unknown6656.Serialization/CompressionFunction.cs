using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System;

using Unknown6656.Mathematics.Cryptography;
using Unknown6656.Mathematics.Numerics;
using Unknown6656.Generics;

namespace Unknown6656.IO;


/// <completionlist cref="CompressionFunction"/>
public abstract partial class CompressionFunction
    : HashFunction<CompressionFunction>
{
    [Obsolete, EditorBrowsable(EditorBrowsableState.Never), DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public static RLECompression RLE { get; } = new();

    public sealed override int HashSize => throw new InvalidOperationException("The size of compressed data cannot be determined outside an active data compression.");


    public sealed override byte[] Hash(byte[] data) => CompressData(data);

    public abstract byte[] CompressData(byte[] data);

    public abstract byte[] UncompressData(byte[] data);
}

[Obsolete, EditorBrowsable(EditorBrowsableState.Never)]
public sealed class RLECompression
    : CompressionFunction
{
    private sealed class DictionaryEntry
    {
        public byte[]? Sequence { set; get; }
        public VarInt? Codepoint { set; get; }
        public int Occurrence { get; set; }

        public override string ToString() => $"{Occurrence}x {Codepoint}: {DataStream.FromBytes(Sequence).ToHexString()}";
    }


    public override byte[] CompressData(byte[] data)
    {
        List<DictionaryEntry> dictionary = [];
        List<VarInt> output = [];
        int cp = 256;

        for (int i = 0; i < data.Length;)
        {
            int length = 1;
            DictionaryEntry? entry = (from tuple in dictionary
                                      where tuple.Sequence is { }
                                      let len = tuple.Sequence!.Length
                                      orderby len descending
                                      where tuple.Sequence!.SequenceEqual(data.Slice(i, len))
                                      select tuple).FirstOrDefault();

            if (entry is { })
            {
                output.Add(entry.Codepoint!);
                entry.Occurrence++;
                length = entry.Sequence!.Length;
            }
            else
                output.Add(data[i]);

            var lol = new DictionaryEntry
            {
                Codepoint = cp++,
                Sequence = data.Slice(i, length + 1).ToArray(),
                Occurrence = 0,
            };
            dictionary.Add(lol);

            i += length;
        }

        dictionary.RemoveAll(entry => entry.Occurrence == 0);

        cp = 256;
        Dictionary<VarInt, int> map = (from entry in dictionary
                                       where entry.Occurrence > 0
                                       select (entry.Codepoint, cp++)).ToDictionary();

        for (int i = 0; i < output.Count; ++i)
            if ((int)output[i] > 255)
                output[i] = map[output[i]];

        output.Insert(0, dictionary.Count);
        output.InsertRange(1, dictionary.Select(entry => VarInt.FromBytes(entry.Sequence!)));

        return output.SelectMany(item => item.Serialize()).ToArray();
    }

    public override byte[] UncompressData(byte[] data)
    {
        using MemoryStream ms = new(data);
        List<VarInt> input = [];
        List<byte> output = [];

        while (ms.Position < ms.Length)
            input.Add(VarInt.Deserialize(ms));

        Dictionary<VarInt, byte[]> dic = [];
        int count = (int)input[0];

        for (int i = 0; i < count; ++i)
            dic[256 + i] = input[i + 1].InternalBytes;

        for (int i = count + 1; i < input.Count; ++i)
            output.AddRange(input[i] < 256 ? input[i].InternalBytes : dic[input[i]]);

        return [.. output];
    }
}
