using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    [Serializable]
    public sealed class JanhyangPhase
    {
        public string Name => Loc.Get(name);
        public string name;
        public float enterHealthRatio, accuracy, windup, recovery, linkGap;
        public int targets, stageCap;
        public int[] pattern;
    }
    [Serializable]
    public sealed class JanhyangTechnique
    {
        public string skillId;
        public int damage, finisherDamage;
        public float knockback;
    }
    [Serializable]
    public sealed class JanhyangSwordBeat
    {
        public float duration, strikeTime, reach, width, height;
    }
    [Serializable]
    public sealed class JanhyangDefinition
    {
        public int version, memoryUses;
        public float memorySeconds, counterRecovery, transitionRecovery, aimLockSeconds;
        public float arenaMin, arenaMax, moveSpeed, initialCooldown, initialJump, jumpSpeed, jumpInterval;
        public Color tint;
        public JanhyangPhase[] phases;
        public JanhyangTechnique[] techniques;
        public JanhyangSwordBeat[] sword;
        public JanhyangTechnique Find(string id) => Array.Find(techniques, t => t.skillId == id);
        public int PhaseFor(int current, int maximum)
        {
            float ratio = (float)current / Math.Max(1, maximum);
            int phase = 0;
            for (int i = 1; i < phases.Length; i++)
                if (ratio <= phases[i].enterHealthRatio) phase = i;
            return phase;
        }
        public static JanhyangDefinition Load(SkillCatalog catalog)
        {
            var asset = Resources.Load<TextAsset>("Bosses/janhyang");
            if (asset == null) throw new InvalidOperationException("Missing Janhyang definition");
            var data = JsonUtility.FromJson<JanhyangDefinition>(asset.text);
            data.Validate(catalog);
            return data;
        }
        private void Validate(SkillCatalog catalog)
        {
            if (version != 1 || memoryUses < 2 || memoryUses > 32 || memorySeconds <= 0
                || counterRecovery <= 0 || transitionRecovery <= 0 || aimLockSeconds < 0.15f
                || phases == null || phases.Length != 3 || techniques == null || techniques.Length != 6
                || sword == null || sword.Length != 4 || arenaMin >= arenaMax || moveSpeed <= 0)
                throw new InvalidOperationException("Invalid Janhyang definition");
            float previous = 2f, accuracy = 0f;
            foreach (var p in phases)
            {
                if (p.enterHealthRatio < 0 || p.enterHealthRatio > 1 || p.enterHealthRatio >= previous
                    || p.accuracy < accuracy || p.accuracy > 1 || p.windup < 0.45f || p.windup <= aimLockSeconds
                    || p.recovery < 0.5f || p.linkGap < 0.15f || p.targets < 1 || p.targets > 2
                    || p.stageCap < 1 || p.stageCap > 3 || p.pattern == null || p.pattern.Length < 1 || p.pattern.Length > 3)
                    throw new InvalidOperationException("Invalid Janhyang phase");
                foreach (int index in p.pattern) if (index < 0 || index >= p.targets)
                    throw new InvalidOperationException("Invalid Janhyang pattern index");
                previous = p.enterHealthRatio; accuracy = p.accuracy;
            }
            var ids = new HashSet<string>();
            foreach (var t in techniques)
                if (!ids.Add(t.skillId) || !catalog.Get(t.skillId).implemented || t.damage < 1 || t.damage > 2
                    || t.finisherDamage < t.damage || t.finisherDamage > 2 || t.knockback <= 0 || t.knockback > 1)
                    throw new InvalidOperationException("Invalid Janhyang technique");
            foreach (var beat in sword)
                if (beat.strikeTime <= 0 || beat.duration <= beat.strikeTime || beat.reach <= 0 || beat.width <= 0 || beat.height <= 0)
                    throw new InvalidOperationException("Invalid Janhyang sword shape");
        }
    }
}
