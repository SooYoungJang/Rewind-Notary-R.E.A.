using System;
using System.Collections.Generic;

namespace RewindNotary
{
    // Read-only consumer of the SAME events written to uses-*.jsonl. Never reconstruct
    // recency from lifetime mastery counts, and never award uses for an enemy imitation.
    public sealed class JanhyangMemory
    {
        private sealed class Sample
        {
            public string Skill, Use;
            public float At;
            public int Stage;
        }
        public readonly struct Choice
        {
            public readonly string SkillId;
            public readonly int Stage, Count;
            public Choice(string skill, int stage, int count) { SkillId = skill; Stage = stage; Count = count; }
        }
        private readonly List<Sample> recent = new List<Sample>();
        private readonly JanhyangDefinition data;
        private bool? debugStream;
        public JanhyangMemory(JanhyangDefinition definition) { data = definition; }
        public void Clear() { recent.Clear(); debugStream = null; }
        private void Prune(float now) { recent.RemoveAll(s => now - s.At >= data.memorySeconds); }
        public bool Observe(ProgressionEvent e, float now)
        {
            if (e == null) return false;
            if (e.kind == "debug_seed") { Clear(); return false; }
            if (e.kind != "valid_use" || e.reason != "accepted" || e.after != e.before + 1
                || string.IsNullOrEmpty(e.useId) || data.Find(e.skillId) == null) return false;
            if (debugStream.HasValue && debugStream.Value != e.testData) Clear();
            debugStream = e.testData;
            Prune(now);
            if (recent.Exists(s => s.Use == e.useId)) return false;
            recent.Add(new Sample { Skill = e.skillId, Use = e.useId, At = now, Stage = e.stage });
            while (recent.Count > data.memoryUses) recent.RemoveAt(0);
            return true;
        }
        public Choice[] Select(float now, int limit)
        {
            Prune(now);
            var counts = new Dictionary<string, int>();
            var last = new Dictionary<string, int>();
            for (int i = 0; i < recent.Count; i++)
            {
                string id = recent[i].Skill;
                counts[id] = counts.TryGetValue(id, out int count) ? count + 1 : 1;
                last[id] = i;
            }
            var ids = new List<string>(counts.Keys);
            ids.Sort((a, b) => {
                int frequency = counts[b].CompareTo(counts[a]);
                if (frequency != 0) return frequency;
                int recency = last[b].CompareTo(last[a]);
                return recency != 0 ? recency : string.CompareOrdinal(a, b);
            });
            // No invented history: this is an explicitly unlearned basic fallback (Count=0).
            if (ids.Count == 0) return new[] { new Choice(SkillRules.BasicSword, 1, 0) };
            var result = new Choice[Math.Min(Math.Min(limit, 2), ids.Count)];
            for (int i = 0; i < result.Length; i++)
                result[i] = new Choice(ids[i], recent[last[ids[i]]].Stage, counts[ids[i]]);
            return result;
        }
        public static bool RotationCounter(ProgressionEvent e, string focus, string bossName, bool windowOpen, bool directForgeCounter = false)
        {
            return windowOpen && e != null && e.kind == "valid_use" && e.reason == "accepted"
                && !string.IsNullOrEmpty(focus) && e.skillId != focus && e.targetId != null
                && (e.targetId == bossName || e.targetId.StartsWith(bossName + ":", StringComparison.Ordinal)
                    || (directForgeCounter && e.skillId == BenefactorIds.Forge && e.targetId == "forge_counter"));
        }
    }
}
