using System;
using System.Collections.Generic;

namespace RewindNotary
{
    [Serializable] public sealed class InvestigationState
    {
        public List<string> clueIds = new List<string>();
        public List<string> readStageIds = new List<string>();
        public List<string> confirmedEntryIds = new List<string>();
        public void Validate()
        {
            // Unknown nonempty IDs survive round trips for content compatibility. Only catalog IDs count.
            ValidateIds(clueIds); ValidateIds(readStageIds); ValidateIds(confirmedEntryIds);
        }
        public void ValidateProgress(InvestigationCatalog catalog)
        {
            foreach (var stage in catalog.stages)
            {
                bool gap = false; int confirmed = 0;
                foreach (var entry in stage.entries)
                {
                    bool has = confirmedEntryIds.Contains(entry.id);
                    if (has && gap) throw new InvalidOperationException("Non-sequential core reading receipts");
                    if (has) confirmed++; else gap = true;
                }
                if (readStageIds.Contains(stage.id) != (confirmed == stage.entries.Length))
                    throw new InvalidOperationException("Incomplete core reading transaction");
                if (confirmed > 0)
                    foreach (var condition in stage.gate.all)
                        if (condition.kind == "stage_read" && !readStageIds.Contains(condition.subject))
                            throw new InvalidOperationException("Missing prior core reading");
            }
        }
        private static void ValidateIds(List<string> values)
        {
            if (values == null) throw new InvalidOperationException("Incomplete investigation state");
            var unique = new HashSet<string>();
            foreach (string value in values)
                if (string.IsNullOrWhiteSpace(value) || !unique.Add(value))
                    throw new InvalidOperationException("Invalid/duplicate investigation receipt");
        }
    }

    public sealed class InvestigationService
    {
        public const string MiddleInvestigationFlag = "investigation_complete";
        public const string EchoDefeatedFlag = "janhyang_defeated";
        public const string OrderWithdrawnFlag = "janhyang_order_withdrawn";
        private readonly SkillProgressionService progression;
        private readonly Action save;
        public InvestigationCatalog Catalog { get; }
        // Always read the active record: debug seed/copy can replace it during the same session.
        public CoreRecord Record => progression.Record;
        public InvestigationState State => Record.investigation;
        public event Action<string> TextKeyRequested;
        public event Action FinalReadingCompleted;
        public InvestigationService(SkillProgressionService owner, InvestigationCatalog catalog, Action persist)
        { progression = owner; Catalog = catalog; save = persist; }
        private const string OriginalReceipt = "story:core_contract_original";
        private bool AtLateReturn => Record.lastStageId == "hub" && LateInvestigation;
        private bool LateInvestigation => Record.storyFlags.Contains(MiddleInvestigationFlag)
            && Record.ledger.FindAll(e => e.transferredToRea && e.ownerId == "rea").Count == 5;
        public bool CanRevealClue(string id) => id != "contract_original" || Record.lastStageId == "prototype"
            || (LateInvestigation && (Record.completedEncounters.Contains(OriginalReceipt)
                || HasRead("contract_comparison") || State.confirmedEntryIds.Contains(Catalog.Stage("contract_comparison").entries[0].id)));
        public bool HasClue(string id) => CanRevealClue(id) && State.clueIds.Contains(id);
        public bool HasRead(string id) => State.readStageIds.Contains(id);
        public int ClueCount(string topic = null)
        {
            int count = 0;
            foreach (var c in Catalog.clues) if ((topic == null || c.topicId == topic) && HasClue(c.clueId)) count++;
            return count;
        }
        public int TopicTotal(string topic) => Array.FindAll(Catalog.clues, c => c.topicId == topic).Length;
        public int ReadCount => Array.FindAll(Catalog.stages, s => HasRead(s.id)).Length;
        public string ProgressLabel => Loc.Get("investigation.progress", ClueCount(), Catalog.clues.Length, ReadCount, Catalog.stages.Length);
        public bool TryCollect(string id)
        {
            return Record.lastStageId == "prototype" && CollectOnce(id);
        }
        internal bool RecordStoryClue(string id, string receipt)
        {
            if (Catalog.Clue(id) == null) throw new InvalidOperationException("Unknown story clue: " + id);
            if (Record.completedEncounters.Contains(receipt)) return false;
            Record.completedEncounters.Add(receipt);
            if (!CollectOnce(id)) save(); // Imported clue already owned; persist just its scene receipt.
            return true;
        }
        internal bool OffersOriginal(bool atCore, string stageId) => atCore && stageId == "contract_comparison"
            && AtLateReturn && !Record.completedEncounters.Contains(OriginalReceipt);
        public void PrepareStoryComparison(bool atCore, string stageId)
        {
            if (OffersOriginal(atCore, stageId)) RecordStoryClue("contract_original", OriginalReceipt);
        }
        private bool CollectOnce(string id)
        {
            var clue = Catalog.Clue(id);
            if (clue == null || State.clueIds.Contains(id)) return false;
            State.clueIds.Add(id); save();
            progression.EmitSimple("investigation_clue", "", id);
            RequestText(clue.summaryKey); RequestText(clue.textKey); return true;
        }
        public bool Matches(InvestigationCondition c)
        {
            if (c == null) return false;
            switch (c.kind)
            {
                case "clue": return HasClue(c.subject);
                case "clue_percent":
                    int total = TopicTotal(c.subject);
                    return total > 0 && ClueCount(c.subject) * 100 >= total * c.amount;
                case "transfers": return Record.ledger.FindAll(e => e.transferredToRea && e.ownerId == "rea").Count >= c.amount;
                case "skill_stage":
                    var skill = Record.Get(c.subject);
                    return skill != null && skill.acquired && progression.Catalog.Get(c.subject).Stage(skill.validUseCount) >= c.amount;
                case "trial_complete":
                    var trial = Array.Find(BenefactorTrialCatalog.Load().trials, t => t.id == c.subject);
                    return trial != null && BenefactorTrialProgress.HasTrialProof(Record, trial);
                case "stage_read": return HasRead(c.subject);
                case "story_flag": return Record.storyFlags.Contains(c.subject);
                default: return false;
            }
        }
        public bool CanRead(CoreReadingStage stage)
        {
            if (stage == null) return false;
            if (HasRead(stage.id)) return true; // Replay never re-grants completion.
            return Array.TrueForAll(stage.gate.all, Matches)
                && (stage.gate.any.Length == 0 || Array.Exists(stage.gate.any, Matches));
        }
        public int NextEntry(CoreReadingStage stage)
        {
            for (int i = 0; i < stage.entries.Length; i++)
                if (!State.confirmedEntryIds.Contains(stage.entries[i].id)) return i;
            return 0;
        }
        public bool TryConfirmEntry(string stageId, int index, bool atCore)
        {
            var stage = Catalog.Stage(stageId);
            if (!atCore || !CanRead(stage) || index < 0 || index >= stage.entries.Length || HasRead(stageId)
                || index != NextEntry(stage)) return false;
            var entry = stage.entries[index];
            if (State.confirmedEntryIds.Contains(entry.id)) return false;
            State.confirmedEntryIds.Add(entry.id);
            bool completed = index == stage.entries.Length - 1;
            if (completed) State.readStageIds.Add(stageId);
            save(); // Entry receipts and completed stage share the existing atomic save/retry transaction.
            progression.EmitSimple(completed ? "core_read_complete" : "core_entry_confirmed", "", entry.id);
            if (completed && stageId == Catalog.finalStageId) FinalReadingCompleted?.Invoke();
            return true;
        }
        public void RequestText(string key)
        { progression.EmitSimple("investigation_text_key", "", key); TextKeyRequested?.Invoke(key); }
        // Called by the real CARD-06 sequence only, after investigation AND sword experiment complete.
        // Collecting clues / reading the index / winning a prototype duel never invokes this hook.
        public bool CompleteMiddleInvestigation()
        {
            if (!Record.ledger.Exists(e => e.transferredToRea && e.ownerId == "rea")) return false;
            if (Record.storyFlags.Contains(MiddleInvestigationFlag)) return false;
            progression.SetStoryFlag(MiddleInvestigationFlag, true); return true;
        }
        public bool FinalReadingComplete => HasRead(Catalog.finalStageId);
        public static bool CanEnterCassel(CoreRecord record)
        {
            return record != null && record.investigation != null
                && record.investigation.readStageIds.Contains(InvestigationCatalog.Load().finalStageId)
                && record.storyFlags.Contains(MiddleInvestigationFlag)
                && record.ledger.FindAll(e => e.transferredToRea && e.ownerId == "rea").Count == 5
                && record.storyFlags.Contains(EchoDefeatedFlag) && record.storyFlags.Contains(OrderWithdrawnFlag);
        }
        public string ConditionLabel(InvestigationCondition c)
        {
            switch (c.kind)
            {
                case "clue": return Loc.Get("investigation.collect_condition", Catalog.Clue(c.subject).Label);
                case "clue_percent": return Catalog.Topic(c.subject).Label + " " + c.amount + "% (" + ClueCount(c.subject) + "/" + TopicTotal(c.subject) + ")";
                case "transfers": return Loc.Get("investigation.transfers_condition", c.amount);
                case "skill_stage": return Loc.Get("investigation.skill_condition", progression.Catalog.Get(c.subject).DisplayName, c.amount);
                case "trial_complete": return Loc.Get("investigation.trial_condition", Array.Find(BenefactorTrialCatalog.Load().trials, t => t.id == c.subject).DisplayName);
                case "stage_read": return Loc.Get("investigation.read_condition", Catalog.Stage(c.subject).Label);
                case "story_flag": return Loc.Get("investigation.mid_condition");
                default: return Loc.Get("investigation.unknown_condition");
            }
        }
        public string GateLabel(CoreReadingStage stage)
        {
            Func<InvestigationCondition, string> label = c => (Matches(c) ? Loc.Get("investigation.done_prefix") : Loc.Get("investigation.pending_prefix")) + ConditionLabel(c);
            string all = string.Join("\n", Array.ConvertAll(stage.gate.all, c => label(c)));
            string any = stage.gate.any.Length == 0 ? "" : Loc.Get("investigation.any_conditions", string.Join("\n", Array.ConvertAll(stage.gate.any, c => label(c))));
            return Loc.Get("investigation.all_conditions", all, any);
        }
    }
}
