namespace Unknown6656.Serialization.EDS;


public enum Endianess
    : byte
{
    LittleEndian,
    BigEndian
}

public enum DictionaryStrategy
    : byte
{
    FullCompatibility,
    Compact,
}

// TODO : guid handling
// TODO : datetime handling

public record class SerializerOptions
{
    public DictionaryStrategy DictionaryStrategy { get; init; }
    public Endianess ByteEndianess { get; init; }
    public bool EnforceDataOrder { get; init; }
    public bool IncludeFields { get; init; }
    public bool IgnoreCase { get; init; }
    public bool IncludeReadonlyMembers { get; init; }
    public bool IncludePrivateMembers { get; init; }
    //public Encoding StringEncoding { get; init; }


    public static SerializerOptions DefaultOptions { get; } = new()
    {
        DictionaryStrategy = DictionaryStrategy.FullCompatibility,
        ByteEndianess = Endianess.LittleEndian,
        EnforceDataOrder = false,
        IgnoreCase = true,
        IncludeFields = true,
        IncludeReadonlyMembers = true,
        IncludePrivateMembers = false,
        //StringEncoding = Encoding.UTF8,
    };
}
