using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared
{
    /// <summary>
    /// Provides extension methods for the ContentNode class.
    /// </summary>
    public static class ContentNodeExtensions
    {
        /// <summary>
        /// Removes reserved words from the heading of the specified ContentNode.
        /// </summary>
        /// <param name="contentNode">The ContentNode whose heading will be modified.</param>
        public static void RemoveReservedWordsFromHeading(this ContentNode contentNode)
        {
            var removedWordsCollection = new List<string> { "chapter", "section", "appendix", "table" };

            var titleParts = contentNode.Text.Split(' ');
            var firstWordOfTitle = titleParts[0];
            if (removedWordsCollection.Contains(firstWordOfTitle.ToLower()))
            {
                contentNode.Text = contentNode.Text.Replace(firstWordOfTitle, "").Trim();
            }
        }
    }
}
