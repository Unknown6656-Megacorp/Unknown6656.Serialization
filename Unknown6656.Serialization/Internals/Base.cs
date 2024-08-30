using System.Runtime.CompilerServices;
using System.ComponentModel;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace Unknown6656.EDS.Internals;


public enum EDSType
    : byte
{
    Null       = 0b_0000_0000,
    Boolean    = 0b_0000_0001,
    Integer    = 0b_0000_0010,
    [EditorBrowsable(EditorBrowsableState.Never), DebuggerBrowsable(DebuggerBrowsableState.Never)]
    Integer_N  = 0b_0000_0011,
    Float      = 0b_0000_0100,
    String     = 0b_0000_0101,
    Array      = 0b_0000_0110,
    Dictionary = 0b_0000_0111,
}

public abstract class EDSObject
{
    public static EDSNull Null { get; } = EDSNull.Null;


    public virtual bool IsNull => this is null or EDSNull or { Type: EDSType.Null };

    public EDSType Type { get; }


    public EDSObject()
    {
        Type = (EDSType)(GetType().GetProperty(nameof(EDSObject<EDSNull>.Type), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                                 ?.GetValue(null) ?? throw new InvalidProgramException());
    }

    public abstract void Write(Stream stream, SerializerOptions options);

    public abstract string ToJSON();

    public override string ToString() => ToJSON();

    public override bool Equals(object? other)
    {
        EDSObject obj = FromObject(other);

        if (IsNull && obj.IsNull)
            return true;
        else if (Type == obj.Type)
            return ToJSON() == obj.ToJSON(); // TODO : find better comparison.
        else
            return false;
    }

    public static EDSObject Read(Stream stream, SerializerOptions options) =>
        stream.ReadByte() is int first_byte and > 0 ? Read(stream, (byte)first_byte, options) : Null;

    public static EDSObject Read(Stream stream, byte first_byte, SerializerOptions options)
    {
        EDSObject? obj = (EDSType)(first_byte >> 5) switch
        {
            EDSType.Boolean => EDSBoolean.Read(stream, first_byte, options),
            EDSType.Integer or EDSType.Integer_N => EDSInteger.Read(stream, first_byte, options),
            EDSType.Float => EDSFloat.Read(stream, first_byte, options),
            EDSType.String => EDSString.Read(stream, first_byte, options),
            EDSType.Array => EDSArray.Read(stream, first_byte, options),
            EDSType.Dictionary => EDSDictionary.Read(stream, first_byte, options),
            EDSType.Null => null,
            _ => throw new Exception(),
        };

        return obj ?? Null;
    }

    public static T? Read<T>(Stream stream, SerializerOptions options) where T : EDSObject, EDSObject<T> =>
        stream.ReadByte() is int first_byte and > 0 ? Read<T>(stream, (byte)first_byte, options) : null;

    public static T? Read<T>(Stream stream, byte first_byte, SerializerOptions options)
        where T : EDSObject, EDSObject<T> => CastTo<T>(Read(stream, first_byte, options));

    public virtual object? ToObject(Type? type, SerializerOptions options)
    {
        if (this is null or EDSNull)
            return type?.IsValueType ?? false ? Activator.CreateInstance(type) : null;
        else if (type == typeof(EDSObject))
            return this;
        else if (type is null)
            switch (this)
            {
                case EDSNull:
                    return null;
                case EDSBoolean boolean:
                    return boolean.Value;
                case EDSInteger integer:
                    return (integer.Size, integer.IsNegative) switch
                    {
                        ( < sizeof(int), _) or
                        (sizeof(int), true) => integer.ToInt32(),
                        (sizeof(int), false) => integer.ToUInt32(),
                        ( < sizeof(long), _) or
                        (sizeof(long), true) => integer.ToInt64(),
                        (sizeof(long), false) => integer.ToUInt64(),
                        _ => integer.ToUInt128(),
                    };
                case EDSFloat @float:
                    return @float.ToFixed128() as object ?? @float.ToFloat64();
                case EDSString @string:
                    return @string.Encoding is { } ? @string.ToString() : @string.ToByteArray();
                case EDSArray array:
                    return array.Select(e => e.ToObject(null, options)).ToArray();
                case EDSDictionary dict:
                    return dict.ToDictionary(e => e.Key, e => e.Value);
            }
        else if (ToObject(null, options) is var result && result?.GetType() == type)
            return result;
        else if (type == typeof(bool?))
            return CastTo<EDSBoolean>(this)?.Value;
        else if (type == typeof(sbyte?))
            return CastTo<EDSInteger>(this)?.ToInt8();
        else if (type == typeof(byte?))
            return CastTo<EDSInteger>(this)?.ToUInt8();
        else if (type == typeof(short?))
            return CastTo<EDSInteger>(this)?.ToInt16();
        else if (type == typeof(ushort?))
            return CastTo<EDSInteger>(this)?.ToUInt16();
        else if (type == typeof(int?))
            return CastTo<EDSInteger>(this)?.ToInt32();
        else if (type == typeof(uint?))
            return CastTo<EDSInteger>(this)?.ToUInt32();
        else if (type == typeof(nint?))
            return CastTo<EDSInteger>(this)?.ToNInt();
        else if (type == typeof(nuint?))
            return CastTo<EDSInteger>(this)?.ToNUInt();
        else if (type == typeof(long?))
            return CastTo<EDSInteger>(this)?.ToInt64();
        else if (type == typeof(ulong?))
            return CastTo<EDSInteger>(this)?.ToUInt64();
        else if (type == typeof(Int128?))
            return CastTo<EDSInteger>(this)?.ToInt128();
        else if (type == typeof(UInt128?))
            return CastTo<EDSInteger>(this)?.ToUInt128();
        else if (type == typeof(Half?))
            return CastTo<EDSFloat>(this)?.ToFloat16();
        else if (type == typeof(float?))
            return CastTo<EDSFloat>(this)?.ToFloat32();
        else if (type == typeof(double?))
            return CastTo<EDSFloat>(this)?.ToFloat64();
        else if (type == typeof(decimal?))
            return CastTo<EDSFloat>(this)?.ToFixed128();
        else if (type == typeof(char?))
            return CastTo<EDSString>(this)?.ToChar();
        else if (type == typeof(Guid?))
            return CastTo<EDSString>(this)?.ToGUID();
        else if (type == typeof(string))
            return CastTo<EDSString>(this)?.ToString();

        // TODO : datetime
        // TODO : datetimeoffset
        // TODO : timespan





        else if (type.GetInterfaces().Contains(typeof(ITuple)))
        {
            object?[]? elements = CastTo<EDSArray>(this)?.Select(item => item.ToObject(null, options))?.ToArray();

            return CreateTuple(elements ?? Array.Empty<object?>(), type.IsValueType);
        }
        else if (type.IsValueType && !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)))
            return ToObject(typeof(Nullable<>).MakeGenericType(type), options) ?? Activator.CreateInstance(type);
        else if (type.IsArray)
        {
            Type? elem_type = type.GetElementType();
            Array? src_array = CastTo<EDSArray>(this)?.Select(item => item.ToObject(elem_type, options))?.ToArray();

            if (src_array != null && elem_type != null)
            {
                Array dst_array = Array.CreateInstance(elem_type, src_array.Length);
                int i = 0;

                foreach (object? item in src_array)
                    dst_array.SetValue(item, i++);

                src_array = dst_array;
            }

            return src_array;
        }








        // TODO : generic dictionary
        // TODO : generic ienumerable
        // TODO : dynamic / expandoobject
        // TODO : IDynamicMetaObjectProvider


        //else if (type == typeof(IEnumerable<char>))
        //    return FromObject(new string(value.ToArray()), typeof(string), options);
        //else if (type == typeof(IEnumerable<byte>))
        //    return EDSString.FromByteArray(value as byte[] ?? value.ToArray());


        //else if (type == typeof(IEnumerable))
        //    {
        //        EDSArray array = EDSArray.CreateNew();

        //        foreach (object? item in enumerable)
        //            array.Append(FromObject(item, null, options));

        //        return array;
        //    }




        if (type is { } && CastTo<EDSDictionary>(this) is { } dictionary)
            return DictionaryToNETObject(dictionary, type, options);
        else
            throw new NotImplementedException();
    }

    private static EDSObject FromObject(object? other) => FromObject(other, null, SerializerOptions.DefaultOptions);

    public static EDSObject FromObject(object? other, Type? type, SerializerOptions options)
    {
        if (type != null && other?.GetType() is { } current_type)
        {
            if (!type.IsAssignableFrom(current_type))
                other = Convert.ChangeType(other, type);
        }
        else
            type ??= other?.GetType() ?? typeof(object);

        switch (other)
        {
            case null:
                return Null;
            case EDSObject obj:
                return obj;
            case bool value:
                return EDSBoolean.FromBoolean(value);
            case sbyte value:
                return EDSInteger.FromInt8(value);
            case byte value:
                return EDSInteger.FromUInt8(value);
            case short value:
                return EDSInteger.FromInt16(value);
            case ushort value:
                return EDSInteger.FromUInt16(value);
            case int value:
                return EDSInteger.FromInt32(value);
            case uint value:
                return EDSInteger.FromUInt32(value);
            case nint value:
                return EDSInteger.FromNInt(value);
            case nuint value:
                return EDSInteger.FromNUInt(value);
            case long value:
                return EDSInteger.FromInt64(value);
            case ulong value:
                return EDSInteger.FromUInt64(value);
            case Int128 value:
                return EDSInteger.FromInt128(value);
            case UInt128 value:
                return EDSInteger.FromUInt128(value);
            case Half value:
                return EDSFloat.FromFloat16(value);
            case float value:
                return EDSFloat.FromFloat32(value);
            case double value:
                return EDSFloat.FromFloat64(value);
            case decimal value:
                return EDSFloat.FromFixed128(value);
            case char value:
                return EDSString.FromChar(value);
            case string value:
                return EDSString.FromString(value);
            case Guid value:
                return EDSString.FromGUID(value);

            // TODO : datetime
            // TODO : datetimeoffset
            // TODO : timespan
            // TODO : other struct-like types

            case IEnumerable<char> value:
                return FromObject(new string(value.ToArray()), typeof(string), options);
            case IEnumerable<byte> value:
                return EDSString.FromByteArray(value as byte[] ?? value.ToArray());
            case ITuple tuple:
                {
                    EDSArray array = EDSArray.CreateNew();

                    for (int i = 0; i < tuple.Length; i++)
                        array.Append(FromObject(tuple[i], null, options));

                    return array;
                }
            //case { } when type.GetInterfaces().Where(x => x.IsGenericType).ToArray() is Type[] interfaces:
            //    {
            //        if (interfaces.Any(x => x.GetGenericTypeDefinition() == typeof(<>)))
            //        {

            //        }


            //        break;
            //    }



            // TODO : dictionary
            // TODO : tuple
            // TODO : value tuple
            // TODO : dynamic / expandoobject
            // TODO : IDynamicMetaObjectProvider

            case IEnumerable enumerable:
                {
                    EDSArray array = EDSArray.CreateNew();

                    foreach (object? item in enumerable)
                        array.Append(FromObject(item, null, options));

                    return array;
                }

            // TODO : arbitrary data
        }

        return NETObjectToDictionary(other, options);
    }

    public static EDSObject FromJSON(string json) => FromJSON(json, new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    });

    public static EDSObject FromJSON(string json, JsonDocumentOptions options)
    {
        JsonDocument doc = JsonDocument.Parse(json, options);

        return FromJSON(doc.RootElement);
    }

    private static EDSObject FromJSON(JsonElement json)
    {
        switch (json.ValueKind)
        {
            case JsonValueKind.Object:
                {
                    SerializerOptions options = new()
                    {
                        IgnoreCase = true,
                        DictionaryStrategy = DictionaryStrategy.FullCompatibility
                    };
                    EDSDictionary dict = EDSDictionary.CreateNew(options);

                    foreach (JsonProperty item in json.EnumerateObject())
                        dict[item.Name] = FromJSON(item.Value);

                    return dict;
                }
            case JsonValueKind.Array:
                EDSArray arr = EDSArray.CreateNew();

                arr.Append(json.EnumerateArray().Select(FromJSON));

                return arr;
            case JsonValueKind.String:
                return EDSString.FromString(json.GetString() ?? json.ToString());
            case JsonValueKind.Number:
                if (json.TryGetDecimal(out decimal f128))
                    return EDSFloat.FromFixed128(f128);
                else if (json.TryGetDouble(out double f64))
                    return EDSFloat.FromFloat64(f64);
                else if (json.TryGetInt64(out long i64))
                    return EDSInteger.FromInt64(i64);
                else if (json.TryGetUInt64(out ulong u64))
                    return EDSInteger.FromUInt64(u64);
                else
                    return EDSInteger.Zero;
            case JsonValueKind.True:
                return EDSBoolean.True;
            case JsonValueKind.False:
                return EDSBoolean.False;
            case JsonValueKind.Undefined:
            case JsonValueKind.Null:
            default:
                return EDSNull.Null;
        }
    }

    public static T? CastTo<T>(EDSObject? @object)
        where T : EDSObject, EDSObject<T>
    {
        EDSType type = T.Type;

        @object ??= Null;

        if (@object is T t)
            return t;
        else if (@object.Type == type)
            return (T?)@object;
        else if (@object.IsNull)
            return null;
        else if (type is EDSType.Boolean)
            return CastTo<T>(EDSBoolean.Cast(@object));
        else if (type is EDSType.Integer or EDSType.Integer_N)
            return CastTo<T>(EDSInteger.Cast(@object));
        else if (type is EDSType.Float)
            return CastTo<T>(EDSFloat.Cast(@object));
        else if (type is EDSType.String)
            return CastTo<T>(EDSString.Cast(@object));
        else if (type is EDSType.Array)
            return CastTo<T>(EDSArray.Cast(@object));
        else if (type is EDSType.Dictionary)
            return CastTo<T>(EDSDictionary.Cast(@object));
        else
            return null;
    }

    private static ITuple? CreateTuple(object?[] arguments, bool value_tuple)
    {
        if (arguments.Length > 7)
            arguments = arguments.Take(7).Append(CreateTuple(arguments[7..], value_tuple)).ToArray();
        else if (arguments.Length == 0)
            return value_tuple ? ValueTuple.Create() : CreateTuple(new object?[] { null }, value_tuple);

        Type base_type = (arguments.Length, value_tuple) switch
        {
            (1, true) => typeof(ValueTuple<>),
            (1, false) => typeof(Tuple<>),
            (2, true) => typeof(ValueTuple<,>),
            (2, false) => typeof(Tuple<,>),
            (3, true) => typeof(ValueTuple<,,>),
            (3, false) => typeof(Tuple<,,>),
            (4, true) => typeof(ValueTuple<,,,>),
            (4, false) => typeof(Tuple<,,,>),
            (5, true) => typeof(ValueTuple<,,,,>),
            (5, false) => typeof(Tuple<,,,,>),
            (6, true) => typeof(ValueTuple<,,,,,>),
            (6, false) => typeof(Tuple<,,,,,>),
            (7, true) => typeof(ValueTuple<,,,,,,>),
            (7, false) => typeof(Tuple<,,,,,,>),
            (8, true) => typeof(ValueTuple<,,,,,,,>),
            (8, false) => typeof(Tuple<,,,,,,,>),
            _ => throw new ArgumentOutOfRangeException(nameof(arguments), $"Too many arguments for {(value_tuple ? "Value" : "")}Tuple<...>.")
        };
        Type[] argument_types = arguments.Select(x => x?.GetType() ?? typeof(object)).ToArray();
        Type generic_type = base_type.MakeGenericType(argument_types);
        ConstructorInfo? ctor = generic_type.GetConstructors().FirstOrDefault(ctor => ctor.GetParameters().Length == arguments.Length);
        ITuple? instance = ctor?.Invoke(arguments) as ITuple;

        return instance;
    }

    private static EDSDictionary NETObjectToDictionary(object @object, SerializerOptions options)
    {
        Type type = @object.GetType();
        Dictionary<string, MemberInfo> members = GetNETMembers(type, options);
        EDSDictionary dictionary = EDSDictionary.CreateNew(options);

        foreach ((string key, MemberInfo member) in members)
        {
            object? value = member switch
            {
                FieldInfo field => field.GetValue(@object),
                PropertyInfo prop => prop.GetValue(@object),
                _ => null
            };

            dictionary[key] = FromObject(value, null, options);
        }

        return dictionary;
    }

    private static int Distance(string first, string second)
    {
        first = first.ToLowerInvariant().Trim().Replace("-", "").Replace("_", "");
        second = second.ToLowerInvariant().Trim().Replace("-", "").Replace("_", "");

        int[,] matrix = new int[first.Length + 1, second.Length + 1];

        for (int i = 0; i <= first.Length; i++)
            matrix[i, 0] = i;

        for (int j = 0; j <= second.Length; j++)
            matrix[0, j] = j;

        for (int i = 1; i <= first.Length; i++)
            for (int j = 1; j <= second.Length; j++)
                if (first[i - 1] == second[j - 1])
                    matrix[i, j] = matrix[i - 1, j - 1];
                else
                    matrix[i, j] = Math.Min(Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1), matrix[i - 1, j - 1] + 1);

        return matrix[first.Length, second.Length];
    }

    private static object? DictionaryToNETObject(EDSDictionary dictionary, Type target_type, SerializerOptions options)
    {
        MemberInfo[] class_members = GetNETMembers(target_type, options).Values.ToArray();
        string[] eds_keys = dictionary.Keys.ToArray();
        MinimalPairFinder<string> pair_finder = new(Distance);

        Dictionary<string, string> eds_to_member = pair_finder.FindMinimalPairs(eds_keys, class_members.Select(m => m.Name));
        Dictionary<ConstructorInfo, (Dictionary<string, string> eds_to_param, int total_sum)> constructors = new();

        foreach (ConstructorInfo constructor in target_type.GetConstructors())
        {
            string[] parameters = constructor.GetParameters().Select(p => p.Name ?? "").ToArray();
            Dictionary<string, string> param_to_eds = pair_finder.FindMinimalPairs(parameters, eds_keys, out int sum);

            // prefer constructors with matching number of parameters
            sum += 3 * Math.Abs(parameters.Length - eds_keys.Length);
            constructors[constructor] = (param_to_eds, sum);
        }

        object? instance;

        if (constructors.Count > 0)
        {
            (ConstructorInfo constructor, (Dictionary<string, string> param_to_eds, _)) = constructors.MinBy(ctor => ctor.Value.total_sum);
            List<object?> arguments = new();

            foreach (ParameterInfo param in constructor.GetParameters())
                if (param_to_eds.TryGetValue(param.Name ?? "", out string? eds_key))
                {
                    arguments.Add(dictionary[eds_key]?.ToObject(param.ParameterType, options));
                    eds_to_member.Remove(eds_key);
                }
                else
                    arguments.Add(param.ParameterType.IsValueType ? Activator.CreateInstance(param.ParameterType) : null);

            instance = constructor.Invoke(arguments.ToArray());
        }
        else
            instance = Activator.CreateInstance(target_type);

        if (instance is { })
            foreach ((string eds_key, string member_name) in eds_to_member)
                switch (class_members.FirstOrDefault(m => m.Name == member_name))
                {
                    case FieldInfo { IsLiteral: false } field:
                        object? value = dictionary[eds_key]?.ToObject(field.FieldType, options);

                        field.SetValue(instance, value);

                        break;
                    case PropertyInfo { CanWrite: true } property:
                        value = dictionary[eds_key]?.ToObject(property.PropertyType, options);

                        property.SetValue(instance, value);

                        break;
                }

        return instance;
    }

    private static Dictionary<string, MemberInfo> GetNETMembers(Type type, SerializerOptions options)
    {
        Dictionary<string, MemberInfo> members = new(options.IgnoreCase ? StringComparer.InvariantCultureIgnoreCase : StringComparer.InvariantCulture);

        foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (member is MethodInfo or EventInfo or ConstructorInfo)
                continue;
            else if (member is FieldInfo && !options.IncludeFields)
                continue;

            (bool @readonly, bool visible) = member switch
            {
                PropertyInfo prop => (!prop.CanWrite, (prop.GetGetMethod() ?? prop.GetSetMethod())?.IsPublic ?? false),
                FieldInfo field => (field.IsInitOnly || field.IsLiteral, field.IsPublic),
                _ => (true, false),
            };

            if (!options.IncludeReadonlyMembers && @readonly)
                continue;
            else if (!options.IncludePrivateMembers && !visible)
                continue;

            members[member.Name] = member;
        }

        return members;
    }
}

public interface EDSObject<T>
    where T : EDSObject
            , EDSObject<T>
{
    public static abstract EDSType Type { get; }

    public static abstract T? Read(Stream stream, byte first_byte, SerializerOptions options);

    public static abstract T? Cast(EDSObject @object);
}

public sealed class EDSNull
    : EDSObject
    , EDSObject<EDSNull>
{
    public static new EDSNull Null { get; } = new();

    public static new EDSType Type { get; } = EDSType.Null;

    public override bool IsNull => true;


    private EDSNull()
    {
    }

    public override string ToJSON() => "null";

    public override void Write(Stream stream, SerializerOptions options) => stream.WriteByte(0);

    public static new EDSNull? Read(Stream stream, byte first_byte, SerializerOptions options)
    {
        if ((EDSType)(first_byte >> 5) != Type)
            return Read<EDSNull>(stream, first_byte, options);

        return first_byte <= 0 ? Null : null;
    }

    public static EDSNull? Cast(EDSObject @object) => @object.IsNull ? Null : null;
}
