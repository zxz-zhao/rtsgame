using Godot;


public static class GodotDictionaryExtensions
{
    public static string GetString(this Godot.Collections.Dictionary data, string key, string fallback = "")
        => data.TryGetValue(key, out var value) ? value.AsString() : fallback;

    public static int GetInt(this Godot.Collections.Dictionary data, string key, int fallback = 0)
        => data.TryGetValue(key, out var value) ? value.AsInt32() : fallback;

    public static float GetFloat(this Godot.Collections.Dictionary data, string key, float fallback = 0f)
        => data.TryGetValue(key, out var value) ? (float)value.AsDouble() : fallback;

    public static bool GetBool(this Godot.Collections.Dictionary data, string key, bool fallback = false)
        => data.TryGetValue(key, out var value) ? value.AsBool() : fallback;

    public static long GetLong(this Godot.Collections.Dictionary data, string key, long fallback = 0L)
        => data.TryGetValue(key, out var value) ? value.AsInt64() : fallback;

    public static Godot.Collections.Array GetArray(this Godot.Collections.Dictionary data, string key)
        => data.TryGetValue(key, out var value) && value.VariantType == Variant.Type.Array
            ? value.AsGodotArray()
            : new Godot.Collections.Array();

    public static Godot.Collections.Dictionary GetDictionary(this Godot.Collections.Dictionary data, string key)
        => data.TryGetValue(key, out var value) && value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new Godot.Collections.Dictionary();
}
