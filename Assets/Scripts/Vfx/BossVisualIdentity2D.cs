using UnityEngine;

namespace RewindNotary
{
    public sealed class BossVisualIdentity2D : MonoBehaviour
    {
        private CombatMotor2D motor;
        private Transform visual;
        private Material additiveMat;
        private Texture2D glowTex;
        private Texture2D ringTex;
        private Sprite glowSprite;
        private Sprite ringSprite;
        private SpriteRenderer auraBack;
        private SpriteRenderer crestRing;
        private SpriteRenderer[] orbiters;
        private SpriteRenderer telegraphRing;
        private string activeIdentity;
        private float baseScale = 1f;
        private float nextEchoGhost;

        public void Initialize(CombatMotor2D owner)
        {
            motor = owner;
            visual = owner.Animation.transform;
            BuildTextures();
            BuildLayers();
            RefreshIdentity(true);
        }

        private void BuildTextures()
        {
            glowTex = CreateTex(64, false);
            ringTex = CreateTex(96, true);
            glowSprite = Sprite.Create(glowTex, new Rect(0, 0, 64, 64), Vector2.one * 0.5f, 64f);
            ringSprite = Sprite.Create(ringTex, new Rect(0, 0, 96, 96), Vector2.one * 0.5f, 96f);
            Shader sh = Resources.Load<Shader>("Shaders/AdditiveSprite") ?? Shader.Find("Sprites/Default");
            additiveMat = new Material(sh);
        }

        private static Texture2D CreateTex(int size, bool ring)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = ring
                        ? Mathf.Exp(-Mathf.Pow((d - 0.80f) / 0.08f, 2f)) * Mathf.Clamp01(1.04f - d)
                        : Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private SpriteRenderer MakeLayer(string label, Sprite sprite, int order, Vector3 scale)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = additiveMat;
            sr.sortingOrder = order;
            return sr;
        }

        private void BuildLayers()
        {
            auraBack = MakeLayer("BossAuraBack", glowSprite, 8, new Vector3(2.4f, 2.8f, 1f));
            crestRing = MakeLayer("BossCrestRing", ringSprite, 9, new Vector3(1.8f, 1.8f, 1f));
            orbiters = new SpriteRenderer[3];
            for (int i = 0; i < orbiters.Length; i++)
                orbiters[i] = MakeLayer("BossOrbiter_" + i, glowSprite, 11, new Vector3(0.45f, 0.45f, 1f));
            telegraphRing = MakeLayer("BossTelegraphRing", ringSprite, 14, new Vector3(2.2f, 2.2f, 1f));
            telegraphRing.color = new Color(1.0f, 0.55f, 0.25f, 0.92f);
            telegraphRing.enabled = false;
        }

        private static string ResolveKey(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "janhyang";
            string n = rawName.ToLowerInvariant();
            if (n.Contains("cassel")) return "cassel";
            if (n.Contains("mikhail") || n.Contains("silver")) return "mikhail";
            if (n.Contains("julius") || n.Contains("pendulum")) return "julius";
            if (n.Contains("valeria") || n.Contains("waltz")) return "valeria";
            if (n.Contains("gabriel") || n.Contains("forge")) return "gabriel";
            if (n.Contains("irina") || n.Contains("canvas")) return "irina";
            return "janhyang";
        }

        private void RefreshIdentity(bool force)
        {
            string key = ResolveKey(gameObject.name);
            if (!force && key == activeIdentity) return;
            activeIdentity = key;

            string customSheet = "GameArt/sprites/boss-" + key + "-sheet";
            bool hasCustom = key != "janhyang" &&
                (motor.Animation.ActiveResource == customSheet || motor.Animation.SwapSheet(customSheet));
            if (!hasCustom && motor.Animation.ActiveResource != "GameArt/sprites/janhyang-sheet")
                motor.Animation.SwapSheet("GameArt/sprites/janhyang-sheet");

            Color tint, auraColor, ringColor, orbColor;
            switch (key)
            {
                case "mikhail":
                    baseScale = 1.16f;
                    tint = hasCustom ? Color.white : new Color(0.95f, 0.72f, 0.76f, 1f);
                    auraColor = new Color(0.86f, 0.18f, 0.28f, 0.34f);
                    ringColor = new Color(0.94f, 0.94f, 1.0f, 0.52f);
                    orbColor = new Color(0.92f, 0.96f, 1.0f, 0.85f);
                    break;
                case "julius":
                    baseScale = 1.14f;
                    tint = hasCustom ? Color.white : new Color(1.0f, 0.90f, 0.68f, 1f);
                    auraColor = new Color(0.96f, 0.74f, 0.28f, 0.36f);
                    ringColor = new Color(1.0f, 0.84f, 0.40f, 0.64f);
                    orbColor = new Color(1.0f, 0.86f, 0.45f, 0.85f);
                    break;
                case "valeria":
                    baseScale = 1.10f;
                    tint = hasCustom ? Color.white : new Color(1.0f, 0.80f, 0.88f, 1f);
                    auraColor = new Color(0.96f, 0.36f, 0.62f, 0.36f);
                    ringColor = new Color(1.0f, 0.82f, 0.88f, 0.58f);
                    orbColor = new Color(1.0f, 0.56f, 0.76f, 0.85f);
                    break;
                case "gabriel":
                    baseScale = 1.24f;
                    tint = hasCustom ? Color.white : new Color(0.76f, 0.94f, 0.96f, 1f);
                    auraColor = new Color(0.24f, 0.76f, 0.88f, 0.38f);
                    ringColor = new Color(0.88f, 0.72f, 0.42f, 0.62f);
                    orbColor = new Color(0.42f, 0.92f, 1.0f, 0.85f);
                    break;
                case "irina":
                    baseScale = 1.12f;
                    tint = hasCustom ? Color.white : new Color(0.88f, 0.95f, 1.0f, 1f);
                    auraColor = new Color(0.45f, 0.78f, 1.0f, 0.34f);
                    ringColor = new Color(1.0f, 0.92f, 0.74f, 0.54f);
                    orbColor = new Color(0.72f, 0.90f, 1.0f, 0.85f);
                    break;
                case "cassel":
                    baseScale = 1.32f;
                    tint = hasCustom ? Color.white : new Color(0.86f, 0.92f, 1.0f, 1f);
                    auraColor = new Color(0.78f, 0.86f, 1.0f, 0.44f);
                    ringColor = new Color(1.0f, 0.86f, 0.44f, 0.78f);
                    orbColor = new Color(1.0f, 0.92f, 0.60f, 0.92f);
                    break;
                default:
                    baseScale = 1.00f;
                    tint = new Color(0.78f, 0.92f, 1.0f, 0.94f);
                    auraColor = new Color(0.35f, 0.75f, 0.98f, 0.30f);
                    ringColor = new Color(0.60f, 0.90f, 1.0f, 0.45f);
                    orbColor = new Color(0.72f, 0.94f, 1.0f, 0.72f);
                    break;
            }

            motor.Animation.Tint(tint);
            auraBack.color = auraColor;
            crestRing.color = ringColor;
            for (int i = 0; i < orbiters.Length; i++) orbiters[i].color = orbColor;
        }

        private void LateUpdate()
        {
            if (motor == null || visual == null) return;
            RefreshIdentity(false);

            bool dead = motor.Health != null && motor.Health.Dead;
            float t = Time.time;
            float breath = 1f + Mathf.Sin(t * 3.4f) * 0.025f;
            visual.localScale = new Vector3(baseScale * (2f - breath), baseScale * breath, 1f);

            bool show = !dead && gameObject.activeInHierarchy;
            auraBack.enabled = show;
            crestRing.enabled = show;
            for (int i = 0; i < orbiters.Length; i++) orbiters[i].enabled = show;

            if (!show)
            {
                telegraphRing.enabled = false;
                return;
            }

            float h = 1.08f * baseScale;
            auraBack.transform.localPosition = new Vector3(0f, h, 0f);
            float pulse = 0.92f + Mathf.Sin(t * 4.2f) * 0.10f;
            auraBack.transform.localScale = new Vector3(2.3f * baseScale * pulse, 2.7f * baseScale * pulse, 1f);

            crestRing.transform.localPosition = new Vector3(0f, h + 0.15f, 0f);
            crestRing.transform.localRotation = Quaternion.Euler(0f, 0f, t * (activeIdentity == "julius" ? -55f : 32f));
            crestRing.transform.localScale = new Vector3(1.75f * baseScale, 1.75f * baseScale, 1f);

            for (int i = 0; i < orbiters.Length; i++)
            {
                float ang = t * 2.1f + i * (Mathf.PI * 2f / orbiters.Length);
                float rx = 0.95f * baseScale;
                float ry = 0.55f * baseScale;
                orbiters[i].transform.localPosition = new Vector3(Mathf.Cos(ang) * rx, h + Mathf.Sin(ang) * ry, 0f);
            }

            bool tele = motor.Animation.IsTelegraphing;
            telegraphRing.enabled = tele;
            if (tele)
            {
                float s = (1.6f + Mathf.PingPong(t * 4.5f, 0.9f)) * baseScale;
                telegraphRing.transform.localPosition = new Vector3(0f, h, 0f);
                telegraphRing.transform.localScale = new Vector3(s, s, 1f);
                telegraphRing.transform.localRotation = Quaternion.Euler(0f, 0f, -t * 150f);
            }

            if (activeIdentity == "janhyang" && Time.time >= nextEchoGhost && Mathf.Abs(motor.Body.linearVelocity.x) > 1.5f && motor.Animation.Renderer != null)
            {
                SkillStroke.Afterimage(motor.Animation.Renderer, 0.22f);
                nextEchoGhost = Time.time + 0.18f;
            }
        }

        private void OnDestroy()
        {
            if (glowSprite != null) Destroy(glowSprite);
            if (ringSprite != null) Destroy(ringSprite);
            if (glowTex != null) Destroy(glowTex);
            if (ringTex != null) Destroy(ringTex);
            if (additiveMat != null) Destroy(additiveMat);
        }
    }
}
