using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    // Executors forward their ORIGINAL context and actual effect bounds. No synthetic uses,
    // count multiplier, activation credit, or substitute skill input path.
    public static class WaveCombatTargets
    {
        public static readonly List<WaveTarget2D> All = new List<WaveTarget2D>();
        public static void Sword(SkillSession session, UseContext use, Bounds area, bool finisher)
        {
            if (session == null || use == null || use.Closed || use.Owner != session.Progression) return;
            bool changed = false;
            foreach (var target in All)
                if (area.Intersects(target.Bounds))
                {
                    bool hit = target.Cut(use);
                    session.Progression.Apply(use, new CombatEffectResult(target.Kind, target.TargetId, hit, hit ? 1 : 0));
                    changed |= hit;
                }
            if (!changed) return;
            AudioManager.Play(AudioManager.Sfx.Hit);
            if (finisher) ImpactFeedback.ComboFinisherHit(); else ImpactFeedback.SwordHit();
        }
        public static void Skill(SkillSession session, UseContext use, Bounds area)
        {
            if (session == null || use == null || use.Closed || use.Owner != session.Progression) return;
            foreach (var target in All)
                if (area.Intersects(target.Bounds))
                    session.Progression.Apply(use, new CombatEffectResult(target.Kind, target.TargetId + ":posture", target.Setup(use)));
        }
    }
}
