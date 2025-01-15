using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO
{
    public class ThemePreferenceDTO
    {
        /// <summary>
        /// Subject ID in the user's audhentication provider.
        /// </summary>
        public string? ProviderSubjectId { get; set; }

        /// <summary>
        /// Theme Preference of the user
        /// </summary>
        public ThemePreference? ThemePreference { get; set; }
    }
}