using System;
using UnityEngine;

namespace RewindNotary
{
    internal static class QuickSkills
    {
        internal static readonly KeyCode[] Keys = { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R };
        internal static readonly string[] Starter = { "rea_thrust", "rea_rising", "rea_sweep", "rea_wave" };

        internal static int StarterIndex(string id) => Array.IndexOf(Starter, id);
        internal static bool IsAssignable(string id) =>
            StarterIndex(id) >= 0 || Array.IndexOf(BenefactorIds.All, id) >= 0;

        internal static string Equipped(GameSettings settings, CoreRecord record, int slot)
        {
            var id = settings.quickSkills[slot];
            return StarterIndex(id) >= 0 || record.Get(id)?.acquired == true ? id : Starter[slot];
        }

        internal static string Name(SkillCatalog catalog, string id) =>
            StarterIndex(id) >= 0 ? Loc.Get("quick_skill." + id) : catalog.Get(id).DisplayName;
    }
}
