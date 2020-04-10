using System;

namespace GithubActionPinner.Core
{
    public static class VersionHelper
    {
        /// <summary>
        /// Helper to parse Github version tags.
        /// Supports "v1", "v1.0" and "v1.0.0" formats
        /// </summary>
        public static bool TryParse(string text, out Version version)
        {
            version = new Version();
            if (!text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                return false;
            if (int.TryParse(text.Substring(1), out int major))
            {
                version = new Version(major, 0);
                return true;
            }
            return Version.TryParse(text.Substring(1), out version);
        }
    }
}
