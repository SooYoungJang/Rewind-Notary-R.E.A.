using UnityEngine;

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
                return keyboard != 0 ? keyboard : Stronger(Input.GetAxisRaw("PadHorizontal"), Input.GetAxisRaw("PadDpadHorizontal"));
            }
        }
        private static float Stronger(float a, float b) => Mathf.Abs(a) >= Mathf.Abs(b) ? a : b;
        public static bool JumpDown => Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W)
            || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.JoystickButton0);
        public static bool JumpHeld => Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.W)
            || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.JoystickButton0);
        public static bool JumpUp => Input.GetKeyUp(KeyCode.Space) || Input.GetKeyUp(KeyCode.W)
            || Input.GetKeyUp(KeyCode.UpArrow) || Input.GetKeyUp(KeyCode.JoystickButton0);
        public static bool AttackDown => Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.Z)
            || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.JoystickButton2);
        public static bool SilverDown => Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.JoystickButton3);
        public static bool PauseDown => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7);
        public static bool SubmitDown => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.JoystickButton0);
        public static bool CancelDown => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1);
        public static bool DeleteDown => Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.JoystickButton2);
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
