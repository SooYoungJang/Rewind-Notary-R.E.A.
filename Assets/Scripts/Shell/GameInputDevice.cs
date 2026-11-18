using UnityEngine;

namespace RewindNotary
{
    internal interface IGameInputSource
    {
        bool GetKey(KeyCode key);
        bool GetKeyDown(KeyCode key);
        bool GetKeyUp(KeyCode key);
        bool GetMouseButton(int button);
        bool GetMouseButtonDown(int button);
        float GetAxisRaw(string axis);
    }

    // The mapping in GameInput is shared by both devices, including menu and cast chords.
    internal static class GameInputDevice
    {
        internal static IGameInputSource Source { get; set; }
        internal static bool GetKey(KeyCode key) => Source?.GetKey(key) ?? Input.GetKey(key);
        internal static bool GetKeyDown(KeyCode key) => Source?.GetKeyDown(key) ?? Input.GetKeyDown(key);
        internal static bool GetKeyUp(KeyCode key) => Source?.GetKeyUp(key) ?? Input.GetKeyUp(key);
        internal static bool GetMouseButton(int button) => Source?.GetMouseButton(button) ?? Input.GetMouseButton(button);
        internal static bool GetMouseButtonDown(int button) => Source?.GetMouseButtonDown(button) ?? Input.GetMouseButtonDown(button);
        internal static float GetAxisRaw(string axis) => Source?.GetAxisRaw(axis) ?? Input.GetAxisRaw(axis);
    }
}
