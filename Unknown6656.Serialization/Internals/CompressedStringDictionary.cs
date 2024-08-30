namespace Unknown6656.EDS.Internals;


internal sealed class CompressedStringDictionary
{
    private const char MAP_START = '\xe000';
    private const char MAP_END = '\xf8ff';

    public bool IgnoreCase { get; }
    public string[] SourceStrings { get; }
    public string[] Prefixes { get; }
    public string[] Mapping { get; }


    private CompressedStringDictionary(IEnumerable<string> prefixes)
    {
        IgnoreCase = false;
        Prefixes = prefixes.ToArray();
        SourceStrings = Array.Empty<string>();
        Mapping = Array.Empty<string>();
    }

    public CompressedStringDictionary(IEnumerable<string> strings, bool ignore_case)
    {
        IgnoreCase = ignore_case;
        SourceStrings = strings.ToArray();
        (Prefixes, Mapping) = BuildMapping();
    }

    private (string[] prefixes, string[] mapping) BuildMapping()
    {
        string[] mapping = new string[SourceStrings.Length];
        Dictionary<string, HashSet<int>> prefix_map = new();

        for (int i = 0; i < mapping.Length; ++i)
        {
            foreach (char c in SourceStrings[i])
                if (c >= MAP_START && c <= MAP_END)
                    throw new NotSupportedException($"The string '{SourceStrings[i]}' contains the character 0x{(int)c:x4} which is reserved for internal usage.");

            for (int j = i + 1; j < mapping.Length; ++j)
                if (CommonPrefix(SourceStrings[i], SourceStrings[j]) is { Length: > 1 } prefix)
                {
                    if (!prefix_map.TryGetValue(prefix, out HashSet<int>? indices))
                        prefix_map[prefix] = indices = new() { i };

                    indices.Add(j);
                }
        }

        string[] prefixes = prefix_map.Keys.OrderByDescending(p => p.Length).ToArray();

        if (prefixes.Length > MAP_END - MAP_START)
            throw new NotSupportedException($"The dictionary contains more than {MAP_END - MAP_START} unique prefixes and can therefore not be efficiently compressed");

        for (int i = 0; i < mapping.Length; ++i)
        {
            string s = SourceStrings[i];

            for (int j = 0; j < prefixes.Length; ++j)
                if (prefix_map[prefixes[j]].Contains(i))
                {
                    s = (char)(MAP_START + j) + s[prefixes[j].Length..];

                    break;
                }

            mapping[i] = s;
        }

        return (prefixes, mapping);
    }

    public string GetMapping(string source)
    {
        for (int i = 0; i < SourceStrings.Length; ++i)
            if (SourceStrings[i].Equals(source, IgnoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture))
                return Mapping[i];

        throw new KeyNotFoundException();
    }

    public string FromMapping(string mapping) =>
        mapping.Length > 0 && mapping[0] is char pfx and >= MAP_START and <= MAP_END ? Prefixes[pfx - MAP_START] + mapping[1..] : mapping;

    private string CommonPrefix(string first, string second)
    {
        int length = 0;

        foreach (char c in first)
            if (second.Length <= length || (IgnoreCase ? char.ToLowerInvariant(second[length]) != char.ToLowerInvariant(c) : second[length] != c))
                return first[..length];
            else
                ++length;

        return first;
    }

    internal static CompressedStringDictionary FromPrefixes(IEnumerable<string> prefixes) => new(prefixes);
}
