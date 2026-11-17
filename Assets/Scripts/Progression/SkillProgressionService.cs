using System;

namespace RewindNotary
{
    public enum EffectTargetKind { Enemy, Destructible, TerrainMechanism }
    public readonly struct CombatEffectResult
    {
        public readonly EffectTargetKind Kind;
        public readonly string TargetId;
        public readonly bool Applied;
        public readonly int Damage;
        public CombatEffectResult(EffectTargetKind kind, string targetId, bool applied, int damage = 0)
        { Kind = kind; TargetId = targetId; Applied = applied; Damage = damage; }
    }
    public sealed class UseContext
    {
        public string Id { get; internal set; }
        public string SkillId { get; internal set; }
        public int Stage { get; internal set; }
        public bool Preview { get; internal set; }
        internal SkillProgressionService Owner;
        internal int Epoch;
        internal bool Counted, Closed;
    }
    [Serializable]
    public sealed class ProgressionEvent
    {
        public string kind, skillId, useId, targetId, reason;
        public int stage, before, after, damage;
        public bool testData;
    }
    public sealed class SkillProgressionService
    {
        public SkillCatalog Catalog { get; }
        public CoreRecord Record { get; private set; }
        public event Action<ProgressionEvent> Event;
        public event Action Changed;
        private int epoch;
        private long nextUse;
        private readonly string sessionId = Guid.NewGuid().ToString("N");
        public SkillProgressionService(SkillCatalog catalog, CoreRecord record)
        { Catalog = catalog; record.Validate(catalog); Record = record; }
        public UseContext BeginUse(string skillId, int previewStage = 0)
        {
            var progress = Record.Get(skillId);
            if (progress == null || !progress.acquired || !Catalog.Get(skillId).implemented) return null;
            return new UseContext { Id = sessionId + ":" + (++nextUse), SkillId = skillId,
                Stage = previewStage >= 1 && previewStage <= 3 ? previewStage : progress.stage,
                Preview = previewStage != 0, Owner = this, Epoch = epoch };
        }
        public bool Apply(UseContext use, CombatEffectResult result)
        {
            if (use == null) return false;
            if (use.Owner != this) return false;
            string reason = use.Owner != this || use.Epoch != epoch || use.Closed ? "stale_or_cancelled"
                : !result.Applied || string.IsNullOrEmpty(result.TargetId) ? "no_state_change"
                : use.Counted ? "same_activation" : use.Preview ? "stage_preview" : "accepted";
            var progress = Record.Get(use.SkillId);
            int before = progress.validUseCount;
            if (reason != "accepted") { Emit("use_rejected", use, result, reason, before, before); return false; }
            if (before >= int.MaxValue - 1) return false;
            use.Counted = true;
            int previousStage = progress.stage;
            progress.validUseCount++;
            progress.stage = Catalog.Get(use.SkillId).Stage(progress.validUseCount);
            // Mutate count, stage and ledger as one transaction BEFORE notifying save/UI listeners.
            bool mastery = previousStage < 3 && progress.stage == 3;
            if (mastery)
            {
                if (use.SkillId == SkillRules.BasicSword) Record.basicMasteryReached = true;
                else
                {
                    var entry = Record.ledger.Find(e => e.skillId == use.SkillId);
                    entry.ownerId = "rea"; entry.transferredToRea = true;
                    entry.transferEventId = "mastery:" + use.SkillId;
                    Record.firstTransferPending = true;
                }
            }
            Changed?.Invoke();
            Emit("valid_use", use, result, "accepted", before, progress.validUseCount);
            if (progress.stage != previousStage) Emit("stage_up", use, result, "embodiment", before, progress.validUseCount);
            if (mastery) Emit(use.SkillId == SkillRules.BasicSword ? "basic_mastery" : "doom_transfer",
                use, result, "presentation_hook", before, progress.validUseCount);
            return true;
        }
        public void EndUse(UseContext use)
        {
            if (use == null || use.Owner != this || use.Closed) return;
            if (!use.Counted) Emit("use_ended", use, default, "no_valid_effect_or_preview", 0, 0);
            use.Closed = true;
        }
        public void ResetAttempt() { epoch++; EmitSimple("attempt_reset", "", "progress_retained"); }
        public bool CompleteEncounter(string encounterId, string rewardSkillId)
        {
            var progress = Record.Get(rewardSkillId);
            if (progress == null || !Catalog.Get(rewardSkillId).implemented || string.IsNullOrEmpty(encounterId)) return false;
            if (Record.completedEncounters.Contains(encounterId)) return false;
            Record.completedEncounters.Add(encounterId);
            bool newlyAcquired = !progress.acquired;
            if (newlyAcquired) { progress.acquired = true; progress.acquiredEncounterId = encounterId; }
            Changed?.Invoke();
            EmitSimple(newlyAcquired ? "acquired" : "encounter_complete", rewardSkillId, encounterId);
            return newlyAcquired;
        }
        public void EnableDebugRecord()
        {
            if (Record.testData) return;
            // Copy first: debug injection/preview can never write the main slot.
            Record = UnityEngine.JsonUtility.FromJson<CoreRecord>(UnityEngine.JsonUtility.ToJson(Record));
            Record.testData = true; Record.slotId = "debug"; ResetAttempt(); Changed?.Invoke();
        }
        public void SeedBoundary(bool mastery)
        {
            EnableDebugRecord();
            // A new debug fixture, never an edit of a completed main ledger.
            Record = CoreRecord.NewGame(Catalog); Record.testData = true; Record.slotId = "debug";
            Record.Get(SkillRules.BasicSword).validUseCount = mastery ? 399 : 150;
            Record.Get(SkillRules.BasicSword).stage = mastery ? 2 : 1;
            var silver = Record.Get(SkillRules.SilverStitch);
            silver.acquired = true; silver.acquiredEncounterId = "debug_fixture";
            silver.validUseCount = mastery ? 79 : 29; silver.stage = mastery ? 2 : 1;
            Record.Validate(Catalog); ResetAttempt(); Changed?.Invoke();
            EmitSimple("debug_seed", "", mastery ? "399/79" : "150/29");
        }
        public void EmitSimple(string kind, string skillId, string reason)
        {
            Event?.Invoke(new ProgressionEvent { kind = kind, skillId = skillId, reason = reason,
                stage = Record.Get(skillId)?.stage ?? 0, testData = Record.testData });
        }
        private void Emit(string kind, UseContext use, CombatEffectResult result, string reason, int before, int after)
        {
            Event?.Invoke(new ProgressionEvent { kind = kind, skillId = use.SkillId, useId = use.Id,
                stage = Record.Get(use.SkillId).stage, targetId = result.TargetId, reason = reason,
                before = before, after = after, damage = result.Damage, testData = Record.testData || use.Preview });
        }
    }
}
