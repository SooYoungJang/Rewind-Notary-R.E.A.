using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    [Serializable] public sealed class CasselReturnProfile
    {
        public int r, releasedPercent, directBeats;
        public float guardPower, parryBonus, waveReach, staggerSeconds, echoDelay, reaTheme;
    }
    [Serializable] public sealed class CasselPattern
    {
        public string Label => Loc.Get(label);
        public string id, label, skillId, shape;
        public int beats, damage;
        public float windup, interval, recovery, reach, height, dashSpeed;
    }
    [Serializable] public sealed class CasselDefinition
    {
        public int version, health, mirrorHealth, coreIntegrity;
        public float arenaMin, arenaMax, playerX, bossX, graceSeconds, exposureSeconds, matchDistance, guardThreshold, parryOpening;
        public CasselReturnProfile[] returns;
        public CasselPattern[] patterns;
        public string[] openingSequence, mirrorSequence, finaleSequence;
        public CasselReturnProfile Profile(int r) => returns[Mathf.Clamp(r, 0, 5)];
        public CasselPattern Pattern(string id) => Array.Find(patterns, p => p.id == id);
        public string[] Sequence(int phase) => phase == 1 ? openingSequence : phase == 2 ? mirrorSequence : finaleSequence;
        public bool Enabled(CasselPattern pattern, FinalBattleAttempt state) => pattern.skillId == SkillRules.BasicSword || state.CanUse(pattern.skillId);
        private static CasselDefinition cached;
        public static CasselDefinition Load()
        {
            if (cached != null) return cached;
            var asset = Resources.Load<TextAsset>("Bosses/cassel");
            if (asset == null) throw new InvalidOperationException("Missing Cassel definition");
            var value = JsonUtility.FromJson<CasselDefinition>(asset.text); value.Validate(); cached = value; return value;
        }
        public void Validate()
        {
            if (version != 1 || health <= mirrorHealth || mirrorHealth <= 1 || coreIntegrity <= 0
                || arenaMin >= playerX || playerX >= bossX || bossX >= arenaMax || graceSeconds != 3f
                || exposureSeconds < 10 || matchDistance < 4 || guardThreshold <= 0 || parryOpening < 1
                || returns == null || returns.Length != 6 || patterns == null || patterns.Length != 6)
                throw new InvalidOperationException("Invalid Cassel definition");
            for (int r = 0; r <= 5; r++)
            {
                var p = returns[r];
                if (p.r != r || p.releasedPercent != r * 20 || Mathf.Abs(p.reaTheme - r * 0.2f) > 0.0001f
                    || p.guardPower < 1 || p.directBeats < 3 || p.directBeats > 4 || p.waveReach < 0
                    || p.parryBonus < 0 || p.staggerSeconds < 0 || p.echoDelay < 0)
                    throw new InvalidOperationException("Invalid Cassel return profile");
            }
            var ids = new HashSet<string>(); var skills = new HashSet<string>();
            foreach (var p in patterns)
                if (p == null || !ids.Add(p.id) || !skills.Add(p.skillId)
                    || (p.skillId != SkillRules.BasicSword && Array.IndexOf(BenefactorIds.All, p.skillId) < 0)
                    || Array.IndexOf(new[] { "sword", "thread", "pendulum", "petals", "bell", "portrait" }, p.shape) < 0
                    || p.windup < 0.45f || p.interval < 0.25f || p.recovery < 0.6f || p.reach <= 0 || p.height <= 0
                    || p.beats < 1 || p.beats > 4 || p.damage != 1 || p.dashSpeed < 0)
                    throw new InvalidOperationException("Invalid Cassel pattern");
            foreach (var sequence in new[] { openingSequence, mirrorSequence, finaleSequence })
            {
                if (sequence == null || sequence.Length == 0 || !Array.Exists(sequence, id => Pattern(id)?.skillId == SkillRules.BasicSword))
                    throw new InvalidOperationException("Cassel requires an unconditional basic route");
                foreach (var id in sequence) if (Pattern(id) == null) throw new InvalidOperationException("Unknown Cassel pattern");
            }
        }
    }
}
