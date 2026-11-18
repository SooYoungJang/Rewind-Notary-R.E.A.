using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    internal sealed class BotKeyboard : IGameInputSource
    {
        private HashSet<KeyCode> previous = new HashSet<KeyCode>();
        private HashSet<KeyCode> current = new HashSet<KeyCode>();

        internal void NextFrame()
        {
            var reuse = previous;
            previous = current;
            current = reuse;
            current.Clear();
        }

        internal void Hold(KeyCode key) => current.Add(key);
        internal void Tap(KeyCode key)
        {
            if (!previous.Contains(key)) Hold(key);
        }

        internal void Cast(int horizontal = 0, int vertical = 0)
        {
            if (horizontal != 0) Hold(horizontal < 0 ? KeyCode.A : KeyCode.D);
            if (vertical != 0) Hold(vertical < 0 ? KeyCode.DownArrow : KeyCode.UpArrow);
            Tap(KeyCode.Q);
        }

        public bool GetKey(KeyCode key) => current.Contains(key);
        public bool GetKeyDown(KeyCode key) => current.Contains(key) && !previous.Contains(key);
        public bool GetKeyUp(KeyCode key) => !current.Contains(key) && previous.Contains(key);
        public bool GetMouseButton(int button) => false;
        public bool GetMouseButtonDown(int button) => false;
        public float GetAxisRaw(string axis) => 0f;
    }
}
