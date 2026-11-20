using UnityEngine;

namespace RewindNotary
{
    internal sealed class StarterCommandInput
    {
        internal const float SequenceSeconds = .32f, FinishSeconds = .13f;
        private KeyCode previous;
        private float previousAt;
        private int ready = -1, facing;
        private float readyAt;

        internal void Press(KeyCode direction, float now)
        {
            if (direction == KeyCode.None) return;
            ready = -1;
            facing = direction == KeyCode.LeftArrow ? -1 : direction == KeyCode.RightArrow ? 1 : 0;
            if (now - previousAt <= SequenceSeconds)
            {
                if (facing != 0 && previous == KeyCode.DownArrow) ready = QuickSkills.BladeWave;
                else if (facing != 0 && previous == direction) ready = QuickSkills.Thrust;
                else if (previous == KeyCode.DownArrow && direction == KeyCode.UpArrow) ready = QuickSkills.Rising;
                else if (previous == KeyCode.UpArrow && direction == KeyCode.DownArrow) ready = QuickSkills.Sweep;
            }
            previous = direction;
            previousAt = readyAt = now;
        }

        internal int Consume(float now, out int direction)
        {
            direction = facing;
            int result = now - readyAt <= FinishSeconds ? ready : -1;
            Reset();
            return result;
        }

        internal void Reset() { previous = KeyCode.None; ready = -1; }
    }
}
