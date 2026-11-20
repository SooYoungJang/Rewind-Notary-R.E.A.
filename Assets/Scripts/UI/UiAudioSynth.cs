using UnityEngine;

namespace RewindNotary
{
    public sealed class UiAudioSynth : MonoBehaviour
    {
        public static UiAudioSynth Instance { get; private set; }
        private AudioSource source;
        private AudioClip moveClip, confirmClip, cancelClip, mapOpenClip, mapCloseClip, checkpointClip, voiceClip;
        private float lastVoiceAt;

        public static void Ensure(Transform parent)
        {
            if (Instance != null) return;
            var go = new GameObject("UI Audio Synth");
            if (parent != null) go.transform.SetParent(parent, false);
            Instance = go.AddComponent<UiAudioSynth>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            moveClip = MakeTone("ui_move", 0.042f, (t, d) =>
                (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(760f, 520f, t / d) * t) * 0.45f
                + Mathf.Sin(2f * Mathf.PI * 1480f * t) * 0.14f) * Env(t, d, 0.004f));

            confirmClip = MakeTone("ui_confirm", 0.13f, (t, d) =>
            {
                float a = Mathf.Sin(2f * Mathf.PI * 587.3f * t) * 0.36f;
                float b = t > 0.032f ? Mathf.Sin(2f * Mathf.PI * 880f * (t - 0.032f)) * 0.42f : 0f;
                return (a + b) * Env(t, d, 0.006f);
            });

            cancelClip = MakeTone("ui_cancel", 0.075f, (t, d) =>
                Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(400f, 240f, t / d) * t) * 0.42f * Env(t, d, 0.005f));

            mapOpenClip = MakeTone("ui_map_open", 0.12f, (t, d) =>
                (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(420f, 760f, t / d) * t) * 0.32f
                + Mathf.Sin(2f * Mathf.PI * 1120f * t) * 0.12f) * Env(t, d, 0.01f));

            mapCloseClip = MakeTone("ui_map_close", 0.10f, (t, d) =>
                Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(640f, 340f, t / d) * t) * 0.32f * Env(t, d, 0.008f));

            checkpointClip = MakeTone("ui_checkpoint", 0.44f, (t, d) =>
            {
                float n1 = Mathf.Sin(2f * Mathf.PI * 523.25f * t) * Mathf.Exp(-t * 6f) * 0.34f;
                float n2 = t > 0.08f ? Mathf.Sin(2f * Mathf.PI * 659.25f * (t - 0.08f)) * Mathf.Exp(-(t - 0.08f) * 5.5f) * 0.34f : 0f;
                float n3 = t > 0.16f ? Mathf.Sin(2f * Mathf.PI * 1046.5f * (t - 0.16f)) * Mathf.Exp(-(t - 0.16f) * 4.5f) * 0.38f : 0f;
                return (n1 + n2 + n3) * Mathf.Min(1f, t / 0.008f);
            });

            voiceClip = MakeTone("ui_voice", 0.036f, (t, d) =>
                (Mathf.Sin(2f * Mathf.PI * 440f * t) * 0.42f
                + Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.16f) * Env(t, d, 0.004f));
        }

        private static float Env(float t, float d, float attack)
        {
            float a = Mathf.Clamp01(t / attack);
            float r = Mathf.Pow(Mathf.Clamp01(1f - t / d), 1.8f);
            return a * r;
        }

        private static AudioClip MakeTone(string name, float seconds, System.Func<float, float, float> sample)
        {
            const int rate = 24000;
            int count = Mathf.Max(16, Mathf.RoundToInt(rate * seconds));
            var data = new float[count];
            for (int i = 0; i < count; i++)
                data[i] = Mathf.Clamp(sample((float)i / rate, seconds), -0.95f, 0.95f);
            var clip = AudioClip.Create(name, count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private float Volume()
        {
            var mgr = AudioManager.Instance;
            return mgr != null ? Mathf.Clamp01(mgr.MasterVolume * mgr.SfxVolume * 0.72f) : 0.45f;
        }

        private void PlayClip(AudioClip clip, float pitch = 1f, float gain = 1f)
        {
            if (source == null || clip == null) return;
            source.pitch = pitch;
            source.PlayOneShot(clip, Volume() * gain);
        }

        public static void PlayMove() => Instance?.PlayClip(Instance.moveClip, Random.Range(0.96f, 1.05f), 0.75f);
        public static void PlayConfirm() => Instance?.PlayClip(Instance.confirmClip, 1f, 0.90f);
        public static void PlayCancel() => Instance?.PlayClip(Instance.cancelClip, 1f, 0.80f);
        public static void PlayMapToggle(bool open) => Instance?.PlayClip(open ? Instance.mapOpenClip : Instance.mapCloseClip, 1f, 0.85f);
        public static void PlayCheckpoint() => Instance?.PlayClip(Instance.checkpointClip, 1f, 1.0f);

        public static void PlayVoiceBlip(string speakerId)
        {
            if (Instance == null || AutoTestRuntime.Report != null) return;
            if (Time.unscaledTime - Instance.lastVoiceAt < 0.068f) return;
            Instance.lastVoiceAt = Time.unscaledTime;
            float basePitch = SpeakerPitch(speakerId);
            Instance.PlayClip(Instance.voiceClip, basePitch * Random.Range(0.94f, 1.06f), 0.36f);
        }

        private static float SpeakerPitch(string speakerId)
        {
            switch (speakerId)
            {
                case "rea": return 1.32f;
                case "rea_echo": return 1.48f;
                case "luka": return 1.24f;
                case "irina": return 1.22f;
                case "valeria": return 1.18f;
                case "marta": return 1.06f;
                case "theo": return 0.98f;
                case "julius": return 0.90f;
                case "mikhail": return 0.84f;
                case "ethan": return 0.78f;
                case "gabriel": return 0.74f;
                case "cassel": return 0.70f;
                default: return 1.05f;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (moveClip != null) Destroy(moveClip);
            if (confirmClip != null) Destroy(confirmClip);
            if (cancelClip != null) Destroy(cancelClip);
            if (mapOpenClip != null) Destroy(mapOpenClip);
            if (mapCloseClip != null) Destroy(mapCloseClip);
            if (checkpointClip != null) Destroy(checkpointClip);
            if (voiceClip != null) Destroy(voiceClip);
        }
    }
}
