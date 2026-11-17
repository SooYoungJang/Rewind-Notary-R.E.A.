using UnityEngine;

namespace RewindNotary
{
    public static class SkillPracticeWorld
    {
        public static void Build(Transform parent, CombatMotor2D player, CombatMotor2D boss)
        {
            // Gap endpoints are the existing fixed banks x=16/20. All routes still allow ordinary jumps.
            Surface(parent, "gap_16_20", new Vector2(18, 0), 4, false, player, boss);
            Surface(parent, "cracked_support_32", new Vector2(32, 0.18f), 0.9f, true, player, boss);
            Surface(parent, "arena_seam_43", new Vector2(43, 0.18f), 0.9f, true, player, boss);
            var crate = new GameObject("practice_breakable_34"); crate.transform.SetParent(parent, false);
            crate.transform.position = new Vector3(34, 0.6f, 0); crate.AddComponent<DestructibleTarget2D>();
        }
        private static void Surface(Transform parent, string id, Vector2 at, float span, bool support,
            CombatMotor2D player, CombatMotor2D boss)
        {
            var go = new GameObject(id); go.transform.SetParent(parent, false); go.transform.position = at;
            go.AddComponent<StitchSurface2D>().Initialize(id, span, support, player, boss);
        }
        public static void ResetAttempt()
        {
            foreach (var surface in StitchSurface2D.All) surface.ResetAttempt();
            foreach (var target in DestructibleTarget2D.All) target.ResetAttempt();
        }
    }
}
