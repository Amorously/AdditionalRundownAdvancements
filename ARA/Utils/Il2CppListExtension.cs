namespace ARA.Utils;

public static class Il2CppListExtension
{
    public static Il2CppSystem.Collections.Generic.List<T2> ToIl2Cpp<T1, T2>(this IEnumerable<T1> source, Func<T1, T2> selector)
    {
        var result = new Il2CppSystem.Collections.Generic.List<T2>();
        foreach (var item in source)
        {
            result.Add(selector(item));
        }
        return result;
    }
}
