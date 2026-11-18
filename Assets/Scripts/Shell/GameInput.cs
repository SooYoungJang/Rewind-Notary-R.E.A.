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
        public static float Horizontal
        {
            get
            {
                float keyboard = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                    - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
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
                float keyboard = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                    - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
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
        public static bool AttackDown => Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.Z)
            || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.JoystickButton2);
        public static bool CastDown => Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.JoystickButton3);
        // Down already chooses canvas. Holding the attack button chooses its first wall instead.
        public static bool PaintWallHeld => Input.GetKey(KeyCode.J) || Input.GetKey(KeyCode.Z)
            || Input.GetMouseButton(0) || Input.GetKey(KeyCode.JoystickButton2);
        public static bool DashDown => Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)
            || Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.JoystickButton4);
        public static bool ParryDown => Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.JoystickButton5);
        public static bool PauseDown => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7);
        public static bool SubmitDown => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.JoystickButton0);
        public static bool CancelDown => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1);
        public static bool DeleteDown => Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.JoystickButton2);
        public static bool InvestigateDown => Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.JoystickButton8);
        public static bool SupportDown => Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.JoystickButton9);
        public static bool RetryDown => Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.JoystickButton6);
        public static Vector2 MenuAxis => new Vector2(
            Stronger((Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) ? 1 : 0)
                - (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) ? 1 : 0),
                Stronger(Input.GetAxisRaw("PadHorizontal"), Input.GetAxisRaw("PadDpadHorizontal"))),
            Stronger((Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) ? 1 : 0)
                - (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) ? 1 : 0),
                Stronger(Input.GetAxisRaw("PadVertical"), Input.GetAxisRaw("PadDpadVertical"))));
    }
}
