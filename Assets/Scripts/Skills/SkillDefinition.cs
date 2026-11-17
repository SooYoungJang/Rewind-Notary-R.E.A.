using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    // Definition data never contains a player's progress. All thresholds are v6.1 contracts.
    public static class SkillRules
    {
        public const string Version = "v6.1-t0-1";
        public const string BasicSword = "basic_sword", SilverStitch = "silver_stitch";
        public const int InitialSwordUses = 120;
        public const int SwordStage2 = 151, SwordStage3 = 400;
        public const int BossStage2 = 30, BossStage3 = 80;
        public static int Stage(int uses, int second, int third) => uses >= third ? 3 : uses >= second ? 2 : 1;
    }

    [Serializable]
    public sealed class SkillStageDefinition
    {
        public int stage, comboLimit, cutDamage, installationLimit;
        public string name, effectId;
        public float stepSpeed, stepSeconds, waveReach, afterimageSeconds;
        public float reach, cooldown, bindSeconds, lifetime, maxSpan;
    }

    [Serializable]
    public sealed class SkillDefinition
    {
        public string skillId, displayName, sourceId, doomId;
        public bool implemented;
        public int stage2At, stage3At;
        public SkillStageDefinition[] stages;
        public int Stage(int uses) => SkillRules.Stage(uses, stage2At, stage3At);
        public SkillStageDefinition Profile(int stage) => stages[stage - 1];
    }

    [Serializable]
    public sealed class SkillCatalog
    {
        public string rulesVersion;
        public SkillDefinition[] skills;
        public SkillDefinition Get(string id)
        {
            foreach (var skill in skills) if (skill.skillId == id) return skill;
            throw new ArgumentException("Unknown skillId: " + id);
        }
        public static SkillCatalog Load()
        {
            var asset = Resources.Load<TextAsset>("Skills/catalog");
            if (asset == null) throw new InvalidOperationException("Missing skill catalog");
            var catalog = JsonUtility.FromJson<SkillCatalog>(asset.text);
            if (catalog == null || catalog.rulesVersion != SkillRules.Version || catalog.skills == null || catalog.skills.Length != 6)
                throw new InvalidOperationException("Invalid skill catalog/version");
            var ids = new HashSet<string>();
            var dooms = new HashSet<string>();
            foreach (var skill in catalog.skills)
            {
                bool basic = skill.skillId == SkillRules.BasicSword;
                if (!ids.Add(skill.skillId) || skill.stages == null || skill.stages.Length != 3
                    || skill.stage2At != (basic ? SkillRules.SwordStage2 : SkillRules.BossStage2)
                    || skill.stage3At != (basic ? SkillRules.SwordStage3 : SkillRules.BossStage3)
                    || (!basic && (string.IsNullOrEmpty(skill.doomId) || !dooms.Add(skill.doomId))))
                    throw new InvalidOperationException("Skill definition contract mismatch: " + skill.skillId);
                for (int i = 0; i < 3; i++)
                    if (skill.stages[i].stage != i + 1) throw new InvalidOperationException("Invalid stage order");
            }
            if (!ids.Contains(SkillRules.BasicSword) || !ids.Contains(SkillRules.SilverStitch) || dooms.Count != 5)
                throw new InvalidOperationException("Missing skill/doom identity");
            return catalog;
        }
    }
}
