using AmorLib.Utils;
using System.Text.Json;

namespace ARA;

internal static class ARAJson
{
    private static readonly JsonSerializerOptions _setting = JsonSerializerUtil.CreateDefaultSettings(true, true, false);

    public static T Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, _setting)!;
    }

    public static object Deserialize(Type type, string json)
    {
        return JsonSerializer.Deserialize(json, type, _setting)!;
    }

    public static string Serialize(object value, Type type)
    {
        return JsonSerializer.Serialize(value, type, _setting);
    }
}
