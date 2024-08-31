using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.IO;
using System;

using Unknown6656.Generics;
using Unknown6656.Common;

namespace Unknown6656.IO;


public sealed class INIFile
    : DynamicObject
    , IDictionary<string, INISection>
    , ICloneable
{
    private static readonly Regex INI_REGEX_SECTION = new(@"^\s*\[\s*(?<sec>[\w\-]+)\s*\]", RegexOptions.Compiled);
    private static readonly Regex INI_REGEX_PROPERTY = new(@"^\s*(?<prop>[\w\-]+)\s*\=\s*(?<val>.*)\s*$", RegexOptions.Compiled);

    private readonly Dictionary<string, INISection> _sections;
    private readonly bool _case_insensitive;


    int ICollection<KeyValuePair<string, INISection>>.Count => SectionCount;

    bool ICollection<KeyValuePair<string, INISection>>.IsReadOnly => false;

    ICollection<string> IDictionary<string, INISection>.Keys => _sections.Keys;

    ICollection<INISection> IDictionary<string, INISection>.Values => _sections.Values;

    public string[] SectionKeys => _sections.Keys.ToArray();

    public int SectionCount => _sections.Count;

    public bool IsEmpty => SectionCount == 0;

    public bool HasDefaultSection => HasSection(string.Empty);

    public INISection? DefaultSection
    {
        get => TryGetSection(string.Empty, out INISection? section) ? section : null;
        set
        {
            if (value is null)
                TryDeleteSection(string.Empty);
            else
                SetOrOverwrite(string.Empty, value);
        }
    }

    public INISection this[string section]
    {
        get => GetOrAddSection(section);
        set => SetOrOverwrite(section, value);
    }

    public string this[string section, string key]
    {
        get => this[section][key];
        set => this[section][key] = value;
    }


    public INIFile()
        : this(false)
    {
    }

    public INIFile(bool case_insensitive)
    {
        StringComparison comp = _case_insensitive ? StringComparison.InvariantCultureIgnoreCase : StringComparison.Ordinal;

        _sections = new(new CustomEqualityComparer<string>((s1, s2) => string.Equals(s1, s2, comp)));
        _case_insensitive = case_insensitive;
    }

    public INIFile(INISection default_section)
        : this(default_section, false)
    {
    }

    public INIFile(INISection default_section, bool case_insensitive) : this(case_insensitive) => DefaultSection = default_section;

    public void Add(string key, INISection value) => SetOrOverwrite(key, value);

    public string ToJSONString(JsonSerializerOptions? options = null) => JsonSerializer.Serialize(ToExpandoObject(), options);

    public override string ToString() => _sections.SelectWhere(kvp => !kvp.Value.IsEmpty, kvp => $"[{kvp.Key}]{Environment.NewLine}{kvp.Value}").StringJoin(Environment.NewLine + Environment.NewLine);

    public string Serialize() => ToString();

    public void SaveTo(string path) => DataStream.FromINI(this).ToFile(path);

    public void SaveTo(FileInfo file) => DataStream.FromINI(this).ToFile(file);

    public bool HasSection(string key) => _sections.ContainsKey(key);

    bool ICollection<KeyValuePair<string, INISection>>.Contains(KeyValuePair<string, INISection> item) => _sections.Contains(item);

    bool IDictionary<string, INISection>.ContainsKey(string key) => HasSection(key);

    public INISection CreateSection(string key) => SetOrOverwrite(key, new INISection(_case_insensitive));

    public bool TryGetSection(string key, [NotNullWhen(true)] out INISection? section)
    {
        if (!_sections.TryGetValue(key, out section))
            section = CreateSection(key);

        return section is { };
    }

    public INISection GetOrAddSection(string key)
    {
        if (TryGetSection(key, out INISection? section))
            return section;
        else
            return CreateSection(key);
    }

    public INISection SetOrOverwrite(string key, INISection section) => _sections[key] = section;

    public void Clear() => _sections.Clear();

    public bool TryDeleteSection(string key) => _sections.Remove(key);

    public INIFile Clone()
    {
        INIFile cloned = new(_case_insensitive);

        foreach (KeyValuePair<string, INISection> kvp in this)
            cloned.Add(kvp.Key, kvp.Value.Clone());

        return cloned;
    }

    #region DYNAMIC

    public ExpandoObject ToExpandoObject()
    {
        ExpandoObject expando = new();
        IDictionary<string, object?> dic = expando;

        foreach (KeyValuePair<string, INISection> kvp in this)
            dic[kvp.Key] = kvp.Value.ToExpandoObject();

        return expando;
    }

    private bool DynDelete(object? key)
    {
        TryDeleteSection(key?.ToString() ?? "");

        return true;
    }

    private bool DynGet(object? key, out object? value)
    {
        TryGetSection(key?.ToString() ?? "", out INISection? val);

        value = val;

        return true;
    }

    private bool DynSet(object? key, object? value)
    {
        if (value is INISection sec)
        {
            SetOrOverwrite(key?.ToString() ?? "", sec);

            return true;
        }
        else
            return false;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override IEnumerable<string> GetDynamicMemberNames() => SectionKeys;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool TryDeleteIndex(DeleteIndexBinder binder, object[] indexes) => DynDelete(indexes?.FirstOrDefault());

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool TryDeleteMember(DeleteMemberBinder binder) => DynDelete(binder.Name);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object? result) => DynGet(indexes?.FirstOrDefault(), out result);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool TryGetMember(GetMemberBinder binder, out object? result) => DynGet(binder.Name, out result);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object? value) => DynSet(indexes?.FirstOrDefault(), value);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool TrySetMember(SetMemberBinder binder, object? value) => DynSet(binder.Name, value);

    #endregion
    #region EXPLICITS

    object ICloneable.Clone() => Clone();

    void ICollection<KeyValuePair<string, INISection>>.Add(KeyValuePair<string, INISection> item) => SetOrOverwrite(item.Key, item.Value);

    bool IDictionary<string, INISection>.Remove(string key) => TryDeleteSection(key);

#pragma warning disable CS8769 // Nullability of reference types in type of parameter doesn't match implemented member (possibly because of nullability attributes).
    bool IDictionary<string, INISection>.TryGetValue(string key, out INISection? value) => TryGetSection(key, out value);
#pragma warning restore CS8769

    bool ICollection<KeyValuePair<string, INISection>>.Remove(KeyValuePair<string, INISection> item) => _sections.Contains(item) && _sections.Remove(item.Key);

    void ICollection<KeyValuePair<string, INISection>>.CopyTo(KeyValuePair<string, INISection>[] array, int arrayIndex) => Array.Copy(_sections.ToArray(), 0, array, arrayIndex, SectionCount);

    IEnumerator<KeyValuePair<string, INISection>> IEnumerable<KeyValuePair<string, INISection>>.GetEnumerator() => ((IEnumerable<KeyValuePair<string, INISection>>)_sections).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_sections).GetEnumerator();

    #endregion
    #region STATICS

    public static INIFile FromURI(Uri uri) => DataStream.FromWebResource(uri).ToINI();

    public static INIFile FromURI(string uri) => DataStream.FromWebResource(uri).ToINI();

    public static INIFile FromFile(string path) => DataStream.FromFile(path).ToINI();

    public static INIFile FromFile(FileInfo file) => DataStream.FromFile(file).ToINI();

    public static INIFile FromINIString(string ini_string) => FromINIString(ini_string, false);

    public static INIFile FromINIString(string ini_string, bool case_insensitive)
    {
        INIFile ini = new(case_insensitive);
        INISection? section = null;

        foreach (string line in ini_string.SplitIntoLines())
        {
            string ln = (line.Contains('#') ? line[..line.LastIndexOf('#')] : line).Trim();

            if (ln.Match(INI_REGEX_SECTION, out Match m))
                section = ini.CreateSection(m.Groups["sec"].ToString());
            else if (ln.Match(INI_REGEX_PROPERTY, out m))
            {
                section ??= ini.CreateSection(string.Empty);
                section[m.Groups["prop"].ToString()] = m.Groups["val"].ToString();
            }
        }

        foreach (string key in ini.SectionKeys)
            if (ini[key].IsEmpty)
                ini.TryDeleteSection(key);

        return ini;
    }

    #endregion
}

public sealed class INISection
    : DynamicObject
    , IDictionary<string, string>
    , ICloneable
{
    private readonly Dictionary<string, string> _dictionary;
    private readonly bool _case_insensitive;


    public static INISection Empty => [];

    public int Count => _dictionary.Count;

    bool ICollection<KeyValuePair<string, string>>.IsReadOnly => false;

    ICollection<string> IDictionary<string, string>.Keys => Keys;

    ICollection<string> IDictionary<string, string>.Values => Values;

    public string[] Keys => _dictionary.Keys.ToArray();

    public string[] Values => _dictionary.Values.ToArray();

    public bool IsEmpty => Count == 0;

    public string this[string key]
    {
        get => GetOrAddKey(key);
        set => SetOrOverwrite(key, value);
    }


    public INISection()
        : this(false)
    {
    }

    public INISection(bool case_insensitive)
    {
        StringComparison comp = _case_insensitive ? StringComparison.InvariantCultureIgnoreCase : StringComparison.Ordinal;

        _dictionary = new(new CustomEqualityComparer<string>((s1, s2) => string.Equals(s1, s2, comp)));
        _case_insensitive = case_insensitive;
    }

    public string ToJSONString(JsonSerializerOptions? options = null) => JsonSerializer.Serialize(ToExpandoObject(), options);

    public override string ToString() => _dictionary.Select(kvp => $"{kvp.Key}={kvp.Value}").StringJoin(Environment.NewLine);

    public string Serialize() => ToString();

    public void Add(string key, string value) => SetOrOverwrite(key, value);

    public void Add(KeyValuePair<string, string> item) => SetOrOverwrite(item.Key, item.Value);

    public bool HasSection(string key) => _dictionary.ContainsKey(key);

    public bool Contains(KeyValuePair<string, string> item) => _dictionary.Contains(item);

    public bool ContainsKey(string key) => HasSection(key);

    public bool TryGetValue(string key, [NotNullWhen(true)] out string? value) => _dictionary.TryGetValue(key, out value);

    public string GetOrAddKey(string key) => TryGetValue(key, out string? section) ? section : SetOrOverwrite(key, string.Empty);

    public string SetOrOverwrite(string key, string value) => _dictionary[key] = value;

    public void Clear() => _dictionary.Clear();

    public bool Remove(string key) => _dictionary.Remove(key);

    public bool Remove(KeyValuePair<string, string> item) => _dictionary.Contains(item) && _dictionary.Remove(item.Key);

    public INISection Clone()
    {
        INISection cloned = new(_case_insensitive);

        foreach (KeyValuePair<string, string> kvp in this)
            cloned.Add(kvp);

        return cloned;
    }

    object ICloneable.Clone() => Clone();

    void ICollection<KeyValuePair<string, string>>.CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) => Array.Copy(_dictionary.ToArray(), 0, array, arrayIndex, Count);

    IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator() => ((IEnumerable<KeyValuePair<string, string>>)_dictionary).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_dictionary).GetEnumerator();


    #region DYNAMIC

    public ExpandoObject ToExpandoObject()
    {
        ExpandoObject expando = new();
        IDictionary<string, object?> dic = expando;

        foreach (KeyValuePair<string, string> kvp in this)
            dic[kvp.Key] = kvp.Value;

        return expando;
    }

    private bool DynDelete(object? key)
    {
        Remove(key?.ToString() ?? "");

        return true;
    }

    private bool DynGet(object? key, out object? value)
    {
        TryGetValue(key?.ToString() ?? "", out string? val);

        value = val;

        return true;
    }

    private bool DynSet(object? key, object? value)
    {
        SetOrOverwrite(key?.ToString() ?? "", value?.ToString() ?? "");

        return true;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override IEnumerable<string> GetDynamicMemberNames() => Keys;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool TryDeleteIndex(DeleteIndexBinder binder, object[] indexes) => DynDelete(indexes?.FirstOrDefault());

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool TryDeleteMember(DeleteMemberBinder binder) => DynDelete(binder.Name);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object? result) => DynGet(indexes?.FirstOrDefault(), out result);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool TryGetMember(GetMemberBinder binder, out object? result) => DynGet(binder.Name, out result);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object? value) => DynSet(indexes?.FirstOrDefault(), value);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool TrySetMember(SetMemberBinder binder, object? value) => DynSet(binder.Name, value);

    #endregion
}
