using System;

namespace RewindNotary
{
    // Transient display values only. Always read the current Record (debug fixtures replace it).
    public sealed class SkillStatusView
    {
        public readonly string SkillId, Name, StageText, ProgressText, FormText;
        public readonly bool Owned, Overridden;
        public readonly int ActualStage, DisplayStage, Uses, Remaining;
        private SkillStatusView(SkillDefinition definition, SkillProgress progress, int preview)
        {
            SkillId = definition.skillId; Name = definition.DisplayName;
            Owned = progress != null && progress.acquired;
            Overridden = Owned && preview != 0;
            Uses = progress?.validUseCount ?? 0;
            ActualStage = Owned ? progress.stage : 0;
            DisplayStage = Owned ? (Overridden ? preview : ActualStage) : 0;
            Remaining = !Owned || ActualStage == 3 ? 0
                : Math.Max(0, (ActualStage == 1 ? definition.stage2At : definition.stage3At) - Uses);
            if (!Owned)
            {
                StageText = Loc.Get("skills.locked_unowned");
                ProgressText = Loc.Get("skills.unowned_progress", Uses);
                FormText = Loc.Get("skills.acquire_hint");
                return;
            }
            StageText = Loc.Get("skills.current_stage", StageLabel(DisplayStage), (Overridden ? Loc.Get("skills.override_suffix", StageLabel(ActualStage)) : ""));
            ProgressText = Loc.Get("skills.effective_uses", Uses, (ActualStage == 3 ? Loc.Get("skills.growth_complete") : Loc.Get("skills.remaining_uses", StageLabel(ActualStage + 1), Remaining)));
            FormText = definition.Profile(DisplayStage).Name + " — " + Describe(SkillId, DisplayStage);
        }
        public static SkillStatusView[] Read(SkillSession session)
        {
            var service = session.Progression;
            var definitions = service.Catalog.skills;
            var rows = new SkillStatusView[definitions.Length];
            for (int i = 0; i < definitions.Length; i++)
                rows[i] = new SkillStatusView(definitions[i], service.Record.Get(definitions[i].skillId), session.PreviewStage);
            return rows;
        }
        public static string Mode(SkillSession session)
        {
            string storage = session.Progression.Record.testData ? Loc.Get("skills.test_save_mode") : Loc.Get("slot.name", session.ActiveSlot);
            return storage + (session.PreviewStage == 0 ? Loc.Get("skills.actual_mode_suffix")
                : Loc.Get("skills.override_mode_suffix", StageLabel(session.PreviewStage)));
        }
        public static string StageLabel(int stage) => stage == 3 ? Loc.Get("skills.ultimate_stage") : Loc.Get("skills.stage", stage);
        public static bool IsEvolution(ProgressionEvent result, SkillCatalog catalog)
        {
            if (result.kind != "stage_up" || string.IsNullOrEmpty(result.skillId)
                || string.IsNullOrEmpty(result.useId) || result.before < 0 || result.after <= result.before
                || result.after - result.before != 1) return false;
            var definition = catalog.Get(result.skillId);
            return result.stage == definition.Stage(result.after)
                && definition.Stage(result.before) < result.stage;
        }
        public static string Describe(string id, int stage)
        {
            switch (id)
            {
                case SkillRules.BasicSword: return stage == 1 ? Loc.Get("skills.sword.form1")
                    : stage == 2 ? Loc.Get("skills.sword.form2") : Loc.Get("skills.sword.form3");
                case SkillRules.SilverStitch: return stage == 1 ? Loc.Get("skills.stitch.form1")
                    : stage == 2 ? Loc.Get("skills.stitch.form2") : Loc.Get("skills.stitch.form3");
                case BenefactorIds.Pendulum: return stage == 1 ? Loc.Get("skills.pendulum.form1")
                    : stage == 2 ? Loc.Get("skills.pendulum.form2") : Loc.Get("skills.pendulum.form3");
                case BenefactorIds.Waltz: return stage == 1 ? Loc.Get("skills.waltz.form1")
                    : stage == 2 ? Loc.Get("skills.waltz.form2") : Loc.Get("skills.waltz.form3");
                case BenefactorIds.Forge: return stage == 1 ? Loc.Get("skills.forge.form1")
                    : stage == 2 ? Loc.Get("skills.forge.form2") : Loc.Get("skills.forge.form3");
                case BenefactorIds.Canvas: return stage == 1 ? Loc.Get("skills.canvas.form1")
                    : stage == 2 ? Loc.Get("skills.canvas.form2") : Loc.Get("skills.canvas.form3");
                default: return Loc.Get("skills.no_form_info");
            }
        }
    }
}
