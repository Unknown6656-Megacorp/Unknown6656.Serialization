using System.Text;
using System.IO;
using System;

namespace Unknown6656.Serialization;


public record HexSerializationOptions
{
    public bool Colored { get; init; } = true;
    public bool PrintHeader { get; init; } = true;
    public int? MaxWidth { get; init; } = null;
    public bool UseUnicode { get; init; } = true;
}

public static class Visualization
{
    public static void HexDump(this byte[] data) => HexDump(data, Console.Out);

    public static void HexDump(this byte[] data, TextWriter writer) => HexDump(new ReadOnlySpan<byte>(data), writer);

    public static unsafe void HexDump(void* ptr, int length) => HexDump(ptr, length, Console.Out);

    public static unsafe void HexDump(void* ptr, int length, TextWriter writer) => HexDump(new ReadOnlySpan<byte>(ptr, length), writer);

    public static void HexDump(this ReadOnlySpan<byte> data) => HexDump(data, Console.Out);

    public static void HexDump(this ReadOnlySpan<byte> data, TextWriter writer, HexSerializationOptions? options = null)
    {
        if (data.Length == 0)
            return;

        ConsoleColor foreground = ConsoleColor.Gray;
        ConsoleColor background = ConsoleColor.Black;
        bool is_console = writer == Console.Out || writer == Console.Error;
        int? width = null;

        if (is_console)
        {
            foreground = Console.ForegroundColor;
            background = Console.BackgroundColor;
            width = Console.WindowWidth - 1;

            if (Console.CursorLeft > 0)
                Console.WriteLine();
        }

        if (options is null)
            options = new()
            {
                Colored = is_console,
                MaxWidth = width,
            };

        writer.WriteLine(HexDumpToString(data, options));

        if (is_console)
        {
            Console.ForegroundColor = foreground;
            Console.BackgroundColor = background;
        }
    }

    public static unsafe string HexDumpToString(this ReadOnlySpan<byte> data, HexSerializationOptions options)
    {
        if (data.Length == 0)
            return "";

        int width = (options.MaxWidth ?? 200) - 16;
        int horizontal_count = (width - 3) / 4;

        horizontal_count -= horizontal_count % 16;

        int vertical_count = (int)Math.Ceiling((float)data.Length / horizontal_count);
        int h_digits = (int)Math.Log(horizontal_count, 16);

        StringBuilder builder = new();
        byte b;

        if (options.Colored)
            builder.Append("\x1b[97m");

        if (options.PrintHeader)
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

                for (int j = 0; j < horizontal_count && i * horizontal_count + j < data.Length; ++j)
                {
                    b = ptr[i * horizontal_count + j];
                    cflag = *(int*)(ptr + i * horizontal_count + j / 4 * 4) != 0;

                    if (options.Colored)
                        builder.Append(b is 0 ? cflag ? "\x1b[97m" : "\x1b[90m" : "\x1b[33m");

                    builder.Append($"{b:x2} ");
                }

                if (options.Colored)
                    builder.Append("\x1b[97m");

                if (i == vertical_count - 1)
                    builder.Append(new string(' ', 3 * (horizontal_count * vertical_count - data.Length)));

                builder.Append(options.UseUnicode ? '│' : '|')
                       .Append(' ');

                for (int j = 0; j < horizontal_count && i * horizontal_count + j < data.Length; j++)
                {
                    byte @byte = ptr[i * horizontal_count + j];
                    bool ctrl = @byte < 0x20 || @byte >= 0x7f && @byte <= 0xa0;

                    if (ctrl && options.Colored)
                        builder.Append("\x1b[31m");

                    builder.Append(ctrl ? '.' : (char)@byte);

                    if (options.Colored)
                        builder.Append("\x1b[97m");
                }

                builder.AppendLine();
            }

        return builder.ToString();
    }
}
