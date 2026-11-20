using UnityEngine;

namespace RewindNotary
{
    public sealed class BenefactorSkillRunner : MonoBehaviour
    {
        private BenefactorSkill[] skills;
        private SilverStitchSkill silver;
        private SkillSession session;
        private CombatMotor2D motor;
        public DirectionalSkillInput InputSnapshot { get; private set; }
        public bool BlocksActions
        {
            get { if (skills != null) foreach (var skill in skills) if (skill.BlocksActions) return true; return false; }
        }
        public void Initialize(SkillSession session, CombatMotor2D enemy, SilverStitchSkill stitch, GameSettings settings)
        {
            this.session = session;
            motor = GetComponent<CombatMotor2D>();
            InputSnapshot = gameObject.AddComponent<DirectionalSkillInput>();
            InputSnapshot.SetQuickSkills(settings);
            silver = stitch;
            skills = new BenefactorSkill[] { gameObject.AddComponent<PendulumRewindSkill>(),
                gameObject.AddComponent<CurtainCallWaltzSkill>(), gameObject.AddComponent<ResonanceForgeSkill>(),
                gameObject.AddComponent<CanvasCarveSkill>() };
            foreach (var skill in skills) skill.Initialize(session, enemy);
        }
        public bool CanRequest(SkillCastRequest request)
        {
            if (QuickSkills.StarterIndex(request.SkillId) >= 0) return true;
            var progress = session.Progression.Record.Get(request.SkillId);
            if (progress != null && progress.acquired) return true;
            session.ShowUnlearnedSkill(request.SkillId);
            return false;
        }
        public bool Activate(SkillCastRequest request)
        {
            if (!CanRequest(request)) return false;
            int starter = QuickSkills.StarterIndex(request.SkillId);
            if (starter >= 0) return !BlocksActions && motor.UseStarterSkill(starter);
            if (request.SkillId == SkillRules.SilverStitch) return !BlocksActions && silver.Activate();
            var chosen = System.Array.Find(skills, skill => skill.SkillId == request.SkillId);
            if (chosen == null) return false;
            foreach (var other in skills) if (other != chosen && other.BlocksActions) return false;
            return chosen is CanvasCarveSkill canvas ? canvas.Activate(request.PaintWall) : chosen.Activate();
        }

        internal float Cooldown(string id)
        {
            int starter = QuickSkills.StarterIndex(id);
            if (starter >= 0) return motor.StarterCooldown(starter);
            return 0f;
        }
        public bool DriveMovement()
        { if (skills != null) foreach (var skill in skills) if (skill.DriveMovement()) return true; return false; }
        public void Revoke(string id)
        {
            if (id == SkillRules.SilverStitch) silver.ResetAttempt();
            if (skills != null) foreach (var skill in skills) if (skill.SkillId == id) skill.Interrupt();
        }
        public void Interrupt() { if (skills != null) foreach (var skill in skills) skill.Interrupt(); }
        public void ResetAttempt() { if (skills != null) foreach (var skill in skills) skill.ResetAttempt(); }
    }
}
