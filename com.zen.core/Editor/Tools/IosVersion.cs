using System;

namespace Base.Tools
{
    /// <summary>iOS version strings of Player Settings and Info.plist files ("17", "17.0", "12.4.1").</summary>
    internal static class IosVersion
    {
        /// <summary>Like Version.TryParse, which rejects a major version alone ("17").</summary>
        public static bool TryParse(string text, out Version version)
        {
            text = text?.Trim();
            return Version.TryParse(text != null && !text.Contains('.') ? text + ".0" : text, out version);
        }
    }
}
