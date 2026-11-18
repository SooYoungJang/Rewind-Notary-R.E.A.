namespace RewindNotary
{
    // Font-family requirements only; no font assets or downloads are included in this change.
    public enum LocalizationFontGroup { NotoSans, NotoSansCjkKR, NotoSansCjkSC, NotoSansCjkJP }
    public static class LocalizationFonts
    {
        public static LocalizationFontGroup Group(string language)
        {
            switch (language)
            {
                case "ko": return LocalizationFontGroup.NotoSansCjkKR;
                case "zh-Hans": return LocalizationFontGroup.NotoSansCjkSC;
                case "ja": return LocalizationFontGroup.NotoSansCjkJP;
                default: return LocalizationFontGroup.NotoSans; // Latin and Cyrillic.
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
    }
}
