using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    // Executors forward their ORIGINAL context and actual effect bounds. No synthetic uses,
    // count multiplier, activation credit, or substitute skill input path.
    public static class WaveCombatTargets
    {
        public static readonly List<WaveTarget2D> All = new List<WaveTarget2D>();
        public static bool Sword(SkillSession session, UseContext use, Bounds area, int comboStep,
            int starter, bool juggle, int pulse, HashSet<int> visited)
        {
            if (session == null || use == null || use.Closed || use.Owner != session.Progression) return false;
            bool changed = false;
            foreach (var target in All)
                if (area.Intersects(target.Bounds) && (visited == null || visited.Add(target.GetInstanceID())))
                {
                    bool hit = target.Cut(use, comboStep, pulse);
                    if (hit) target.ReactToSword(starter, area.center.x, comboStep, juggle);
                    session.Progression.Apply(use, new CombatEffectResult(target.Kind, target.TargetId, hit, hit ? 1 : 0));
                    changed |= hit;
                }
            if (!changed) return false;
            AudioManager.Play(AudioManager.Sfx.Hit);
            if (comboStep == 4) ImpactFeedback.ComboFinisherHit();
            else if (starter < 0) ImpactFeedback.SwordHit();
            return true;
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
