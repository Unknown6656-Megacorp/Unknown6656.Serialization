using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Unknown6656.IO;


public abstract class YAMLObject
    : DynamicObject
    , ICloneable
{
    public abstract override IEnumerable<string> GetDynamicMemberNames();
    public abstract override DynamicMetaObject GetMetaObject(Expression parameter);
    public abstract override bool TryDeleteIndex(DeleteIndexBinder binder, object[] indexes);
    public abstract override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object? result);
    public abstract override bool TryDeleteMember(DeleteMemberBinder binder);
    public abstract override bool TryGetMember(GetMemberBinder binder, out object? result);
    public abstract override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object? value);
    public abstract override bool TrySetMember(SetMemberBinder binder, object? value);
}

public sealed class YAMLValue
    : YAMLObject
{
    public object? Value { get; }
    public YAMLValueType Type { get; }


}

public sealed class YAMLArray
    : YAMLObject
    , IList<YAMLObject>
{
}

public class YAMLSection
    : YAMLObject
    , IDictionary<string, YAMLObject>
{
    private readonly Dictionary<string, YAMLObject> _dictionary = new();


    public YAMLObject this[string key]
    {
        get => _dictionary[key];
        set => _dictionary[key] = value;
    }

    public ICollection<string> Keys => _dictionary.Keys;

    public ICollection<YAMLObject> Values => _dictionary.Values;

    public int Count => _dictionary.Count;

    public bool IsReadOnly => false;


    public void Add(string key, YAMLObject value) => _dictionary.Add(key, value);

    public void Add(KeyValuePair<string, YAMLObject> item) => _dictionary.Add(item.Key, item.Value);

    public void Clear() => _dictionary.Clear();

    public bool Contains(KeyValuePair<string, YAMLObject> item) => _dictionary.Contains(item);

    public bool ContainsKey(string key) => _dictionary.ContainsKey(key);

    public bool Remove(string key) => _dictionary.Remove(key);

    public bool Remove(KeyValuePair<string, YAMLObject> item) => _dictionary.Remove(item.Key);

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out YAMLObject value) => _dictionary.TryGetValue(key, out value);

    void ICollection<KeyValuePair<string, YAMLObject>>.CopyTo(KeyValuePair<string, YAMLObject>[] array, int arrayIndex) => ((ICollection<KeyValuePair<string, YAMLObject>>)_dictionary).CopyTo(array, arrayIndex);

    IEnumerator<KeyValuePair<string, YAMLObject>> IEnumerable<KeyValuePair<string, YAMLObject>>.GetEnumerator() => ((IEnumerable<KeyValuePair<string, YAMLObject>>)_dictionary).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_dictionary).GetEnumerator();
}

public sealed class YAMLDocument
    : YAMLSection
{
}

public enum YAMLValueType
{
    String,
    Float,
    Integer,
    Boolean,
    Binary,
}
