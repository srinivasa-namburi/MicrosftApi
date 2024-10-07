namespace Microsoft.Greenlight.Shared.Extensions;

public static class LinqExtensions
{
    public static IEnumerable<T> Flatten<T>(this T source, Func<T, IEnumerable<T>> selector)
    {
        return selector(source).SelectMany(c => Flatten(c, selector))
            .Concat(new[] { source });
    }

    public static IEnumerable<T> Flatten<T>(this IEnumerable<T> source, Func<T, IEnumerable<T>> selector)
    {
        return source.SelectMany(x => Flatten(x, selector))
            .Concat(source);
    }
}
