using System.Diagnostics.CodeAnalysis;
using System.Collections;

namespace Unknown6656.EDS.Internals;


internal enum EDSDictionaryFlags
    : byte
{
    Null = 0b_0000_0000,

    MASK_SmallLarge = 0b_1111_0000,
    VALUE_Small = 0b_1110_0000,

    LargeShortNamedDictionary = 0b_1110_0001,
    LargeFullNamedDictionary = 0b_1110_0010,
}

public sealed partial class EDSDictionary
    : EDSObject
    , EDSObject<EDSDictionary>
    , IDictionary<string, EDSObject?>
{
    public static new EDSType Type { get; } = EDSType.Dictionary;

    private readonly Dictionary<string, EDSObject> _entries;


    public bool IsCompact { get; private set; }

    public (string Key, EDSObject Value)[] Items => _entries.Select(kvp => (kvp.Key, kvp.Value)).ToArray();

    public ICollection<string> Keys => _entries.Keys;

    public ICollection<EDSObject> Values => _entries.Values;

    public int Count => _entries.Count;

    public bool IsReadOnly { get; }

    public EDSObject? this[string key]
    {
        set => Add(key, value);
        get => TryGetValue(key);
    }


    private EDSDictionary(bool ignore_case, bool compact)
    {
        IsCompact = compact;
        _entries = new(ignore_case ? StringComparer.InvariantCultureIgnoreCase : StringComparer.Ordinal);
    }

    public override string ToJSON() => $"{{{string.Join(",", Items.Select(t => $" {EDSString.FromString(t.Key).ToJSON()}: {t.Value.ToJSON()}"))} }}";

    public EDSObject? TryGetValue(string key) => TryGetValue(key, out EDSObject? value) ? value : null;

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out EDSObject? value) => _entries.TryGetValue(key, out value);

    public bool ContainsKey(string key) => _entries.ContainsKey(key);

    public bool Remove(string key) => _entries.Remove(key);

    public void Add(string key, EDSObject? value)
    {
        value ??= EDSNull.Null;

        _entries[key] = value;
    }

    public void Add(KeyValuePair<string, EDSObject?> item) => Add(item.Key, item.Value);

    public void Clear() => _entries.Clear();

    bool ICollection<KeyValuePair<string, EDSObject?>>.Contains(KeyValuePair<string, EDSObject?> item) => _entries.Contains(item);

    void ICollection<KeyValuePair<string, EDSObject?>>.CopyTo(KeyValuePair<string, EDSObject?>[] array, int arrayIndex) => throw new NotImplementedException();

    bool ICollection<KeyValuePair<string, EDSObject?>>.Remove(KeyValuePair<string, EDSObject?> item) => _entries.Remove(item.Key);

    public IEnumerator<KeyValuePair<string, EDSObject?>> GetEnumerator() => _entries.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _entries.GetEnumerator();

    public override void Write(Stream stream, SerializerOptions options)
    {
        (string key, EDSObject value)[] entries = _entries.Select(kvp => (kvp.Key, kvp.Value)).ToArray();
        bool compact = IsCompact || options.DictionaryStrategy == DictionaryStrategy.Compact;

        CompressedStringDictionary compressed = new(entries.Select(e => e.key), options.IgnoreCase);

        if (entries.Length <= 0b1111)
            stream.WriteByte((byte)((int)EDSDictionaryFlags.VALUE_Small | entries.Length));
        else if (compact)
        {
            stream.WriteByte((byte)EDSDictionaryFlags.LargeShortNamedDictionary);

            EDSInteger.FromInt32(entries.Length).Write(stream, options);
            EDSInteger.FromInt32(compressed.Prefixes.Length).Write(stream, options);

            foreach (string prefix in compressed.Prefixes)
                EDSString.FromString(prefix).Write(stream, options);
        }
        else
        {
            stream.WriteByte((byte)EDSDictionaryFlags.LargeFullNamedDictionary);

            EDSInteger.FromInt32(entries.Length).Write(stream, options);
        }

        foreach ((string key, EDSObject value) in entries)
        {
            EDSString.FromString(compact ? compressed.GetMapping(key) : key).Write(stream, options);
            value.Write(stream, options);
        }
    }

    public static new EDSDictionary? Read(Stream stream, byte first_byte, SerializerOptions options)
    {
        EDSDictionaryFlags dict_flag = (EDSDictionaryFlags)first_byte;
        EDSDictionary dict = CreateNew(options);

        if (dict_flag is EDSDictionaryFlags.Null)
            return null;

        int length;
        bool compact = false;
        CompressedStringDictionary? compressed = null;

        if ((dict_flag & EDSDictionaryFlags.MASK_SmallLarge) == EDSDictionaryFlags.VALUE_Small)
            length = (int)(dict_flag & ~EDSDictionaryFlags.MASK_SmallLarge);
        else
        {
            compact = dict_flag == EDSDictionaryFlags.LargeShortNamedDictionary;
            length = Read<EDSInteger>(stream, options)?.ToInt32() ?? 0;
        }

        if (compact)
        {
            dict.IsCompact = true;

            string[] prefixes = new string[Read<EDSInteger>(stream, options)?.ToInt32() ?? 0];

            for (int i = 0; i < prefixes.Length; ++i)
                prefixes[i] = Read<EDSString>(stream, options)?.ToString() ?? "";

            compressed = CompressedStringDictionary.FromPrefixes(prefixes);
        }

        for (int i = 0; i < length; ++i)
        {
            string name = Read<EDSString>(stream, options)?.ToString() ?? "";

            dict[compressed?.FromMapping(name) ?? name] = Read(stream, options);
        }

        return dict;
    }

    public static EDSDictionary CreateNew(SerializerOptions options) =>
        new(options.IgnoreCase, options.DictionaryStrategy == DictionaryStrategy.Compact);

    public static EDSDictionary? Cast(EDSObject @object)
    {
        switch (@object)
        {
            case null or EDSNull:
                return null;
            case EDSDictionary dictionary:
                return dictionary;
            case EDSArray array:
                {
                    EDSDictionary dictionary = new(false, false);

                    for (int i = 0, l = array.Length; i < l; ++i)
                        dictionary[i.ToString()] = array[i];

                    return dictionary;
                }
            default:
                return null;
        }
    }
}
