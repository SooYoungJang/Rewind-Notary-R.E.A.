namespace RewindNotary
{
    public sealed class StageEntry
    {
        public readonly string Id, NameKey, UnlockFlag;
        public string Name => Loc.Get(NameKey);
        public readonly bool Implemented;
        public StageEntry(string id, string name, string flag, bool implemented = false)
        { Id = id; NameKey = name; UnlockFlag = flag; Implemented = implemented; }
        public bool Unlocked(CoreRecord record)
        {
            if (Id == "janhyang") return RepresentativeScript.CourtEntry(record) || InvestigationService.CanEnterCassel(record);
            if (Id == "cassel" && !InvestigationService.CanEnterCassel(record)) return false;
            var trial = BenefactorTrialCatalog.Load().ForStage(Id);
            if (trial != null) return trial.Unlocked(record);
            return string.IsNullOrEmpty(UnlockFlag) || (record != null && record.storyFlags.Contains(UnlockFlag));
        }
        public string Status(CoreRecord record)
        {
            if (Id == "cassel" && Unlocked(record) && record.Get(SkillRules.BasicSword).stage < 3)
                return Loc.Get("stage.finale_survival_locked");
            var trial = BenefactorTrialCatalog.Load().ForStage(Id);
            if (trial != null && record != null) return BenefactorTrialProgress.Status(record, trial);
            return !Unlocked(record) ? Loc.Get("stage.locked_unavailable") : Implemented ? Loc.Get("stage.playable") : Loc.Get("stage.coming_soon");
        }
    }
    public static class StageCatalog
    {
        // Flags are narrative hooks, never inferred from mastery counts or prototype boss victory.
        public static readonly StageEntry[] All = {
            new StageEntry("hub", "stage.hub.name", "hub_open", true),
            new StageEntry("velvet", "stage.velvet.name", "districts_open", true),
            new StageEntry("clock", "stage.clock.name", "districts_open", true),
            new StageEntry("opera", "stage.opera.name", "districts_open", true),
            new StageEntry("bell", "stage.bell.name", "districts_open", true),
            new StageEntry("gallery", "stage.gallery.name", "districts_open", true),
            new StageEntry("janhyang", "stage.echo.name", "janhyang_open", true),
            new StageEntry("cassel", "stage.cassel.name", "cassel_open", true),
            new StageEntry("prototype", "stage.prototype.name", "", true),
            new StageEntry("practice_julius", "stage.practice_julius.name", "", true),
            new StageEntry("practice_valeria", "stage.practice_valeria.name", "", true),
            new StageEntry("practice_gabriel", "stage.practice_gabriel.name", "", true),
            new StageEntry("practice_irina", "stage.practice_irina.name", "", true),
            new StageEntry("skill_lab", "stage.skill_lab.name", "", true),
            new StageEntry("wave_test", "stage.wave_test.name", "", true),
            new StageEntry("trial_mikhail", "stage.trial_mikhail.name", "", true),
            new StageEntry("trial_julius", "stage.trial_julius.name", "", true),
            new StageEntry("trial_valeria", "stage.trial_valeria.name", "", true),
            new StageEntry("trial_gabriel", "stage.trial_gabriel.name", "", true),
            new StageEntry("trial_irina", "stage.trial_irina.name", "", true)
        };
        public static StageEntry Find(string id) => System.Array.Find(All, entry => entry.Id == id);
    }
}
