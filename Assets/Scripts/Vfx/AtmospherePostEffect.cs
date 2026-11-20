using UnityEngine;

namespace RewindNotary
{
    [RequireComponent(typeof(Camera))]
    public sealed class AtmospherePostEffect : MonoBehaviour
    {
        public static AtmospherePostEffect Instance { get; private set; }

        private struct GradeProfile
        {
            public Color Tint;
            public float BloomThreshold, BloomIntensity, Vignette, Saturation, Contrast;
        }

        private Material material;
        private GradeProfile current, target;
        private float fadeAmount, fadeSpeed;
        private float impactPulse;
        private string activeDistrict;

        private static readonly int BloomThresholdId = Shader.PropertyToID("_BloomThreshold");
        private static readonly int BloomIntensityId = Shader.PropertyToID("_BloomIntensity");
        private static readonly int BloomSpreadId = Shader.PropertyToID("_BloomSpread");
        private static readonly int VignetteIntensityId = Shader.PropertyToID("_VignetteIntensity");
        private static readonly int GradeTintId = Shader.PropertyToID("_GradeTint");
        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        private static readonly int ContrastId = Shader.PropertyToID("_Contrast");
        private static readonly int FadeAmountId = Shader.PropertyToID("_FadeAmount");
        private static readonly int ImpactPulseId = Shader.PropertyToID("_ImpactPulse");

        private void Awake()
        {
            Instance = this;
            var shader = Resources.Load<Shader>("Shaders/AtmospherePost");
            if (shader != null)
            {
                material = new Material(shader);
                material.hideFlags = HideFlags.HideAndDontSave;
            }
            current = ProfileFor("hub");
            target = current;
        }

        public static void SetDistrict(string district, bool fadeIn = false)
        {
            if (Instance == null) return;
            string key = string.IsNullOrEmpty(district) ? "hub" : district;
            if (Instance.activeDistrict != key)
            {
                Instance.activeDistrict = key;
                Instance.target = ProfileFor(key);
            }
            if (fadeIn) Instance.TriggerFadeIn(0.42f);
        }

        public void TriggerFadeIn(float durationSeconds)
        {
            fadeAmount = 1f;
            fadeSpeed = 1f / Mathf.Max(0.08f, durationSeconds);
        }

        public static void PulseImpact(float strength = 0.7f)
        {
            if (Instance != null)
                Instance.impactPulse = Mathf.Clamp01(Mathf.Max(Instance.impactPulse, strength));
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float blend = 1f - Mathf.Exp(-dt * 3.5f);
            current.Tint = Color.Lerp(current.Tint, target.Tint, blend);
            current.BloomThreshold = Mathf.Lerp(current.BloomThreshold, target.BloomThreshold, blend);
            current.BloomIntensity = Mathf.Lerp(current.BloomIntensity, target.BloomIntensity, blend);
            current.Vignette = Mathf.Lerp(current.Vignette, target.Vignette, blend);
            current.Saturation = Mathf.Lerp(current.Saturation, target.Saturation, blend);
            current.Contrast = Mathf.Lerp(current.Contrast, target.Contrast, blend);

            if (fadeAmount > 0f)
                fadeAmount = Mathf.Max(0f, fadeAmount - dt * fadeSpeed);
            if (impactPulse > 0f)
                impactPulse = Mathf.Max(0f, impactPulse - dt * 6.5f);
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (material == null)
            {
                Graphics.Blit(source, destination);
                return;
            }
            material.SetFloat(BloomThresholdId, current.BloomThreshold);
            material.SetFloat(BloomIntensityId, current.BloomIntensity);
            material.SetFloat(BloomSpreadId, 1.85f);
            material.SetFloat(VignetteIntensityId, current.Vignette);
            material.SetColor(GradeTintId, current.Tint);
            material.SetFloat(SaturationId, current.Saturation);
            material.SetFloat(ContrastId, current.Contrast);
            material.SetFloat(FadeAmountId, fadeAmount);
            material.SetFloat(ImpactPulseId, impactPulse);
            Graphics.Blit(source, destination, material);
        }

        private static GradeProfile ProfileFor(string district)
        {
            switch (district)
            {
                case "velvet":
                    return new GradeProfile { Tint = new Color(1.04f, 0.96f, 0.94f), BloomThreshold = 0.68f, BloomIntensity = 0.42f, Vignette = 0.34f, Saturation = 1.08f, Contrast = 1.06f };
                case "clock":
                    return new GradeProfile { Tint = new Color(1.05f, 1.00f, 0.91f), BloomThreshold = 0.70f, BloomIntensity = 0.44f, Vignette = 0.30f, Saturation = 1.06f, Contrast = 1.06f };
                case "opera":
                    return new GradeProfile { Tint = new Color(1.02f, 0.98f, 1.04f), BloomThreshold = 0.64f, BloomIntensity = 0.50f, Vignette = 0.32f, Saturation = 1.09f, Contrast = 1.07f };
                case "bell":
                    return new GradeProfile { Tint = new Color(0.92f, 1.01f, 1.06f), BloomThreshold = 0.66f, BloomIntensity = 0.46f, Vignette = 0.36f, Saturation = 1.07f, Contrast = 1.05f };
                case "gallery":
                    return new GradeProfile { Tint = new Color(1.02f, 1.02f, 1.00f), BloomThreshold = 0.78f, BloomIntensity = 0.32f, Vignette = 0.22f, Saturation = 1.04f, Contrast = 1.03f };
                case "cassel":
                    return new GradeProfile { Tint = new Color(0.95f, 0.98f, 1.06f), BloomThreshold = 0.62f, BloomIntensity = 0.54f, Vignette = 0.40f, Saturation = 1.05f, Contrast = 1.10f };
                default:
                    return new GradeProfile { Tint = new Color(1.04f, 0.99f, 0.94f), BloomThreshold = 0.72f, BloomIntensity = 0.38f, Vignette = 0.28f, Saturation = 1.05f, Contrast = 1.04f };
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (material != null) Destroy(material);
        }
    }
}
