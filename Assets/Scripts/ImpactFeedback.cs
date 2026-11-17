using UnityEngine;

namespace RewindNotary
{
    [DefaultExecutionOrder(-100)]
    public sealed class ImpactFeedback : MonoBehaviour
    {
        public static ImpactFeedback Instance { get; private set; }
        public const float SwordStopScale = 0.05f, SwordStopSeconds = 0.065f;
        public const float HurtStopScale = 0.2f, HurtStopSeconds = 0.045f;
        public const float SwordShake = 0.07f, HurtShake = 0.18f, DefeatShake = 0.32f;
        private ImpactCameraShake cameraShake;
        private bool stopping;
        private float restoreScale, appliedScale, stopUntil;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }
        public void Initialize(ImpactCameraShake shake) { cameraShake = shake; }
        public static void SwordHit()
        {
            if (Instance == null || !Instance.isActiveAndEnabled) return;
            Instance.HitStop(SwordStopScale, SwordStopSeconds);
            Instance.cameraShake?.Shake(SwordShake, 0.12f);
        }
        public static void PlayerHurt()
        {
            if (Instance == null || !Instance.isActiveAndEnabled) return;
            Instance.HitStop(HurtStopScale, HurtStopSeconds);
            Instance.cameraShake?.Shake(HurtShake, 0.22f);
        }
        public static void BossDefeated()
        {
            if (Instance == null || !Instance.isActiveAndEnabled) return;
            Instance.cameraShake?.Shake(DefeatShake, 0.45f);
        }
        private void HitStop(float scale, float seconds)
        {
            // A pause/slow-motion owner that changes timeScale takes precedence.
            if (stopping && !Mathf.Approximately(Time.timeScale, appliedScale)) stopping = false;
            if (Time.timeScale <= 0f) return;
            if (!stopping) { restoreScale = Time.timeScale; stopping = true; stopUntil = 0f; }
            appliedScale = Mathf.Min(Time.timeScale, scale);
            stopUntil = Mathf.Max(stopUntil, Time.unscaledTime + seconds);
            Time.timeScale = appliedScale;
        }
        private void Update()
        {
            if (!stopping) return;
            if (!Mathf.Approximately(Time.timeScale, appliedScale)) { stopping = false; return; }
            if (Time.unscaledTime >= stopUntil) RestoreTime();
        }
        private void RestoreTime()
        {
            if (stopping && Mathf.Approximately(Time.timeScale, appliedScale)) Time.timeScale = restoreScale;
            stopping = false;
        }
        public static void ResetEffects()
        {
            if (Instance == null) return;
            Instance.RestoreTime();
            if (Instance.cameraShake != null) Instance.cameraShake.Clear();
        }
        private void OnDisable() { RestoreTime(); if (cameraShake != null) cameraShake.Clear(); }
        private void OnApplicationFocus(bool focused) { if (!focused) ResetEffects(); }
        private void OnApplicationPause(bool paused) { if (paused) ResetEffects(); }
        private void OnDestroy() { RestoreTime(); if (Instance == this) Instance = null; }
    }
}
