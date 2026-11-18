namespace RewindNotary
{
    public sealed class BenefactorEncounter
    {
        public readonly string StageId, EncounterId, SkillId, LabelKey;
        public string Label => Loc.Get(LabelKey);
        private BenefactorEncounter(string stage, string encounter, string skill, string label)
        { StageId = stage; EncounterId = encounter; SkillId = skill; LabelKey = label; }
        public static readonly BenefactorEncounter[] All = {
            new BenefactorEncounter("prototype", EncounterRewardBridge.PrototypeEncounter, SkillRules.SilverStitch, "encounter.rea_echo"),
            new BenefactorEncounter("practice_julius", "prototype_julius_echo", BenefactorIds.Pendulum, "encounter.julius_echo"),
            new BenefactorEncounter("practice_valeria", "prototype_valeria_echo", BenefactorIds.Waltz, "encounter.valeria_echo"),
            new BenefactorEncounter("practice_gabriel", "prototype_gabriel_echo", BenefactorIds.Forge, "encounter.gabriel_echo"),
            new BenefactorEncounter("practice_irina", "prototype_irina_echo", BenefactorIds.Canvas, "encounter.irina_echo")
        };
        public static BenefactorEncounter ForStage(string id) => System.Array.Find(All, item => item.StageId == id) ?? All[0];
    }
}
