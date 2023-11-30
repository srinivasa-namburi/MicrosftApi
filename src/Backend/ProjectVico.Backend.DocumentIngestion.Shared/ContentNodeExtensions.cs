using ProjectVico.Backend.DocumentIngestion.Shared.Models;

namespace ProjectVico.Backend.DocumentIngestion.Shared
{
    public static class ContentNodeExtensions
    {
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
