using System;

namespace RewindNotary
{
    // Bounded encounter receipts share the existing atomic slot transaction. No narrative flags,
    // skill ownership, schema version, or doom ledger are repurposed. Read Record dynamically:
    // debug copies/seeds and slot replacement must never retain a reference to the main record.
    public sealed class BenefactorTrialProgress
    {
        private readonly SkillProgressionService service;
        public BenefactorTrialDefinition Active { get; private set; }
        private bool storyTrial;
        public bool OrdinaryCombat { get; private set; }
        public event Action<string> TextKeyRequested;
        public string LastTextKey { get; private set; }
        public event Action Verified;
        public BenefactorTrialProgress(SkillProgressionService owner) { service = owner; }
        private static string Key(BenefactorTrialDefinition d, string suffix) => "benefactor:v1:" + d.id + ":" + suffix;
        public static bool Has(CoreRecord r, BenefactorTrialDefinition d, string suffix) => r.completedEncounters.Contains(Key(d, suffix));
        public static int Count(CoreRecord r, BenefactorTrialDefinition d, string bucket, int limit)
        {
            int n = 0;
            for (int i = 0; i < limit; i++) if (Has(r, d, bucket + ":" + i)) n++;
            return n;
        }
        private void Put(BenefactorTrialDefinition d, string suffix)
        { string key = Key(d, suffix); if (!service.Record.completedEncounters.Contains(key)) service.Record.completedEncounters.Add(key); }
        private void Increment(BenefactorTrialDefinition d, string bucket, int limit)
        {
            for (int i = 0; i < limit; i++)
                if (!Has(service.Record, d, bucket + ":" + i)) { Put(d, bucket + ":" + i); return; }
        }
        // Pre-story slots could only obtain "unlocked" by completing an actual practice trial.
        public static bool HasTrialProof(CoreRecord record, BenefactorTrialDefinition d)
            => Has(record, d, "verified") || (Has(record, d, "unlocked") && !Has(record, d, "story_grant"));
        internal bool RecordStoryHelp(BenefactorTrialDefinition d, bool atShelter)
        {
            string scene = atShelter ? "help_shelter" : "help_duel";
            if (Has(service.Record, d, scene)) return false;
            Put(d, scene);
            if (!Has(service.Record, d, "unlocked"))
            {
                // A partly played practice session may already have banked ordinary uses.
                service.Record.completedEncounters.RemoveAll(k => k.StartsWith(Key(d, "field:"), StringComparison.Ordinal));
                Put(d, "story_grant"); Put(d, "unlocked"); Put(d, "charged");
                Put(d, "started"); Put(d, "rewarded");
            }
            service.NotifyTrialChanged();
            return true;
        }
        public void Configure(string stageId, bool storyTrial = false, bool resume = false)
        {
            this.storyTrial = storyTrial;
            Active = BenefactorTrialCatalog.Load().ForStage(stageId);
            OrdinaryCombat = stageId == "prototype" || stageId == "velvet" || stageId == "clock" || stageId == "opera"
                || stageId == "bell" || stageId == "gallery" || stageId == "janhyang" || stageId == "cassel";
            if (Active == null) return;
            if (!Active.Unlocked(service.Record)) throw new InvalidOperationException(Loc.Get("trial.acquire_first"));
            var d = Active;
            if (resume) return;
            if (Count(service.Record, d, "field", d.normalUsesToRenew) >= d.normalUsesToRenew)
            {
                // A session spans retries, exits and app reloads. Only ordinary use earns renewal.
                service.Record.completedEncounters.RemoveAll(k => k.StartsWith(Key(d, "credit:"), StringComparison.Ordinal)
                    || k.StartsWith(Key(d, "success:"), StringComparison.Ordinal) || k.StartsWith(Key(d, "field:"), StringComparison.Ordinal)
                    || k == Key(d, "rewarded"));
            }
            Put(d, "started"); service.NotifyTrialChanged();
            RequestText(d.dialogue.enterKey);
        }
        // Caller has checked owner/epoch/closed/preview/Applied/TargetId, independently of growth cap.
        public void ObserveEffective(UseContext use, CombatEffectResult result)
        {
            var d = Active;
            if (d == null || use.TrialQualified || use.SkillId != d.skillId || (!d.Qualifies(result) &&
                !(storyTrial && d.skillId == BenefactorIds.Waltz && result.TargetId == d.opponentId))) return;
            use.TrialQualified = true;
            bool alreadyRewarded = Has(service.Record, d, "rewarded");
            if (alreadyRewarded && HasTrialProof(service.Record, d)) return;
            Increment(d, "success", d.requiredSuccesses);
            bool verified = Count(service.Record, d, "success", d.requiredSuccesses) >= d.requiredSuccesses;
            if (verified) Put(d, "verified");
            if (verified && !alreadyRewarded && !storyTrial) { Put(d, "unlocked"); Put(d, "charged"); Put(d, "rewarded"); }
            service.NotifyTrialChanged();
            service.EmitSimple(verified ? "trial_verified" : "trial_success", d.skillId, d.id);
            if (verified) { RequestText(d.dialogue.successKey); Verified?.Invoke(); }
        }
        public bool ConfirmStoryRecharge(BenefactorTrialDefinition d)
        {
            if (!storyTrial || d == null || Active != d || Count(service.Record, d, "success", d.requiredSuccesses) < d.requiredSuccesses)
                return false;
            if (Has(service.Record, d, "rewarded")) return true;
            Put(d, "unlocked"); Put(d, "charged"); Put(d, "rewarded");
            service.NotifyTrialChanged();
            return true;
        }
        // Exactly once after the unchanged effective-use guards; never called for repeat/preview effects.
        public bool PermitGrowth(UseContext use, CombatEffectResult result)
        {
            if (Active != null)
            {
                if (Count(service.Record, Active, "credit", Active.sessionCap) >= Active.sessionCap)
                { use.Counted = true; return false; }
                Increment(Active, "credit", Active.sessionCap);
            }
            else if (OrdinaryCombat)
            {
                var d = BenefactorTrialCatalog.Load().ForSkill(use.SkillId);
                // Practice objects cannot mint renewal. Only the live opponent's actual effects count.
                if (d != null && d.renewalCondition == "ordinary_combat_effect" && Has(service.Record, d, "started")
                    && (result.Kind == EffectTargetKind.Enemy || result.TargetId == "forge_counter"
                    || result.TargetId.EndsWith(":time_stop", StringComparison.Ordinal)))
                    Increment(d, "field", d.normalUsesToRenew);
            }
            return true;
        }
        public bool ReserveSupport(BenefactorTrialDefinition d)
        {
            if (!OrdinaryCombat || Active != null || !Has(service.Record, d, "charged")) return false;
            service.Record.completedEncounters.Remove(Key(d, "charged")); service.NotifyTrialChanged(); return true;
        }
        public void RefundSupport(BenefactorTrialDefinition d) { Put(d, "charged"); service.NotifyTrialChanged(); }
        public void RequestText(string key)
        { LastTextKey = key; service.EmitSimple("trial_text_key", Active?.skillId ?? "", key); TextKeyRequested?.Invoke(key); }
        public static string Status(CoreRecord r, BenefactorTrialDefinition d)
        {
            string support = Has(r, d, "charged") ? Loc.Get("support.charged") : Has(r, d, "unlocked")
                ? Loc.Get("support.recharge_progress", Count(r, d, "field", d.normalUsesToRenew), d.normalUsesToRenew) : Loc.Get("support.locked");
            return (d.Unlocked(r) ? Loc.Get("trial.unlocked") : Loc.Get("trial.locked_skill")) + " · " + support;
        }
        public string SessionStatus => Active == null ? "" : Loc.Get("trial.session_status", Count(service.Record, Active, "credit", Active.sessionCap), Active.sessionCap, Count(service.Record, Active, "success", Active.requiredSuccesses), Active.requiredSuccesses, Count(service.Record, Active, "field", Active.normalUsesToRenew), Active.normalUsesToRenew);
    }
}
