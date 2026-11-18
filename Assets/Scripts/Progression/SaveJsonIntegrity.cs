using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace RewindNotary
{
    internal sealed class UnsupportedSaveVersionException : IOException
    {
        public UnsupportedSaveVersionException() : base(Loc.Get("save.unsupported_version")) { }
    }

    // JsonUtility maps into fields; validate the complete JSON grammar and wire types first.
    // No package/reflection dependency. Keep these field sets covered by the static schema audit.
    internal static class SaveJsonIntegrity
    {
        private const string CoreFields = "schemaVersion rulesVersion slotId savedUtc revision testData basicMasteryReached firstTransferPending skills ledger completedEncounters storyFlags lastStageId investigation finalBattleAttempt lastFinalBattleAttempt";
        private const string LegacyFields = "schemaVersion rulesVersion slotId savedUtc revision testData basicMasteryReached firstTransferPending skills ledger completedEncounters";
        private const string SkillFields = "skillId acquiredEncounterId acquired validUseCount stage";
        private const string LedgerFields = "doomId skillId ownerId transferEventId transferredToRea";
        private const string InvestigationFields = "clueIds readStageIds confirmedEntryIds";
        private const string AttemptFields = "attemptId outcome active physicalCoreDestroyed zeroFormClashCompleted returnCount phase basicStageAtSettlement finalConfirmationCount returnOrder remainingSkills";

        internal static Dictionary<string, object> Validate(string json)
        {
            var root = Object(new Parser(json).Parse());
            int version = (int)Integer(Value(root, "schemaVersion"), int.MinValue, int.MaxValue);
            // Do this before checking this build's body schema: a future body can have new fields.
            if (version > CoreRecord.SchemaVersion) throw new UnsupportedSaveVersionException();
            if (version < 1) throw Invalid("schemaVersion");
            string rules = Text(Value(root, "rulesVersion"));
            if (string.IsNullOrEmpty(rules)) throw Invalid("rulesVersion");
            if (rules != SkillRules.Version) throw new UnsupportedSaveVersionException();
            Fields(root, CoreFields, LegacyFields);
            Text(root["slotId"]); Text(root["savedUtc"], true);
            Integer(root["revision"], 0, long.MaxValue);
            Boolean(root["testData"]); Boolean(root["basicMasteryReached"]); Boolean(root["firstTransferPending"]);
            foreach (var item in Array(root["skills"]))
            {
                var skill = Object(item); Fields(skill, SkillFields, SkillFields);
                Text(skill["skillId"]); Text(skill["acquiredEncounterId"], true); Boolean(skill["acquired"]);
                Integer(skill["validUseCount"], 0, int.MaxValue); Integer(skill["stage"], int.MinValue, int.MaxValue);
            }
            foreach (var item in Array(root["ledger"]))
            {
                var entry = Object(item); Fields(entry, LedgerFields, LedgerFields);
                foreach (string name in new[] { "doomId", "skillId", "ownerId" }) Text(entry[name]);
                Text(entry["transferEventId"], true); Boolean(entry["transferredToRea"]);
            }
            Strings(root["completedEncounters"]);
            if (version >= 2 || root.ContainsKey("storyFlags")) Strings(Value(root, "storyFlags"));
            if (version >= 2 || root.ContainsKey("lastStageId")) Text(Value(root, "lastStageId"));
            if (version >= 3 || (root.ContainsKey("investigation") && root["investigation"] != null))
            {
                var state = Object(Value(root, "investigation")); Fields(state, InvestigationFields, InvestigationFields);
                foreach (var value in state.Values) Strings(value);
            }
            if (version >= 4 || (root.ContainsKey("finalBattleAttempt") && root["finalBattleAttempt"] != null)) Attempt(Value(root, "finalBattleAttempt"));
            if (root.ContainsKey("lastFinalBattleAttempt") && root["lastFinalBattleAttempt"] != null) Attempt(root["lastFinalBattleAttempt"]);
            return root;
        }
        private static void Attempt(object value)
        {
            var state = Object(value); Fields(state, AttemptFields, AttemptFields);
            Text(state["attemptId"]); Text(state["outcome"]);
            foreach (string key in new[] { "active", "physicalCoreDestroyed", "zeroFormClashCompleted" }) Boolean(state[key]);
            foreach (string key in new[] { "returnCount", "phase", "basicStageAtSettlement", "finalConfirmationCount" })
                Integer(state[key], int.MinValue, int.MaxValue);
            Strings(state["returnOrder"]); Strings(state["remainingSkills"]);
        }
        private static void Fields(Dictionary<string, object> value, string allowed, string required)
        {
            var names = new HashSet<string>(allowed.Split(' '), StringComparer.Ordinal);
            foreach (string key in value.Keys) if (!names.Contains(key)) throw Invalid(Loc.Get("save.unknown_field", key));
            foreach (string key in required.Split(' ')) Value(value, key);
        }
        private static object Value(Dictionary<string, object> value, string key)
        { if (!value.TryGetValue(key, out var result)) throw Invalid(Loc.Get("save.missing_field", key)); return result; }
        private static Dictionary<string, object> Object(object value)
        { return value as Dictionary<string, object> ?? throw Invalid(Loc.Get("save.invalid_object")); }
        private static List<object> Array(object value)
        { return value as List<object> ?? throw Invalid(Loc.Get("save.invalid_array")); }
        private static string Text(object value, bool nullable = false)
        { if (nullable && value == null) return null; return value as string ?? throw Invalid(Loc.Get("save.invalid_string")); }
        private static void Boolean(object value) { if (!(value is bool)) throw Invalid(Loc.Get("save.invalid_boolean")); }
        private static void Strings(object value) { foreach (var item in Array(value)) Text(item); }
        private static long Integer(object value, long min, long max)
        {
            if (!(value is Number token) || !long.TryParse(token.Raw, NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out long result) || result < min || result > max) throw Invalid(Loc.Get("save.invalid_integer"));
            return result;
        }
        private static InvalidDataException Invalid(string reason) => new InvalidDataException(Loc.Get("save.corrupt_json", reason));
        private sealed class Number { public string Raw; }

        private sealed class Parser
        {
            private readonly string json;
            private int position;
            internal Parser(string source) { json = source ?? ""; }
            internal object Parse()
            {
                var result = ReadValue(0); Space();
                if (position != json.Length) throw Invalid(Loc.Get("save.trailing_data"));
                return result;
            }
            private void Space()
            { while (position < json.Length && (json[position] == ' ' || json[position] == '\t' || json[position] == '\r' || json[position] == '\n')) position++; }
            private bool Take(char token)
            { Space(); if (position >= json.Length || json[position] != token) return false; position++; return true; }
            private void Need(char token) { if (!Take(token)) throw Invalid(Loc.Get("save.syntax_truncated")); }
            private object ReadValue(int depth)
            {
                if (depth > 64) throw Invalid(Loc.Get("save.nesting_depth"));
                Space(); if (position >= json.Length) throw Invalid(Loc.Get("save.truncated"));
                char c = json[position];
                if (c == '{')
                {
                    position++; var result = new Dictionary<string, object>(StringComparer.Ordinal);
                    if (Take('}')) return result;
                    do
                    {
                        string key = ReadString(); Need(':');
                        if (result.ContainsKey(key)) throw Invalid(Loc.Get("save.duplicate_field", key));
                        result.Add(key, ReadValue(depth + 1));
                        if (Take('}')) return result;
                        Need(',');
                    } while (true);
                }
                if (c == '[')
                {
                    position++; var result = new List<object>();
                    if (Take(']')) return result;
                    do { result.Add(ReadValue(depth + 1)); if (Take(']')) return result; Need(','); } while (true);
                }
                if (c == '"') return ReadString();
                if (c == 't') { Literal("true"); return true; }
                if (c == 'f') { Literal("false"); return false; }
                if (c == 'n') { Literal("null"); return null; }
                return ReadNumber();
            }
            private void Literal(string value)
            {
                foreach (char c in value)
                    if (position >= json.Length || json[position++] != c) throw Invalid(Loc.Get("save.invalid_literal"));
            }
            private static bool Digit(char c) => c >= '0' && c <= '9';
            private void Digits()
            {
                int start = position;
                while (position < json.Length && Digit(json[position])) position++;
                if (start == position) throw Invalid(Loc.Get("save.number_syntax"));
            }
            private Number ReadNumber()
            {
                int start = position;
                if (json[position] == '-') position++;
                if (position >= json.Length) throw Invalid(Loc.Get("save.truncated_number"));
                if (json[position] == '0') position++; else Digits();
                if (position < json.Length && json[position] == '.') { position++; Digits(); }
                if (position < json.Length && (json[position] == 'e' || json[position] == 'E'))
                {
                    position++;
                    if (position < json.Length && (json[position] == '+' || json[position] == '-')) position++;
                    Digits();
                }
                return new Number { Raw = json.Substring(start, position - start) };
            }
            private string ReadString()
            {
                Need('"'); var result = new StringBuilder();
                while (position < json.Length)
                {
                    char c = json[position++];
                    if (c == '"')
                    {
                        string value = result.ToString();
                        for (int i = 0; i < value.Length; i++)
                        {
                            if (char.IsHighSurrogate(value[i]))
                            { if (++i >= value.Length || !char.IsLowSurrogate(value[i])) throw Invalid(Loc.Get("save.unicode_pair")); }
                            else if (char.IsLowSurrogate(value[i])) throw Invalid(Loc.Get("save.unicode_pair"));
                        }
                        return value;
                    }
                    if (c < 32) throw Invalid(Loc.Get("save.control_character"));
                    if (c != '\\') { result.Append(c); continue; }
                    if (position >= json.Length) throw Invalid(Loc.Get("save.truncated_escape"));
                    switch (json[position++])
                    {
                        case '"': result.Append('"'); break;
                        case '\\': result.Append('\\'); break;
                        case '/': result.Append('/'); break;
                        case 'b': result.Append('\b'); break;
                        case 'f': result.Append('\f'); break;
                        case 'n': result.Append('\n'); break;
                        case 'r': result.Append('\r'); break;
                        case 't': result.Append('\t'); break;
                        case 'u':
                            if (position + 4 > json.Length || !ushort.TryParse(json.Substring(position, 4),
                                NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort code)) throw Invalid(Loc.Get("save.unicode_escape"));
                            result.Append((char)code); position += 4; break;
                        default: throw Invalid(Loc.Get("save.escape_syntax"));
                    }
                }
                throw Invalid(Loc.Get("save.truncated_string"));
            }
        }
    }
}
