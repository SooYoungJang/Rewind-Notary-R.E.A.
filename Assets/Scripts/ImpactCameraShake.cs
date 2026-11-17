using UnityEngine;
using Unity.Cinemachine;

namespace RewindNotary
{
    // Only correct the final camera state: never move the follow target or physics body.
    public sealed class ImpactCameraShake : CinemachineExtension
    {
        public static bool Enabled = true;
        private float started, duration, amplitude;
        private float Envelope
        {
            get
            {
                if (duration <= 0f) return 0f;
                float remaining = Mathf.Clamp01(1f - (Time.unscaledTime - started) / duration);
                return remaining * remaining;
            }
        }
        public void Shake(float strength, float seconds)
        {
            if (!Enabled) { Clear(); return; }
            float remaining = Mathf.Max(0f, started + duration - Time.unscaledTime);
            amplitude = Mathf.Max(amplitude * Envelope, strength);
            duration = Mathf.Max(remaining, seconds);
            started = Time.unscaledTime;
        }
        public void Clear() { amplitude = duration = 0f; }
        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (!Enabled) { Clear(); return; }
            if (stage != CinemachineCore.Stage.Finalize) return;
            if (deltaTime < 0f) { Clear(); return; }
            float strength = amplitude * Envelope;
            if (strength <= 0f) return;
            float phase = (Time.unscaledTime - started) * Mathf.PI * 2f;
            state.PositionCorrection += new Vector3(Mathf.Cos(phase * 27f), Mathf.Sin(phase * 39f) * 0.65f, 0f) * strength;
        }
    }
}
