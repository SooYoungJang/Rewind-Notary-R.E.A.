using UnityEngine;

namespace RewindNotary
{
    public sealed class BenefactorSkillRunner : MonoBehaviour
    {
        private BenefactorSkill[] skills;
        private SilverStitchSkill silver;
        public int Selected { get; private set; }
        public string SelectedId => BenefactorIds.All[Selected];
        public bool BlocksActions
        {
            get { if (skills != null) foreach (var skill in skills) if (skill.BlocksActions) return true; return false; }
        }
        public void Initialize(SkillSession session, CombatMotor2D enemy, SilverStitchSkill stitch)
        {
            silver = stitch;
            skills = new BenefactorSkill[] { gameObject.AddComponent<PendulumRewindSkill>(),
                gameObject.AddComponent<CurtainCallWaltzSkill>(), gameObject.AddComponent<ResonanceForgeSkill>(),
                gameObject.AddComponent<CanvasCarveSkill>() };
            foreach (var skill in skills) skill.Initialize(session, enemy);
        }
        public void SelectInput()
        {
            int direct = GameInput.SkillSelection;
            if (direct >= 0) Selected = direct;
            else if (GameInput.NextSkillDown) Selected = (Selected + 1) % BenefactorIds.All.Length;
        }
        public bool Activate()
        {
            if (Selected == 0) return !BlocksActions && silver.Activate();
            var chosen = skills[Selected - 1];
            foreach (var other in skills) if (other != chosen && other.BlocksActions) return false;
            return chosen.Activate();
        }
        public bool DriveMovement()
        { if (skills != null) foreach (var skill in skills) if (skill.DriveMovement()) return true; return false; }
        public void Interrupt() { if (skills != null) foreach (var skill in skills) skill.Interrupt(); }
        public void ResetAttempt() { if (skills != null) foreach (var skill in skills) skill.ResetAttempt(); }
    }
}
