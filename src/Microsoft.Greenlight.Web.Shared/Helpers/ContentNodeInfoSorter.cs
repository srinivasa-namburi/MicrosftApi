using System.Text.RegularExpressions;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Web.Shared.Helpers;

public static class ContentNodeInfoSorter
{
    public static void SortContentNodes(List<ContentNodeInfo> nodes)
    {
        // Prioritize BodyText nodes and sort the nodes list itself
        nodes.Sort(CompareContentNodes);

        // Recursively sort the children of each node
        foreach (var node in nodes)
        {
            if (node.Children.Any())
            {
                SortContentNodes(node.Children);
            }
        }
    }

    private static int CompareContentNodes(ContentNodeInfo x, ContentNodeInfo y)
{
    // Priority to BodyText nodes to bubble them up
    if (x.Type == ContentNodeType.BodyText && y.Type != ContentNodeType.BodyText) return -1;
    if (y.Type == ContentNodeType.BodyText && x.Type != ContentNodeType.BodyText) return 1;

    // Only extract numeric parts for Title or Heading nodes to avoid processing lengthy body text
    if (x.Type != ContentNodeType.Title && x.Type != ContentNodeType.Heading &&
        y.Type != ContentNodeType.Title && y.Type != ContentNodeType.Heading)
    {
        // Fall back to simple string comparison for non-title nodes
        return string.Compare(x.Text, y.Text, StringComparison.Ordinal);
    }

    try
    {
        // Extract and compare hierarchical numbers (e.g., 2.1.1)
        var xParts = Regex.Matches(x.Text, @"\b\d+\b")
            .Cast<Match>()
            .Select(m =>
            {
                // Safely parse to long and constrain to int range for comparison
                if (long.TryParse(m.Value, out var num))
                    return num <= int.MaxValue ? (int)num : int.MaxValue;
                return 0;
            })
            .ToArray();

        var yParts = Regex.Matches(y.Text, @"\b\d+\b")
            .Cast<Match>()
            .Select(m =>
            {
                if (long.TryParse(m.Value, out var num))
                    return num <= int.MaxValue ? (int)num : int.MaxValue;
                return 0;
            })
            .ToArray();

        // If no numbers found in either node, fall back to string comparison
        if (xParts.Length == 0 && yParts.Length == 0)
            return string.Compare(x.Text, y.Text, StringComparison.Ordinal);

        int minLength = Math.Min(xParts.Length, yParts.Length);
        for (int i = 0; i < minLength; i++)
        {
            if (xParts[i] != yParts[i])
                return xParts[i].CompareTo(yParts[i]);
        }

        // If one title is a subsection of the other, the shorter (parent) comes first
        if (xParts.Length != yParts.Length)
            return xParts.Length.CompareTo(yParts.Length);
    }
    catch (Exception)
    {
        // If any exception occurs during number parsing/comparison,
        // fall back to simple string comparison
    }

    // If numeric comparison is inconclusive or not applicable, fall back to string comparison
    return string.Compare(x.Text, y.Text, StringComparison.Ordinal);
}
}



