using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    [Serializable] public sealed class InvestigationTopic {
        public string Label => Loc.Get(label); public string id, label; }
    [Serializable] public sealed class InvestigationClue
    {
        public string Label => Loc.Get(label);
        public string clueId, topicId, label, textKey, summaryKey;
        public string BodyKey => "investigation.clue." + clueId + ".body";
    }
    [Serializable] public sealed class InvestigationPointDefinition
    {
        public string Label => Loc.Get(label);
        public string id, stageId, kind, label, clueId;
        public float x, y, radius;
    }
    [Serializable] public sealed class InvestigationCondition
    {
        public string kind, subject;
        public int amount;
    }
    [Serializable] public sealed class InvestigationGate
    {
        // ALL terms AND (ANY terms, if present). Empty ALL/ANY means no additional restriction.
        public InvestigationCondition[] all, any;
    }
    [Serializable] public sealed class CoreReadingEntry
    {
        public string id, titleKey, summaryKey;
        public string BodyKey => "investigation.record." + id + ".body";
        public string[] relatedClues;
    }
    [Serializable] public sealed class CoreReadingStage
    {
        public string Label => Loc.Get(label);
        public string id, label;
        public InvestigationGate gate;
        public CoreReadingEntry[] entries;
    }
    [Serializable] public sealed class InvestigationCatalog
    {
        public int version;
        public string finalStageId;
        public InvestigationTopic[] topics;
        public InvestigationClue[] clues;
        public InvestigationPointDefinition[] points;
        public CoreReadingStage[] stages;
        private static InvestigationCatalog cached;
        public InvestigationClue Clue(string id) => Array.Find(clues, c => c.clueId == id);
        public CoreReadingStage Stage(string id) => Array.Find(stages, s => s.id == id);
        public InvestigationTopic Topic(string id) => Array.Find(topics, t => t.id == id);
        public static InvestigationCatalog Load()
        {
            if (cached != null) return cached;
            var asset = Resources.Load<TextAsset>("Investigation/catalog");
            if (asset == null) throw new InvalidOperationException("Missing investigation catalog");
            var value = JsonUtility.FromJson<InvestigationCatalog>(asset.text);
            if (value == null) throw new InvalidOperationException("Invalid investigation catalog");
            value.Validate(); cached = value; return value;
        }
        private static void Require(bool valid)
        { if (!valid) throw new InvalidOperationException("Invalid investigation data/reference"); }
        private static bool Key(string key) => key != null && System.Text.RegularExpressions.Regex.IsMatch(key, "^investigation\\.[a-z0-9_.]+$");
        private static bool Id(HashSet<string> ids, string id) => !string.IsNullOrWhiteSpace(id) && ids.Add(id);
        public void Validate()
        {
            Require(version == 1 && topics != null && clues != null && points != null && stages != null);
            var ids = new HashSet<string>();
            foreach (var t in topics) Require(t != null && Id(ids, t.id) && !string.IsNullOrWhiteSpace(t.label));
            ids.Clear();
            foreach (var c in clues) Require(c != null && Id(ids, c.clueId) && Topic(c.topicId) != null
                && !string.IsNullOrWhiteSpace(c.label) && Key(c.textKey) && Key(c.summaryKey));
            ids.Clear();
            foreach (var p in points) Require(p != null && Id(ids, p.id) && p.stageId == "prototype"
                && !string.IsNullOrWhiteSpace(p.label) && !float.IsNaN(p.x) && !float.IsInfinity(p.x)
                && !float.IsNaN(p.y) && !float.IsInfinity(p.y) && p.radius > 0 && p.radius <= 2
                && (p.kind == "core" ? string.IsNullOrEmpty(p.clueId)
                    : (p.kind == "terminal" || p.kind == "document" || p.kind == "imprint") && Clue(p.clueId) != null));
            foreach (var c in clues) Require(Array.Exists(points, p => p.clueId == c.clueId));
            Require(Array.Exists(points, p => p.kind == "core"));
            ids.Clear();
            var entryIds = new HashSet<string>();
            var priorStages = new HashSet<string>();
            foreach (var s in stages)
            {
                Require(s != null && Id(ids, s.id) && !string.IsNullOrWhiteSpace(s.label)
                    && s.entries != null && s.entries.Length > 0 && s.gate != null && s.gate.all != null && s.gate.any != null);
                foreach (var term in s.gate.all) ValidateCondition(term, priorStages);
                foreach (var term in s.gate.any) ValidateCondition(term, priorStages);
                foreach (var e in s.entries)
                {
                    Require(e != null && Id(entryIds, e.id) && Key(e.titleKey) && Key(e.summaryKey)
                        && e.relatedClues != null && e.relatedClues.Length > 0);
                    var related = new HashSet<string>();
                    foreach (string id in e.relatedClues) Require(Clue(id) != null && related.Add(id));
                }
                priorStages.Add(s.id); // Ordered references forbid cycles and forward dependencies.
            }
            Require(Stage(finalStageId) != null);
        }
        private void ValidateCondition(InvestigationCondition c, HashSet<string> prior)
        {
            Require(c != null);
            switch (c.kind)
            {
                case "clue": Require(Clue(c.subject) != null); break;
                case "clue_percent": Require(Topic(c.subject) != null && Array.Exists(clues, d => d.topicId == c.subject)
                    && c.amount >= 1 && c.amount <= 100); break;
                case "transfers": Require(c.amount >= 1 && c.amount <= 5); break;
                case "skill_stage": Require(Array.Exists(SkillCatalog.Load().skills, s => s.skillId == c.subject)
                    && c.amount >= 1 && c.amount <= 3); break;
                case "trial_complete": Require(Array.Exists(BenefactorTrialCatalog.Load().trials, t => t.id == c.subject)); break;
                case "stage_read": Require(prior.Contains(c.subject)); break;
                case "story_flag": Require(c.subject == InvestigationService.MiddleInvestigationFlag); break;
                default: Require(false); break;
            }
        }
    }
}
