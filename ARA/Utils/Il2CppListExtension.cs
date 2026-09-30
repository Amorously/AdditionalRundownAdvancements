namespace ARA.Utils;

public static class Il2CppListExtension
{
    public static Il2CppSystem.Collections.Generic.List<TResult> ToIl2Cpp<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector)
    {
        var result = new Il2CppSystem.Collections.Generic.List<TResult>();
        foreach (var item in source)
        {
            result.Add(selector(item));
        }
        return result;
    }
}
