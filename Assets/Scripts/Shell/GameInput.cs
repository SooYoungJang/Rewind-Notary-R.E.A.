using System;
using UnityEngine;
using Input = RewindNotary.GameInputDevice;

namespace RewindNotary
{
    public static class GameInput
    {
        public static bool GameplayEnabled { get; private set; }
        public static bool UsingGamepad { get; private set; }
        private static int suppressedFrame;
        public static bool CanPlay => GameplayEnabled && Time.frameCount > suppressedFrame;
        public static void SetGameplay(bool enabled) { GameplayEnabled = enabled; suppressedFrame = Time.frameCount; }

        public static KeyCode JumpKey { get; private set; } = KeyCode.Space;
        public static KeyCode AttackKey { get; private set; } = KeyCode.J;
        public static KeyCode CastKey { get; private set; } = KeyCode.Q;
        public static KeyCode DashKey { get; private set; } = KeyCode.LeftShift;
        public static KeyCode ParryKey { get; private set; } = KeyCode.L;
        public static KeyCode InteractKey { get; private set; } = KeyCode.F;
        public static KeyCode MapKey { get; private set; } = KeyCode.M;

        public static void ApplyBindings(GameSettings s)
        {
            if (s == null) return;
            JumpKey = ParseKey(s.keyJump, KeyCode.Space);
            AttackKey = ParseKey(s.keyAttack, KeyCode.J);
            CastKey = ParseKey(s.keyCast, KeyCode.Q);
            DashKey = ParseKey(s.keyDash, KeyCode.LeftShift);
            ParryKey = ParseKey(s.keyParry, KeyCode.L);
            InteractKey = ParseKey(s.keyInteract, KeyCode.F);
            MapKey = ParseKey(s.keyMap, KeyCode.M);
        }

        public static void TrackActiveDevice()
        {
            if (AutoTestRuntime.Report != null) { UsingGamepad = false; return; }
            if (UnityEngine.Input.anyKeyDown)
            {
                bool joy = false;
                for (int k = (int)KeyCode.JoystickButton0; k <= (int)KeyCode.JoystickButton19; k++)
                    if (UnityEngine.Input.GetKeyDown((KeyCode)k)) { joy = true; break; }
                UsingGamepad = joy;
            }
            else if (Mathf.Abs(UnityEngine.Input.GetAxisRaw("PadHorizontal")) > 0.35f
                || Mathf.Abs(UnityEngine.Input.GetAxisRaw("PadVertical")) > 0.35f
                || Mathf.Abs(UnityEngine.Input.GetAxisRaw("PadDpadHorizontal")) > 0.35f
                || Mathf.Abs(UnityEngine.Input.GetAxisRaw("PadDpadVertical")) > 0.35f)
            {
                UsingGamepad = true;
            }
        }

        private static KeyCode ParseKey(string raw, KeyCode fallback)
        {
            if (!string.IsNullOrEmpty(raw) && Enum.TryParse(raw, out KeyCode parsed) && parsed != KeyCode.None)
                return parsed;
            return fallback;
        }

        public static string KeyLabel(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.LeftShift: return "L-Shift";
                case KeyCode.RightShift: return "R-Shift";
                case KeyCode.LeftControl: return "L-Ctrl";
                case KeyCode.Return: return "Enter";
                case KeyCode.Mouse1: return "R-Click";
                case KeyCode.Mouse2: return "M-Click";
                default: return k.ToString();
            }
        }

        public static string FormatControlsPrompt(bool combatMode)
        {
            if (UsingGamepad)
                return Loc.Get(combatMode ? "controls.pad_combat" : "controls.pad_explore");
            if (combatMode)
                return Loc.Get("controls.kb_combat", KeyLabel(JumpKey), KeyLabel(AttackKey), KeyLabel(CastKey),
                    KeyLabel(DashKey), KeyLabel(ParryKey), KeyLabel(MapKey));
            return Loc.Get("controls.kb_explore", KeyLabel(InteractKey), KeyLabel(AttackKey), KeyLabel(CastKey),
                KeyLabel(DashKey), KeyLabel(MapKey));
        }

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

        public static string ResolveSkill(Vector2 direction, int facing, bool grounded)
        {
            if (direction.y > SkillDirectionThreshold) return BenefactorIds.Waltz;
            if (direction.y < -SkillDirectionThreshold) return grounded ? BenefactorIds.Canvas : BenefactorIds.Forge;
            if (direction.x * facing < -SkillDirectionThreshold) return BenefactorIds.Pendulum;
            return SkillRules.SilverStitch;
        }

        public static bool JumpDown => Input.GetKeyDown(JumpKey) || (JumpKey != KeyCode.Space && Input.GetKeyDown(KeyCode.Space) && AutoTestRuntime.Report != null) || Input.GetKeyDown(KeyCode.JoystickButton0);
        public static bool JumpHeld => Input.GetKey(JumpKey) || (JumpKey != KeyCode.Space && Input.GetKey(KeyCode.Space) && AutoTestRuntime.Report != null) || Input.GetKey(KeyCode.JoystickButton0);
        public static bool JumpUp => Input.GetKeyUp(JumpKey) || (JumpKey != KeyCode.Space && Input.GetKeyUp(KeyCode.Space) && AutoTestRuntime.Report != null) || Input.GetKeyUp(KeyCode.JoystickButton0);
        public static bool AttackDown => Input.GetKeyDown(AttackKey) || Input.GetKeyDown(KeyCode.Z)
            || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.JoystickButton2);
        public static bool CastDown => Input.GetKeyDown(CastKey) || Input.GetKeyDown(KeyCode.JoystickButton3);
        public static bool PaintWallHeld => Input.GetKey(AttackKey) || Input.GetKey(KeyCode.Z)
            || Input.GetMouseButton(0) || Input.GetKey(KeyCode.JoystickButton2);
        public static bool DashDown => Input.GetKeyDown(DashKey) || Input.GetKeyDown(KeyCode.RightShift)
            || Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.JoystickButton4);
        public static bool ParryDown => Input.GetKeyDown(ParryKey) || Input.GetKeyDown(KeyCode.JoystickButton5);
        public static bool PauseDown => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7);
        public static bool SubmitDown => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.JoystickButton0);
        public static bool CancelDown => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1);
        public static bool DeleteDown => Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.JoystickButton2);
        public static bool InvestigateDown => Input.GetKeyDown(InteractKey) || Input.GetKeyDown(KeyCode.JoystickButton8);
        public static bool SupportDown => Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.JoystickButton9);
        public static bool RetryDown => Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.JoystickButton6);
        public static bool MapDown => AutoTestRuntime.Report == null && (Input.GetKeyDown(MapKey) || Input.GetKeyDown(KeyCode.Tab));

        public static Vector2 MenuAxis => new Vector2(
            Stronger((Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) ? 1 : 0)
                - (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) ? 1 : 0),
                Stronger(Input.GetAxisRaw("PadHorizontal"), Input.GetAxisRaw("PadDpadHorizontal"))),
            Stronger((Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) ? 1 : 0)
                - (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) ? 1 : 0),
                Stronger(Input.GetAxisRaw("PadVertical"), Input.GetAxisRaw("PadDpadVertical"))));
    }
}
