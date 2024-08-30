using System.Text;
using System.Text.Json;

using Unknown6656.EDS.Internals;
using Unknown6656.EDS;





var data_in = new
{
    A = 42,
    B = "Hello World!",
    C = new
    {
        D = -3.14,
        E = new
        {
            F = -1.618,
            G = new[] { true, false },
            Hello_this_is_a_very_long_text_\u01ff = Guid.NewGuid(),
        }
    },
    @\u00FF_Юлия = Guid.NewGuid(),
    PI = Math.PI,
};





Console.WriteLine("\x1b[92m======================================================= DATA IN =======================================================\x1b[0m");
Console.WriteLine(data_in);

var json = JsonSerializer.Serialize(data_in);

Console.WriteLine("\x1b[92m======================================================= JSON IN =======================================================\x1b[0m");
Console.WriteLine(json);


using MemoryStream ms = new();

Serializer.Serialize(data_in, ms);

byte[] bytes = ms.ToArray();
ms.Seek(0, SeekOrigin.Begin);

Console.WriteLine("\x1b[92m====================================================== EDS STREAM =====================================================\x1b[0m");
UTIL.HexDump(bytes);
//Console.WriteLine(string.Concat(bytes.Select(b => $" {Convert.ToString(b >> 4, 2).PadLeft(4, '0')}.{Convert.ToString(b & 0xf, 2).PadLeft(4, '0')}")));

Console.WriteLine("\x1b[92m===================================================== JSON STREAM =====================================================\x1b[0m");
UTIL.HexDump(Encoding.UTF8.GetBytes(json));

Console.WriteLine("\x1b[92m===================================================== JSON GZIPPED =====================================================\x1b[0m");
UTIL.HexDump(EDSString.GZIPCompressData(Encoding.UTF8.GetBytes(json)));

var des = Serializer.Deserialize(ms, (data_in as object)?.GetType());

Console.WriteLine("\x1b[92m======================================================= DATA OUT =======================================================\x1b[0m");
Console.WriteLine(des);
Console.WriteLine("\x1b[92m======================================================= JSON OUT =======================================================\x1b[0m");
Console.WriteLine(JsonSerializer.Serialize(des));
