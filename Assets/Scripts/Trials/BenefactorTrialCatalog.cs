using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    [Serializable] public sealed class TrialDialogueKeys { public string enterKey, successKey, returnKey; }
    [Serializable] public sealed class TrialPattern
    {
        public string Label => Loc.Get(label);
        public string action, label;
        public float windup, duration, recovery, reach, speed, projectileSpeed, projectileLifetime, projectileHeight;
        public int damage;
    }
    [Serializable] public sealed class BenefactorSupportDefinition
    {
        public string Label => Loc.Get(label);
        public string id, label, effect, recharge;
        public int damage, heal, maxCharges;
        public float radius, seconds;
    }
    [Serializable] public sealed class BenefactorTrialDefinition
    {
        public string DisplayName => Loc.Get(displayName);
        public string Role => Loc.Get(role);
        public string ObjectiveLabel => Loc.Get(objectiveLabel);
        public string id, stageId, displayName, role, skillId, condition, objectiveLabel, opponentId;
        public int health, sessionCap, normalUsesToRenew, requiredSuccesses;
        public string renewalCondition;
        public float hitImmunity, spawnX, playerX, arenaMin, arenaMax, approachSpeed, restSeconds;
        public TrialDialogueKeys dialogue;
        public BenefactorSupportDefinition support;
        public TrialPattern[] patterns;
        public TrialPattern storyFinisher;
        public TrialPattern StoryFinisher => string.IsNullOrEmpty(storyFinisher?.action) ? null : storyFinisher;
        public bool Unlocked(CoreRecord record) => record?.Get(skillId)?.acquired == true;
        public bool Qualifies(CombatEffectResult result)
        {
            string target = result.TargetId ?? "";
            bool opponent = target == opponentId || target.StartsWith(opponentId + ":", StringComparison.Ordinal);
            switch (condition)
            {
                case "stitch": return (opponent && target.EndsWith(":joint", StringComparison.Ordinal))
                    || target.StartsWith("trial_anchor:" + id, StringComparison.Ordinal);
                case "opponent": return opponent;
                case "projectile": return target.StartsWith("trial_shot:" + id + ":", StringComparison.Ordinal);
                case "forge": return opponent || target == "forge_counter";
                case "paint": return target.EndsWith(":support", StringComparison.Ordinal)
                    || target.EndsWith(":projectile_block", StringComparison.Ordinal);
                default: return false;
            }
        }
    }
    [Serializable] public sealed class BenefactorTrialCatalog
    {
        public int version;
        public BenefactorTrialDefinition[] trials;
        private static BenefactorTrialCatalog cached;
        public static BenefactorTrialCatalog Load()
        {
            if (cached != null) return cached;
            var asset = Resources.Load<TextAsset>("Trials/catalog");
            if (asset == null) throw new InvalidOperationException(Loc.Get("trial.missing_data"));
            var value = JsonUtility.FromJson<BenefactorTrialCatalog>(asset.text);
            value.Validate(); cached = value; return cached;
        }
        public BenefactorTrialDefinition ForStage(string id) => Array.Find(trials, d => d.stageId == id);
        public BenefactorTrialDefinition ForSkill(string id) => Array.Find(trials, d => d.skillId == id);
        private void Validate()
        {
            if (version != 1 || trials == null || trials.Length != 5) throw new InvalidOperationException("Invalid trial catalog");
            var ids = new HashSet<string>(); var skills = new HashSet<string>();
            var catalog = SkillCatalog.Load();
            foreach (var d in trials)
            {
                if (d == null || !ids.Add(d.id) || !skills.Add(d.skillId) || catalog.Get(d.skillId).sourceId != d.id
                    || d.stageId != "trial_" + d.id || d.opponentId != "trial_opponent:" + d.id
                    || d.health <= 0 || d.hitImmunity < 0 || d.sessionCap <= 0 || d.normalUsesToRenew <= d.sessionCap
                    || d.renewalCondition != "ordinary_combat_effect" || d.requiredSuccesses <= 0 || d.requiredSuccesses > d.sessionCap || d.restSeconds <= 0
                    || d.arenaMin >= d.playerX || d.playerX >= d.spawnX || d.spawnX >= d.arenaMax
                    || d.approachSpeed <= 0 || d.dialogue == null || d.support == null || d.patterns == null || d.patterns.Length < 2)
                    throw new InvalidOperationException("Invalid trial: " + d?.id);
                if (Array.IndexOf(new[] { "stitch", "opponent", "projectile", "forge", "paint" }, d.condition) < 0
                    || !ValidKey(d.dialogue.enterKey, d.id) || !ValidKey(d.dialogue.successKey, d.id) || !ValidKey(d.dialogue.returnKey, d.id))
                    throw new InvalidOperationException("Invalid trial condition/text keys");
                var s = d.support;
                if (s.maxCharges != 1 || s.recharge != "renew_session_and_verify" || s.id != "support_" + d.id
                    || Array.IndexOf(new[] { "bind", "freeze", "strike", "heal" }, s.effect) < 0
                    || (s.effect == "heal" ? s.heal <= 0 : s.radius <= 0)
                    || (s.effect == "strike" && s.damage <= 0) || ((s.effect == "bind" || s.effect == "freeze") && s.seconds <= 0))
                    throw new InvalidOperationException("Invalid support");
                foreach (var p in d.patterns)
                    if (p == null || p.windup < 0.45f || p.duration <= 0 || p.recovery <= 0 || p.reach <= 0 || p.damage <= 0 || p.projectileSpeed <= 0 || p.projectileLifetime <= 0 || p.projectileHeight <= 0
                        || Array.IndexOf(new[] { "thread_sweep", "thread_cross", "pendulum_out", "pendulum_back", "waltz", "volley", "bell", "paint_line" }, p.action) < 0)
                        throw new InvalidOperationException("Invalid trial pattern");
                var finisher = d.StoryFinisher;
                if (finisher != null && (finisher.action != "thread_lunge" || string.IsNullOrEmpty(finisher.label) ||
                    finisher.windup < .45f || finisher.duration <= 0 || finisher.recovery <= 0 ||
                    finisher.reach <= 0 || finisher.speed <= 0 || finisher.damage <= 0))
                    throw new InvalidOperationException("Invalid story finisher: " + d.id);
            }
        }
        private static bool ValidKey(string key, string id) => key != null && key.StartsWith("trial." + id + ".", StringComparison.Ordinal)
            && System.Text.RegularExpressions.Regex.IsMatch(key, "^[a-z0-9_.]+$");
    }
}
