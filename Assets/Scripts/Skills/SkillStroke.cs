using UnityEngine;

namespace RewindNotary
{
    public sealed class SkillStroke : MonoBehaviour
    {
        public static Transform WorldRoot;
        private static Texture2D beamTexture;
        private static Sprite beamSprite;
        private static Material beamMaterial;
        private SpriteRenderer visual;
        private float lifetime, started;
        private Color baseColor;

        private static void EnsureBeamAssets()
        {
            if (beamSprite == null)
            {
                const int w = 64, h = 16;
                beamTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
                beamTexture.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < h; y++)
                {
                    float ny = Mathf.Abs((y / (float)(h - 1)) * 2f - 1f);
                    float core = Mathf.Pow(Mathf.Clamp01(1f - ny), 2.1f);
                    for (int x = 0; x < w; x++)
                    {
                        float nx = Mathf.Abs((x / (float)(w - 1)) * 2f - 1f);
                        float taper = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((nx - 0.55f) / 0.45f));
                        float a = core * taper;
                        beamTexture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                beamTexture.Apply();
                beamSprite = Sprite.Create(beamTexture, new Rect(0, 0, w, h), Vector2.one * 0.5f, w);
            }
            if (beamMaterial == null)
            {
                var sh = Resources.Load<Shader>("Shaders/AdditiveSprite") ?? Shader.Find("Sprites/Default");
                beamMaterial = new Material(sh);
            }
        }

        public static void Show(Vector3 from, Vector3 to, Color color, float seconds)
        {
            EnsureBeamAssets();
            var go = new GameObject("Skill stroke");
            if (WorldRoot != null) go.transform.SetParent(WorldRoot, false);
            var effect = go.AddComponent<SkillStroke>();
            effect.visual = go.AddComponent<SpriteRenderer>();
            effect.visual.sprite = beamSprite;
            effect.visual.sharedMaterial = beamMaterial;
            effect.baseColor = new Color(color.r, color.g, color.b, Mathf.Min(color.a, 0.78f));
            effect.visual.color = effect.baseColor;
            effect.visual.sortingOrder = 14;
            go.transform.position = (from + to) * 0.5f;
            Vector3 delta = to - from;
            go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            go.transform.localScale = new Vector3(Mathf.Max(0.2f, delta.magnitude), 0.72f, 1f);
            effect.lifetime = Mathf.Max(0.05f, seconds);
            effect.started = Time.time;
        }

        public static void Afterimage(SpriteRenderer source, float seconds)
        {
            var go = new GameObject("Sword mastery afterimage");
            if (WorldRoot != null) go.transform.SetParent(WorldRoot, false);
            var effect = go.AddComponent<SkillStroke>();
            effect.visual = go.AddComponent<SpriteRenderer>();
            effect.visual.sprite = source.sprite;
            effect.visual.flipX = source.flipX;
            effect.baseColor = new Color(0.42f, 0.85f, 1f, 0.45f);
            effect.visual.color = effect.baseColor;
            effect.visual.sortingOrder = 9;
            go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            go.transform.localScale = source.transform.lossyScale;
            effect.lifetime = Mathf.Max(0.05f, seconds);
            effect.started = Time.time;
        }

        private void Update()
        {
            float t = (Time.time - started) / lifetime;
            if (t >= 1f) { Destroy(gameObject); return; }
            var c = baseColor;
            c.a = baseColor.a * (1f - t * t);
            visual.color = c;
        }
    }
}
