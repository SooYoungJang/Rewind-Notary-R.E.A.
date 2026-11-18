using UnityEngine;

namespace RewindNotary
{
    public static class FinaleCombat
    {
        // Integer HP rounds only once at each actual contact; the multiplier itself remains exactly 1 + R/5.
        public static int Scale(SkillSession session, int damage) => session?.Finale.State.active == true
            ? Mathf.CeilToInt(damage * session.Finale.State.OutputMultiplier) : damage;
    }
}
