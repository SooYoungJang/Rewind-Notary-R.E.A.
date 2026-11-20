using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public enum LocalizationFontGroup { NotoSans, NotoSansCjkKR, NotoSansCjkSC, NotoSansCjkJP }
    public static class LocalizationFonts
    {
        private static readonly Dictionary<string, Font> Cache = new Dictionary<string, Font>();
        private static Font fallbackOsFont;

        public static LocalizationFontGroup Group(string language)
        {
            switch (language)
            {
                case "ko": return LocalizationFontGroup.NotoSansCjkKR;
                case "zh-Hans": return LocalizationFontGroup.NotoSansCjkSC;
                case "ja": return LocalizationFontGroup.NotoSansCjkJP;
                default: return LocalizationFontGroup.NotoSans;
            }
        }

        public static string Family(string language)
        {
            switch (Group(language))
            {
                case LocalizationFontGroup.NotoSansCjkKR: return "Noto Sans CJK KR";
                case LocalizationFontGroup.NotoSansCjkSC: return "Noto Sans CJK SC";
                case LocalizationFontGroup.NotoSansCjkJP: return "Noto Sans CJK JP";
                default: return "Noto Sans";
            }
        }

        public static string BodyResource(string language)
        {
            switch (Group(language))
            {
                case LocalizationFontGroup.NotoSansCjkSC: return "Fonts/NotoSansSC-Regular";
                case LocalizationFontGroup.NotoSansCjkJP: return "Fonts/NotoSansJP-Regular";
                default: return "Fonts/NotoSansKR-Regular";
            }
        }

        public static string TitleResource(string language)
        {
            switch (Group(language))
            {
                case LocalizationFontGroup.NotoSansCjkSC: return "Fonts/NotoSansSC-Regular";
                case LocalizationFontGroup.NotoSansCjkJP: return "Fonts/NotoSansJP-Regular";
                default: return "Fonts/NotoSerifKR-SemiBold";
            }
        }

        public static Font LoadBodyFont(string language) => LoadCached(BodyResource(language));
        public static Font LoadTitleFont(string language) => LoadCached(TitleResource(language));

        private static Font LoadCached(string path)
        {
            if (Cache.TryGetValue(path, out var existing) && existing != null) return existing;
            var loaded = Resources.Load<Font>(path);
            if (loaded != null)
            {
                Cache[path] = loaded;
                return loaded;
            }
            if (fallbackOsFont == null)
                fallbackOsFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial Unicode MS" }, 18);
            return fallbackOsFont;
        }
    }
}
