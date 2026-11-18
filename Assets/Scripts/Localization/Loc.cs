using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RewindNotary
{
    // Presentation only. No progression, scene, input, time-scale or save-slot ownership.
    public static class Loc
    {
        [Serializable] private sealed class Entry { public string key, value; }
        [Serializable] private sealed class Table { public string language; public Entry[] entries; }
        private static readonly string[] languages = { "ko", "en", "de", "fr", "es", "zh-Hans", "ja", "ru", "it" };
        public static IReadOnlyList<string> Languages => Array.AsReadOnly(languages);
        private static readonly Dictionary<string, Dictionary<string, string>> tables = new Dictionary<string, Dictionary<string, string>>();
        private static readonly HashSet<string> warnings = new HashSet<string>();
        public static string Language { get; private set; } = "en";
        public static event Action LanguageChanged;
        public static bool Supported(string language) => Array.IndexOf(languages, language) >= 0;
        public static string DefaultLanguage(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.English: return "en";
                case SystemLanguage.German: return "de";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.ChineseSimplified: return "zh-Hans";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Russian: return "ru";
                case SystemLanguage.Italian: return "it";
                default: return "en"; // Traditional/unspecified Chinese is not a supported locale.
            }
        }
        public static string Normalize(string language) => Supported(language) ? language : DefaultLanguage(Application.systemLanguage);
        public static string NextLanguage(string language, int direction)
        {
            int index = Array.IndexOf(languages, Normalize(language));
            return languages[(index + (direction < 0 ? -1 : 1) + languages.Length) % languages.Length];
        }
        public static void SetLanguage(string language)
        {
            string next = Normalize(language);
            if (next == Language) return;
            Language = next;
            LanguageChanged?.Invoke();
        }
        public static CultureInfo Culture => CultureInfo.GetCultureInfo(Language == "zh-Hans" ? "zh-CN" : Language);
        private static Dictionary<string, string> Load(string language)
        {
            if (tables.TryGetValue(language, out var existing)) return existing;
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                var asset = Resources.Load<TextAsset>("Localization/" + language);
                var table = asset == null ? null : JsonUtility.FromJson<Table>(asset.text);
                if (table == null || table.language != language || table.entries == null)
                    Warn("Invalid or missing localization table: " + language);
                else foreach (var entry in table.entries)
                    if (entry != null && !string.IsNullOrEmpty(entry.key) && !string.IsNullOrWhiteSpace(entry.value))
                    {
                        if (result.ContainsKey(entry.key)) Warn("Duplicate localization key: " + language + "/" + entry.key);
                        else result.Add(entry.key, entry.value);
                    }
            }
            catch (Exception exception) { Warn("Localization load failed: " + language + ": " + exception.Message); }
            tables[language] = result;
            return result;
        }
        public static bool Contains(string key) => key != null && (Load(Language).ContainsKey(key) || Load("en").ContainsKey(key) || Load("ko").ContainsKey(key));
        public static string Get(string key) => Get(key, Array.Empty<object>());
        public static string Get(string key, params object[] args)
        {
            if (key == null) return "";
            // Also fall through on a malformed translated format, so UI rendering cannot throw.
            foreach (string language in new[] { Language, "en", "ko" })
            {
                if (!Load(language).TryGetValue(key, out string value)) continue;
                if (args == null || args.Length == 0) return value;
                try { return string.Format(Culture, value, args); }
                catch (FormatException) { Warn("Invalid localization format: " + language + "/" + key); }
            }
            Warn("Missing localization key: " + key);
            return "[" + key + "]";
        }
        private static void Warn(string message) { if (warnings.Add(message)) Debug.LogWarning(message); }
    }
}
