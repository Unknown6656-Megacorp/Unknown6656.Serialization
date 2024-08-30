using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Unknown6656.EDS;


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




internal static class UTIL
{

    public static unsafe void HexDump(this byte[] data) => HexDump(new Span<byte>(data));

    public static unsafe void HexDump(void* ptr, int length) => HexDump(new Span<byte>(ptr, length));

    public static unsafe void HexDump([NotNull] this Span<byte> data) => HexDump(data, Console.Out);

    public static unsafe void HexDump(this byte[] data, TextWriter writer) => HexDump(new Span<byte>(data), writer);

    public static unsafe void HexDump(void* ptr, int length, TextWriter writer) => HexDump(new Span<byte>(ptr, length), writer);

    public static unsafe void HexDump([NotNull] this Span<byte> data, TextWriter writer)
    {
        if (data.Length == 0)
            return;

        ConsoleColor fc = Console.ForegroundColor;
        ConsoleColor bc = Console.BackgroundColor;

        if (Console.CursorLeft > 0)
            Console.WriteLine();

        string str = HexDumpToString(data, Console.WindowWidth - 3, true);

        Console.WriteLine(str);
        Console.ForegroundColor = fc;
        Console.BackgroundColor = bc;
    }

    public static unsafe string HexDumpToString([NotNull] this Span<byte> data, int width, bool colored = true)
    {
        if (data.Length == 0)
            return "";

        width -= 16;

        StringBuilder builder = new();
        int horizontal_count = (width - 3) / 4;
        byte b;

        horizontal_count -= horizontal_count % 16;

        int h_digits = (int)Math.Log(horizontal_count, 16);
        int vertical_count = (int)Math.Ceiling((float)data.Length / horizontal_count);

        if (colored)
            builder.Append("\x1b[97m");

        builder.Append(data.Length)
               .Append(" bytes:");

        for (int i = h_digits; i >= 0; --i)
        {
            builder.Append('\n')
                   .Append(new string(' ', 8));

            for (int j = 0; j < horizontal_count; ++j)
                builder.Append($"  {(int)(j / Math.Pow(16, i)) % 16:x}");
        }

        builder.Append('\n');

        fixed (byte* ptr = data)
            for (int i = 0; i < vertical_count; i++)
            {
                builder.Append($"{i * horizontal_count:x8}:  ");

                bool cflag;

                for (int j = 0; (j < horizontal_count) && (i * horizontal_count + j < data.Length); ++j)
                {
                    b = ptr[i * horizontal_count + j];
                    cflag = *(int*)(ptr + (i * horizontal_count) + (j / 4) * 4) != 0;

                    if (colored)
                        builder.Append(b is 0 ? cflag ? "\x1b[97m" : "\x1b[90m" : "\x1b[33m");

                    builder.Append($"{b:x2} ");
                }

                if (colored)
                    builder.Append("\x1b[97m");

                if (i == vertical_count - 1)
                    builder.Append(new string(' ', 3 * (horizontal_count * vertical_count - data.Length)));

                builder.Append("| ");

                for (int j = 0; (j < horizontal_count) && (i * horizontal_count + j < data.Length); j++)
                {
                    byte @byte = ptr[i * horizontal_count + j];
                    bool ctrl = (@byte < 0x20) || ((@byte >= 0x7f) && (@byte <= 0xa0));

                    if (ctrl && colored)
                        builder.Append("\x1b[31m");

                    builder.Append(ctrl ? '.' : (char)@byte);

                    if (colored)
                        builder.Append("\x1b[97m");
                }

                builder.AppendLine();
            }

        return builder.ToString();
    }
}
