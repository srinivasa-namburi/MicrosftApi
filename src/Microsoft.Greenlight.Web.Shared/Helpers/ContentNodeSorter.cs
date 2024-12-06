using System.Text.RegularExpressions;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Web.Shared.Helpers;

public static class ContentNodeSorter
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

        // Extract and compare hierarchical numbers (e.g., 2.1.1)
        var xParts = Regex.Matches(x.Text, @"\d+").Cast<Match>().Select(m => int.Parse(m.Value)).ToArray();
        var yParts = Regex.Matches(y.Text, @"\d+").Cast<Match>().Select(m => int.Parse(m.Value)).ToArray();

        int minLength = Math.Min(xParts.Length, yParts.Length);
        for (int i = 0; i < minLength; i++)
        {
            if (xParts[i] != yParts[i])
                return xParts[i].CompareTo(yParts[i]);
        }

        // If one title is a subsection of the other, the shorter (parent) comes first
        if (xParts.Length != yParts.Length)
            return xParts.Length.CompareTo(yParts.Length);

        // If numeric comparison is inconclusive or not applicable, fall back to string comparison
        return String.Compare(x.Text, y.Text, StringComparison.Ordinal);
    }
}
