namespace Microsoft.Greenlight.Shared.Extensions;

/// <summary>
/// Provides extension methods for LINQ operations.
/// </summary>
public static class LinqExtensions
{
    /// <summary>
    /// Flattens a hierarchical structure into a single sequence.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <param name="source">The root element of the hierarchy.</param>
    /// <param name="selector">A function to select the children of an element.</param>
    /// <returns>A flattened sequence of elements.</returns>
    public static IEnumerable<T> Flatten<T>(this T source, Func<T, IEnumerable<T>> selector)
    {
        return selector(source).SelectMany(c => Flatten(c, selector))
            .Concat([source]);
    }

    /// <summary>
    /// Flattens a hierarchical structure into a single sequence.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <param name="source">The sequence of root elements of the hierarchy.</param>
    /// <param name="selector">A function to select the children of an element.</param>
    /// <returns>A flattened sequence of elements.</returns>
    public static IEnumerable<T> Flatten<T>(this IEnumerable<T> source, Func<T, IEnumerable<T>> selector)
    {
        return source.SelectMany(x => Flatten(x, selector))
            .Concat(source);
    }
}
