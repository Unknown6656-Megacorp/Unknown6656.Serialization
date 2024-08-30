namespace Unknown6656.EDS.Internals;


enum EDSBooleanFlags
{
    Null = 0b_0000_0000,
    False = 0b_0010_0000,
    True = 0b_0010_0001,
}

public sealed class EDSBoolean
    : EDSObject
    , EDSObject<EDSBoolean>
{
    public static EDSBoolean True { get; } = new(true);
    public static EDSBoolean False { get; } = new(false);

    public static new EDSType Type { get; } = EDSType.Boolean;

    public bool Value { get; }


    private EDSBoolean(bool value) => Value = value;

    public override string ToJSON() => Value ? "true" : "false";

    public override void Write(Stream stream, SerializerOptions options) => EDSInteger.FromInt32(Value ? 1 : 0).Write(stream, options);

    public static new EDSBoolean? Read(Stream stream, byte first_byte, SerializerOptions options)
    {
        if ((EDSType)(first_byte >> 5) != Type)
            return Read<EDSBoolean>(stream, first_byte, options);

        return FromBoolean((EDSBooleanFlags)first_byte != EDSBooleanFlags.False);
    }

    public static EDSBoolean? Cast(EDSObject @object)
    {
        switch (@object)
        {
            case null or EDSNull:
                return null;
            case EDSBoolean boolean:
                return boolean;
            case EDSInteger integer:
                return FromBoolean(integer.ToInt128() != 0);
            case EDSFloat @float:
                if (@float.ToFloat64() is double d)
                    return FromBoolean(d != 0);
                else if (@float.ToFixed128() is decimal m)
                    return FromBoolean(m != 0);
                else
                    return null;
            case EDSString @string:
                switch (@string.ToString()?.Trim()?.ToLowerInvariant())
                {
                    case null or "null" or "undefined":
                        return null;
                    case "f" or "0" or "false" or "no" or "off":
                        return False;
                    case "t" or "1" or "true" or "yes" or "on":
                        return True;
                    case string str:
                        if (long.TryParse(str, out long l))
                            return FromBoolean(l != 0);
                        else if (double.TryParse(str, out double dd))
                            return FromBoolean(dd != 0);
                        else
                            return FromBoolean(str.Length > 0);
                }
            case EDSArray { Length: > 0 } array:
                return Cast(array[0]);
            default:
                return null;
        }
    }

    public static EDSBoolean FromBoolean(bool value) => value ? True : False;
}
