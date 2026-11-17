namespace RewindNotary
{
    public sealed class BenefactorEncounter
    {
        public readonly string StageId, EncounterId, SkillId, Label;
        private BenefactorEncounter(string stage, string encounter, string skill, string label)
        { StageId = stage; EncounterId = encounter; SkillId = skill; Label = label; }
        public static readonly BenefactorEncounter[] All = {
            new BenefactorEncounter("prototype", EncounterRewardBridge.PrototypeEncounter, SkillRules.SilverStitch, "잔향 · 어제의 레아"),
            new BenefactorEncounter("practice_julius", "prototype_julius_echo", BenefactorIds.Pendulum, "율리우스 기술 잔향 · 시험 대련"),
            new BenefactorEncounter("practice_valeria", "prototype_valeria_echo", BenefactorIds.Waltz, "발레리아 기술 잔향 · 시험 대련"),
            new BenefactorEncounter("practice_gabriel", "prototype_gabriel_echo", BenefactorIds.Forge, "가브리엘 기술 잔향 · 시험 대련"),
            new BenefactorEncounter("practice_irina", "prototype_irina_echo", BenefactorIds.Canvas, "이리나 기술 잔향 · 시험 대련")
        };
        public static BenefactorEncounter ForStage(string id) => System.Array.Find(All, item => item.StageId == id) ?? All[0];
    }
}
