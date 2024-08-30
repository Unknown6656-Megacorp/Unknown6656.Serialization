using System.Collections;

namespace Unknown6656.EDS.Internals;


internal enum EDSArrayFlags
    : byte
{
    Null = 0b_0000_0000,

    MASK_ArrayType = 0b_1111_0000,

    VALUE_ShortArray = 0b_1100_0000,
    VALUE_LongArray = 0b_1101_0000,

    MASK_ArrayLength = 0b_0000_1111,
}

public sealed class EDSArray
    : EDSObject
    , EDSObject<EDSArray>
    , IEnumerable<EDSObject>
{
    public static new EDSType Type { get; } = EDSType.Array;


    private readonly List<EDSObject> _items;


    public int Length => _items.Count;

    public EDSObject this[int index]
    {
        set => _items[index] = value ?? EDSNull.Null;
        get => _items[index];
    }


    private EDSArray() => _items = new();

    public override string ToJSON() => $"[{string.Join(", ", _items)}]";

    public void Append(EDSObject? value) => _items.Add(value ?? EDSNull.Null);

    public void Append(IEnumerable<EDSObject?> values)
    {
        foreach (EDSObject? item in values)
            Append(item);
    }

    public void Insert(int index, EDSObject? value) => _items.Insert(index, value ?? EDSNull.Null);

    public void RemoveAt(int index) => _items.RemoveAt(index);

    public EDSObject[] ToArray() => _items.ToArray();

    public List<EDSObject> ToList() => _items.ToList(); // clone

    public override void Write(Stream stream, SerializerOptions options)
    {
        EDSObject[] items = _items.ToArray(); // clone

        if (items.Length <= (int)EDSArrayFlags.MASK_ArrayLength)
            stream.WriteByte((byte)(((EDSArrayFlags)items.Length & EDSArrayFlags.MASK_ArrayLength) | EDSArrayFlags.VALUE_ShortArray));
        else
        {
            stream.WriteByte((byte)EDSArrayFlags.VALUE_LongArray);

            EDSInteger.FromInt32(items.Length).Write(stream, options);
        }

        foreach (EDSObject item in items)
            item.Write(stream, options);
    }

    public static new EDSArray? Read(Stream stream, byte first_byte, SerializerOptions options)
    {
        EDSArrayFlags flags = (EDSArrayFlags)first_byte;
        EDSArray array = CreateNew();
        UInt128 length;

        if (flags == EDSArrayFlags.Null)
            return null;
        else if ((flags & EDSArrayFlags.MASK_ArrayType) == EDSArrayFlags.VALUE_ShortArray)
            length = (UInt128)(int)(flags & EDSArrayFlags.MASK_ArrayLength);
        else
            length = Read<EDSInteger>(stream, options)?.ToUInt128() ?? 0;

        while (length --> 0)
            array.Append(Read(stream, options));

        return array;
    }

    public static EDSArray? Cast(EDSObject @object)
    {
        switch (@object)
        {
            case null or EDSNull:
                return null;
            case EDSArray array:
                return array;
            default:
                {
                    EDSArray array = CreateNew();

                    array.Append(@object);

                    return array;
                }
        }
    }

    public static EDSArray CreateNew() => new();

    public IEnumerator<EDSObject> GetEnumerator() => ((IEnumerable<EDSObject>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_items).GetEnumerator();
}
