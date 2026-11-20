using UnityEngine;
using Input = RewindNotary.GameInputDevice;

namespace RewindNotary
{
    // Legacy Input Manager, Xbox / Windows joystick layout. Keyboard remains digital and unsmoothed.
    public static class GameInput
    {
        public static bool GameplayEnabled { get; private set; }
        private static int suppressedFrame;
        public static bool CanPlay => GameplayEnabled && Time.frameCount > suppressedFrame;
        public static void SetGameplay(bool enabled) { GameplayEnabled = enabled; suppressedFrame = Time.frameCount; }
        private static float KeyboardHorizontal =>
            (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        private static float KeyboardVertical =>
            (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
        public static float Horizontal
        {
            get
            {
                float keyboard = KeyboardHorizontal;
                return keyboard != 0 ? keyboard : PadAxis("PadHorizontal", "PadDpadHorizontal");
            }
        }
        private static float Stronger(float a, float b) => Mathf.Abs(a) >= Mathf.Abs(b) ? a : b;
        private const float PadDeadZone = 0.25f;
        private static float PadAxis(string stick, string dpad)
        {
            float value = Stronger(Input.GetAxisRaw(stick), Input.GetAxisRaw(dpad));
            return Mathf.Abs(value) <= PadDeadZone ? 0f : value;
        }
        public static float Vertical
        {
            get
            {
                float keyboard = KeyboardVertical;
                return keyboard != 0 ? keyboard : PadAxis("PadVertical", "PadDpadVertical");
            }
        }
        public static Vector2 SkillDirection => new Vector2(Horizontal, Vertical);
        public const float SkillDirectionThreshold = 0.5f;
        // Vertical wins diagonals. Forward-only input uses silver so running casts remain useful.
        public static string ResolveSkill(Vector2 direction, int facing, bool grounded)
        {
            if (direction.y > SkillDirectionThreshold) return BenefactorIds.Waltz;
            if (direction.y < -SkillDirectionThreshold) return grounded ? BenefactorIds.Canvas : BenefactorIds.Forge;
            if (direction.x * facing < -SkillDirectionThreshold) return BenefactorIds.Pendulum;
            return SkillRules.SilverStitch;
        }
        public static bool JumpDown => Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0);
        public static bool JumpHeld => Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.JoystickButton0);
        public static bool JumpUp => Input.GetKeyUp(KeyCode.Space) || Input.GetKeyUp(KeyCode.JoystickButton0);
        public static bool AttackDown => Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.JoystickButton2);
        public static bool CastDown => Input.GetKeyDown(KeyCode.V) || Input.GetKeyDown(KeyCode.JoystickButton3) ||
            QuickSkillDown >= 0;
        internal static int QuickSkillDown
        {
            get
            {
                for (int i = 0; i < QuickSkills.Keys.Length; i++)
                    if (Input.GetKeyDown(QuickSkills.Keys[i])) return i;
                return -1;
            }
        }
        // Down already chooses canvas. Holding the attack button chooses its first wall instead.
        public static bool PaintWallHeld => Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.JoystickButton2);
        private static bool ShiftHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        internal static bool SprintHeld => ShiftHeld && KeyboardHorizontal != 0;
        internal static bool PadDashDown => Input.GetKeyDown(KeyCode.JoystickButton4);
        public static bool DashDown => PadDashDown || SprintHeld &&
            (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift) ||
             Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow));
        internal static float DashDirection => SprintHeld ? KeyboardHorizontal : Horizontal;
        public static bool ParryDown => Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.JoystickButton5);
        public static bool PauseDown => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7);
        public static bool SubmitDown => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.JoystickButton0);
        public static bool CancelDown => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1);
        public static bool DeleteDown => Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.JoystickButton2);
        public static bool InvestigateDown => Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.JoystickButton8);
        public static bool SupportDown => Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.JoystickButton9);
        public static bool RetryDown => Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.JoystickButton6);
        public static Vector2 MenuAxis => new Vector2(
            Stronger(KeyboardHorizontal,
                Stronger(Input.GetAxisRaw("PadHorizontal"), Input.GetAxisRaw("PadDpadHorizontal"))),
            Stronger(KeyboardVertical,
                Stronger(Input.GetAxisRaw("PadVertical"), Input.GetAxisRaw("PadDpadVertical"))));
    }
}
