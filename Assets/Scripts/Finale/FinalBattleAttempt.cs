using System;
using System.Collections.Generic;

namespace RewindNotary
{
    public enum FinaleEnding { None, Basic, True }

    // Attempt-local rights, deliberately separate from the immutable mastery/transfer ledger.
    [Serializable]
    public sealed class FinalBattleAttempt
    {
        public string attemptId = "", outcome = "ready";
        public bool active, physicalCoreDestroyed, zeroFormClashCompleted;
        public int returnCount, phase = 1, basicStageAtSettlement, finalConfirmationCount;
        public List<string> returnOrder = new List<string>();
        public List<string> remainingSkills = new List<string>(BenefactorIds.All);
        public string DoomOwnerId => finalConfirmationCount == 1 ? "cassel" : "rea";
        public int ReleasedPercent => returnCount * 20;
        public float OutputMultiplier => 1f + ReleasedPercent / 100f;
        public bool CanUse(string skillId) => skillId == SkillRules.BasicSword || remainingSkills.Contains(skillId);
        public static FinalBattleAttempt Begin() => new FinalBattleAttempt { active = true,
            attemptId = Guid.NewGuid().ToString("N"), outcome = "fighting" };
        public bool Reserve(string skillId)
        {
            if (!active || phase != 2 || !remainingSkills.Contains(skillId)
                || Array.IndexOf(BenefactorIds.All, skillId) < 0) return false;
            remainingSkills.Remove(skillId); returnOrder.Add(skillId); returnCount++;
            if (returnCount == 5) phase = 3;
            return true;
        }
        public bool Settle(int basicStage, bool completeClash, bool coreDestroyed)
        {
            if (!active || returnCount != 5 || phase != 3 || basicStage < 1 || basicStage > 3) return false;
            if (basicStage == 3 ? !completeClash : !coreDestroyed) return false;
            basicStageAtSettlement = basicStage;
            zeroFormClashCompleted = basicStage == 3;
            physicalCoreDestroyed = basicStage < 3;
            finalConfirmationCount = zeroFormClashCompleted ? 1 : 0;
            active = false; outcome = "settled"; return true;
        }
        public void Stop(string reason)
        { if (active) { active = false; outcome = reason; } }
        public void Validate()
        {
            if (returnOrder == null || remainingSkills == null || returnCount != returnOrder.Count
                || returnCount < 0 || returnCount > 5 || phase < 1 || phase > 3
                || (phase == 1 && returnCount != 0) || (phase == 3) != (returnCount == 5)
                || remainingSkills.Count + returnCount != 5)
                throw new InvalidOperationException("Invalid final battle state");
            var ids = new HashSet<string>();
            foreach (var id in returnOrder)
                if (Array.IndexOf(BenefactorIds.All, id) < 0 || !ids.Add(id)) throw new InvalidOperationException("Invalid return order");
            foreach (var id in remainingSkills)
                if (Array.IndexOf(BenefactorIds.All, id) < 0 || !ids.Add(id)) throw new InvalidOperationException("Invalid remaining skills");
            if (outcome != "ready" && outcome != "fighting" && outcome != "defeated" && outcome != "interrupted" && outcome != "settled")
                throw new InvalidOperationException("Invalid battle outcome");
            if (active != (outcome == "fighting") || (outcome != "ready" && string.IsNullOrEmpty(attemptId))
                || (outcome == "ready" && (returnCount != 0 || phase != 1 || !string.IsNullOrEmpty(attemptId))))
                throw new InvalidOperationException("Invalid battle lifetime");
            if (outcome == "settled")
            {
                if (FinaleEndingEvaluator.Evaluate(this) == FinaleEnding.None) throw new InvalidOperationException("Invalid settlement");
            }
            else if (basicStageAtSettlement != 0 || physicalCoreDestroyed || zeroFormClashCompleted || finalConfirmationCount != 0)
                throw new InvalidOperationException("Unsettled attempt cannot transfer ownership");
        }
    }
    public static class FinaleEndingEvaluator
    {
        // v6.1: return order changes tactics only. B is captured at the actual settlement.
        public static FinaleEnding Evaluate(FinalBattleAttempt attempt)
        {
            if (attempt == null || attempt.active || attempt.outcome != "settled" || attempt.returnCount != 5
                || attempt.phase != 3 || attempt.remainingSkills == null || attempt.remainingSkills.Count != 0
                || attempt.returnOrder == null || attempt.returnOrder.Count != 5
                || new HashSet<string>(attempt.returnOrder).Count != 5
                || attempt.returnOrder.Exists(id => Array.IndexOf(BenefactorIds.All, id) < 0)) return FinaleEnding.None;
            if (attempt.basicStageAtSettlement == 3 && attempt.zeroFormClashCompleted
                && !attempt.physicalCoreDestroyed && attempt.finalConfirmationCount == 1) return FinaleEnding.True;
            if (attempt.basicStageAtSettlement >= 1 && attempt.basicStageAtSettlement < 3
                && attempt.physicalCoreDestroyed && !attempt.zeroFormClashCompleted
                && attempt.finalConfirmationCount == 0) return FinaleEnding.Basic;
            return FinaleEnding.None;
        }
    }
}
