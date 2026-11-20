using UnityEngine;

namespace RewindNotary
{
    public readonly struct SkillCastRequest
    {
        public readonly string SkillId;
        public readonly bool PaintWall;
        public SkillCastRequest(string skillId, bool paintWall) { SkillId = skillId; PaintWall = paintWall; }
    }

    // Sample before defense (-75) can change Facing; dispatch still follows combat (-50).
    [DefaultExecutionOrder(-90)]
    public sealed class DirectionalSkillInput : MonoBehaviour
    {
        private CombatMotor2D motor;
        private GameSettings settings;
        private int sampledFrame = -1;
        private bool castDown;
        public SkillCastRequest Request { get; private set; }
        public string SupportSkillId { get; private set; } = SkillRules.SilverStitch;
        public bool CastDown => sampledFrame == Time.frameCount && castDown;
        private void Awake() { motor = GetComponent<CombatMotor2D>(); }
        internal void SetQuickSkills(GameSettings value) { settings = value; }
        private void Update()
        {
            sampledFrame = Time.frameCount; castDown = false;
            if (!GameInput.CanPlay || motor.Health == null || motor.Health.Dead) return;
            SupportSkillId = GameInput.ResolveSkill(GameInput.SkillDirection, motor.Facing, motor.Grounded);
            castDown = GameInput.CastDown;
            if (castDown)
            {
                int slot = GameInput.QuickSkillDown;
                string id = slot < 0 ? SupportSkillId : QuickSkills.Equipped(settings, motor.Skills.Progression.Record, slot);
                Request = new SkillCastRequest(id, GameInput.PaintWallHeld);
            }
        }
    }
}
