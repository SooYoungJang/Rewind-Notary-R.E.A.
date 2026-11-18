using System;

namespace RewindNotary
{
    public sealed class FinalBattleService
    {
        private readonly SkillProgressionService progression;
        private readonly Action save;
        private readonly bool storesPrevious;
        public FinalBattleAttempt State => progression.Record.finalBattleAttempt;
        private CoreRecord beforeBattle;
        internal CoreRecord PendingPrevious { get; private set; }
        private FinalBattleAttempt pendingSettlement;
        public event Action<string> Returned;
        public event Action<FinalBattleAttempt, FinaleEnding> Settled;
        public FinalBattleService(SkillProgressionService owner, Action persist) : this(owner, persist, false) { }
        internal FinalBattleService(SkillProgressionService owner, Action persist, bool storesPrevious)
        { progression = owner; save = persist; this.storesPrevious = storesPrevious; }
        public void BeginAttempt()
        {
            foreach (string id in BenefactorIds.All)
            {
                var skill = progression.Record.Get(id);
                if (!skill.acquired || progression.Catalog.Get(id).Stage(skill.validUseCount) != 3)
                    throw new InvalidOperationException(Loc.Get("finale.requires_five_skills"));
            }
            if (PendingPrevious != null) throw new InvalidOperationException(Loc.Get("save.retry_hint"));
            var old = State;
            if (old.outcome != "ready")
            {
                old.Stop("interrupted");
                progression.Record.lastFinalBattleAttempt = old;
            }
            beforeBattle = storesPrevious && !progression.Record.testData ? EndingCheckpoint.BeforeBattle(progression.Record) : null;
            progression.Record.finalBattleAttempt = FinalBattleAttempt.Begin(); save();
        }
        public void EnterMirrorPhase()
        { if (State.active && State.phase == 1) { State.phase = 2; save(); } }
        public bool Reserve(string skillId)
        {
            var skill = progression.Record.Get(skillId);
            if (skill == null || !skill.acquired || progression.Catalog.Get(skillId).Stage(skill.validUseCount) != 3
                || !State.Reserve(skillId)) return false;
            save(); Returned?.Invoke(skillId); return true;
        }
        public bool Settle(bool completeClash, bool coreDestroyed)
        {
            int basicStage = progression.Catalog.Get(SkillRules.BasicSword).Stage(progression.Record.Get(SkillRules.BasicSword).validUseCount);
            if (!State.Settle(basicStage, completeClash, coreDestroyed)) return false;
            var record = progression.Record;
            EndingCheckpoint.Remember(record, State);
            if (storesPrevious && !record.testData && FinaleEndingEvaluator.Evaluate(State) == FinaleEnding.Basic)
            {
                if (beforeBattle == null) beforeBattle = EndingCheckpoint.BeforeBattle(record);
                EndingCheckpoint.Publish(record, beforeBattle);
                PendingPrevious = beforeBattle;
            }
            if (storesPrevious && !record.testData) EndingCheckpoint.StartScenes(record);
            pendingSettlement = State;
            save();
            if (!storesPrevious) Saved();
            return true;
        }
        internal void Saved()
        {
            PendingPrevious = null;
            var settled = pendingSettlement;
            pendingSettlement = null;
            if (settled != null) Settled?.Invoke(settled, FinaleEndingEvaluator.Evaluate(settled));
        }
        public void Defeat()
        { if (!State.active) return; State.Stop("defeated"); save(); }
    }
}
