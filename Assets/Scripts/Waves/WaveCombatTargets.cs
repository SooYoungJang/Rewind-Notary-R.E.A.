using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    // Executors forward their ORIGINAL context and actual effect bounds. No synthetic uses,
    // count multiplier, activation credit, or substitute skill input path.
    public static class WaveCombatTargets
    {
        public static readonly List<WaveTarget2D> All = new List<WaveTarget2D>();
        public static bool Sword(SkillSession session, UseContext use, Bounds area, bool finisher, int starter = -1, bool echo = false)
        {
            if (session == null || use == null || use.Closed || use.Owner != session.Progression) return false;
            bool changed = false;
            foreach (var target in All)
                if (area.Intersects(target.Bounds))
                {
                    bool hit = target.Cut(use, echo);
                    if (hit) target.ReactToStarter(starter, area.center.x);
                    session.Progression.Apply(use, new CombatEffectResult(target.Kind, target.TargetId, hit, hit ? 1 : 0));
                    changed |= hit;
                }
            if (!changed) return false;
            AudioManager.Play(AudioManager.Sfx.Hit);
            if (finisher) ImpactFeedback.ComboFinisherHit(); else ImpactFeedback.SwordHit();
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
