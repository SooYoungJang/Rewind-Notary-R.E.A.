namespace RewindNotary
{
    // Action-completion callbacks for the authored scenes. A scene may advance only on true.
    // Replaying a completed action after an I/O error retries the same pending transaction.
    public sealed class StorySceneLinks
    {
        private readonly SkillSession session;
        private CoreRecord Record => session.Progression.Record;
        internal StorySceneLinks(SkillSession session) { this.session = session; }

        private static string District(string benefactor)
        {
            switch (benefactor)
            {
                case "mikhail": return "velvet";
                case "julius": return "clock";
                case "valeria": return "opera";
                case "gabriel": return "bell";
                case "irina": return "gallery";
                default: return null;
            }
        }
        private bool Durable(bool changed)
        {
            if (!changed && !session.LastSaveSucceeded) session.Save();
            return session.LastSaveSucceeded;
        }
        public bool CompleteMartaAtticHandoff()
        {
            if (!session.AcceptsStoryActions || Record.lastStageId != "hub") return false;
            return Durable(session.Investigation.RecordStoryClue("contract_copy", "story:marta_attic_handoff"));
        }
        public bool CompleteLukaKeepsakeGuide(string benefactor)
        {
            if (!session.AcceptsStoryActions || Record.lastStageId != District(benefactor)) return false;
            return Durable(session.Investigation.RecordStoryClue("imprint_" + benefactor, "story:luka_keepsake:" + benefactor));
        }
        public bool CompleteDuelHelp(string benefactor) => CompleteHelp(benefactor, false);
        public bool CompleteShelterHelp(string benefactor) => CompleteHelp(benefactor, true);
        private bool CompleteHelp(string benefactor, bool atShelter)
        {
            var district = District(benefactor);
            if (!session.AcceptsStoryActions || district == null || (Record.lastStageId != district && !(atShelter && Record.lastStageId == "hub"))
                || !Record.storyFlags.Contains(InvestigationService.MiddleInvestigationFlag)) return false;
            var trial = System.Array.Find(BenefactorTrialCatalog.Load().trials, t => t.id == benefactor);
            return Durable(session.Progression.Trials.RecordStoryHelp(trial, atShelter));
        }
    }
}
