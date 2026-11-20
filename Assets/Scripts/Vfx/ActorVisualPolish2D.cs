using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed class ActorVisualPolish2D : MonoBehaviour
    {
        private CombatMotor2D motor;
        private PlayerController2D controller;
        private SpriteRenderer shadowRenderer;
        private ParticleSystem burstParticles;
        private Texture2D shadowTexture;
        private Texture2D sparkTexture;
        private Sprite shadowSprite;
        private Material additiveMaterial;
        private bool isPlayer;
        private int lastHealth;
        private bool wasDashing;
        private float nextSlideDust;
        private static readonly Dictionary<string, Sprite> FxSprites = new Dictionary<string, Sprite>();
        private static Material SharedAdditiveSpriteMaterial;

        public void Initialize(CombatMotor2D owner, bool playerActor)
        {
            motor = owner;
            isPlayer = playerActor;
            controller = owner.GetComponent<PlayerController2D>();
            lastHealth = owner.Health != null ? owner.Health.Current : 0;
            BuildTextures();
            BuildContactShadow();
            BuildBurstParticles();
            motor.Landed += OnLanded;
            motor.AttackStarted += OnAttackStarted;
        }

        private void BuildTextures()
        {
            const int w = 64, h = 32;
            shadowTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            shadowTexture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (x - (w - 1) * 0.5f) / (w * 0.5f);
                    float ny = (y - (h - 1) * 0.5f) / (h * 0.5f);
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    float a = Mathf.Clamp01(1f - d);
                    pixels[y * w + x] = new Color(0.02f, 0.03f, 0.06f, a * a * 0.58f);
                }
            }
            shadowTexture.SetPixels(pixels);
            shadowTexture.Apply();
            shadowSprite = Sprite.Create(shadowTexture, new Rect(0, 0, w, h), Vector2.one * 0.5f, 32f);

            const int s = 32;
            sparkTexture = new Texture2D(s, s, TextureFormat.RGBA32, false);
            sparkTexture.wrapMode = TextureWrapMode.Clamp;
            var sp = new Color[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float nx = (x - (s - 1) * 0.5f) / (s * 0.5f);
                    float ny = (y - (s - 1) * 0.5f) / (s * 0.5f);
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny));
                    sp[y * s + x] = new Color(1f, 1f, 1f, a * a);
                }
            }
            sparkTexture.SetPixels(sp);
            sparkTexture.Apply();

            Shader additive = Resources.Load<Shader>("Shaders/AdditiveSprite") ?? Shader.Find("Sprites/Default");
            additiveMaterial = new Material(additive);
            additiveMaterial.mainTexture = sparkTexture;
        }

        private void BuildContactShadow()
        {
            var go = new GameObject("Contact shadow");
            go.transform.SetParent(transform, false);
            shadowRenderer = go.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = shadowSprite;
            shadowRenderer.sortingOrder = 1;
            shadowRenderer.transform.localScale = new Vector3(0.85f, 0.42f, 1f);
        }

        private void BuildBurstParticles()
        {
            var go = new GameObject("Combat particles");
            go.transform.SetParent(transform, false);
            burstParticles = go.AddComponent<ParticleSystem>();
            var main = burstParticles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 4.6f);
            main.gravityModifier = 0.55f;

            var emission = burstParticles.emission;
            emission.enabled = false;

            var colorOverLife = burstParticles.colorOverLifetime;
            colorOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLife.color = grad;

            var renderer = burstParticles.GetComponent<ParticleSystemRenderer>();
            renderer.material = additiveMaterial;
            renderer.sortingOrder = 18;
        }

        private void LateUpdate()
        {
            if (motor == null) return;
            UpdateContactShadow();
            CheckHealthChange();
            if (isPlayer) CheckPlayerMovementFx();
        }

        private void UpdateContactShadow()
        {
            if (shadowRenderer == null) return;
            if (motor.Health != null && motor.Health.Dead)
            {
                shadowRenderer.enabled = false;
                return;
            }
            shadowRenderer.enabled = true;
            Vector2 origin = (Vector2)transform.position + Vector2.up * 0.35f;
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 4.5f, 1 << 8);
            if (hit.collider != null)
            {
                float height = Mathf.Max(0f, transform.position.y - hit.point.y);
                float scale = Mathf.Clamp(1f - height * 0.18f, 0.25f, 1f);
                shadowRenderer.transform.position = new Vector3(transform.position.x, hit.point.y + 0.03f, 0f);
                shadowRenderer.transform.localScale = new Vector3(0.85f * scale, 0.42f * scale, 1f);
                var c = shadowRenderer.color;
                c.a = scale;
                shadowRenderer.color = c;
            }
            else
            {
                shadowRenderer.enabled = false;
            }
        }

        private void CheckHealthChange()
        {
            if (motor.Health == null) return;
            int now = motor.Health.Current;
            if (now < lastHealth)
            {
                EmitBurst(transform.position + Vector3.up * 1.0f,
                    isPlayer ? new Color(1f, 0.45f, 0.45f) : new Color(1f, 0.85f, 0.45f), 16, 4.8f);
                AtmospherePostEffect.PulseImpact(isPlayer ? 0.85f : 0.45f);
            }
            lastHealth = now;
        }

        private void CheckPlayerMovementFx()
        {
            bool dashing = motor.Defense != null && motor.Defense.State.Phase == DefensePhase.Dash;
            if (dashing && !wasDashing)
                EmitBurst(transform.position + Vector3.up * 0.85f, new Color(1f, 0.82f, 0.45f), 10, 3.2f);
            wasDashing = dashing;

            if (controller != null && controller.Sliding && Time.time >= nextSlideDust)
            {
                EmitBurst(transform.position + Vector3.up * 0.9f, new Color(0.92f, 0.88f, 0.78f, 0.75f), 4, 1.4f);
                nextSlideDust = Time.time + 0.08f;
            }
        }

        private void OnLanded()
        {
            EmitBurst(transform.position + Vector3.up * 0.08f, new Color(0.92f, 0.88f, 0.78f, 0.75f), 9, 2.2f);
        }

        private void OnAttackStarted()
        {
            if (isPlayer && motor.ComboStep > 0) return;
            Vector3 at = transform.position + new Vector3(motor.Facing * 0.95f, 1.02f, 0f);
            Color sparkColor = isPlayer ? new Color(0.95f, 0.92f, 0.65f) : new Color(0.62f, 0.86f, 1f);
            EmitBurst(at, sparkColor, 8, 3.0f);
            if (!isPlayer)
            {
                SpawnAdditiveSlash("fx-silver-stitch-additive",
                    at, motor.Facing, 1.85f, 0.22f,
                    new Color(0.75f, 0.92f, 1f, 0.82f), 0f);
            }
        }

        public static void SpawnRotatedSlash(string resourceName, Vector3 position, int facing, float worldSize, float duration, float angleDeg, Color tint)
        {
            SpawnAdditiveSlash(resourceName, position, facing, worldSize, duration, tint, angleDeg);
        }

        public static void SpawnSkillFx(string skillId, Vector3 position, int facing, float scale = 2.6f, float duration = 0.34f)
        {
            string res;
            Color tint;
            switch (skillId)
            {
                case SkillRules.SilverStitch:
                    res = "fx-silver-stitch-additive"; tint = new Color(0.92f, 0.98f, 1f, 0.95f); break;
                case BenefactorIds.Pendulum:
                    res = "fx-pendulum-rewind-additive"; tint = new Color(1f, 0.88f, 0.52f, 0.95f); break;
                case BenefactorIds.Waltz:
                    res = "fx-curtain-call-waltz-additive"; tint = new Color(1f, 0.78f, 0.88f, 0.95f); break;
                case BenefactorIds.Forge:
                    res = "fx-resonance-forge-additive"; tint = new Color(1f, 0.76f, 0.45f, 0.95f); break;
                case BenefactorIds.Canvas:
                    res = "fx-canvas-carve-additive"; tint = new Color(0.72f, 0.95f, 1f, 0.95f); break;
                default:
                    res = "fx-basic-sword-mastery-additive"; tint = new Color(1f, 0.92f, 0.68f, 0.95f); break;
            }
            SpawnAdditiveSlash(res, position, facing, scale, duration, tint);
        }

        private static void SpawnAdditiveSlash(string resourceName, Vector3 position, int facing, float worldSize, float duration, Color tint, float angleDeg = 0f)
        {
            if (!FxSprites.TryGetValue(resourceName, out var sprite) || sprite == null)
            {
                var tex = Resources.Load<Texture2D>("GameArt/sprites/" + resourceName);
                if (tex == null) return;
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f, Mathf.Max(tex.width, tex.height));
                FxSprites[resourceName] = sprite;
            }
            if (SharedAdditiveSpriteMaterial == null)
            {
                var sh = Resources.Load<Shader>("Shaders/AdditiveSprite") ?? Shader.Find("Sprites/Default");
                SharedAdditiveSpriteMaterial = new Material(sh);
            }
            var go = new GameObject("Additive FX " + resourceName);
            if (SkillStroke.WorldRoot != null) go.transform.SetParent(SkillStroke.WorldRoot, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
            go.transform.localScale = new Vector3((facing < 0 ? -1f : 1f) * worldSize, worldSize, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = SharedAdditiveSpriteMaterial;
            sr.color = tint;
            sr.sortingOrder = 19;
            go.AddComponent<AdditiveFxFader>().Initialize(sr, duration, worldSize, facing < 0 ? -1f : 1f);
        }

        private void EmitBurst(Vector3 worldPos, Color color, int count, float speed)
        {
            if (burstParticles == null) return;
            var main = burstParticles.main;
            main.startColor = color;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.45f, speed);
            var emitParams = new ParticleSystem.EmitParams { position = worldPos };
            burstParticles.Emit(emitParams, count);
        }

        private void OnDestroy()
        {
            if (motor != null)
            {
                motor.Landed -= OnLanded;
                motor.AttackStarted -= OnAttackStarted;
            }
            if (shadowSprite != null) Destroy(shadowSprite);
            if (shadowTexture != null) Destroy(shadowTexture);
            if (sparkTexture != null) Destroy(sparkTexture);
            if (additiveMaterial != null) Destroy(additiveMaterial);
        }

        private sealed class AdditiveFxFader : MonoBehaviour
        {
            private SpriteRenderer sr;
            private float life, age, baseSize, sign;
            private Color baseColor;

            public void Initialize(SpriteRenderer renderer, float duration, float size, float flipSign)
            {
                sr = renderer;
                life = Mathf.Max(0.06f, duration);
                baseSize = size;
                sign = flipSign;
                baseColor = renderer.color;
            }

            private void Update()
            {
                age += Time.deltaTime;
                float t = age / life;
                if (t >= 1f) { Destroy(gameObject); return; }
                float pop = Mathf.Lerp(0.75f, 1.18f, 1f - (1f - t) * (1f - t));
                transform.localScale = new Vector3(sign * baseSize * pop, baseSize * pop, 1f);
                var c = baseColor;
                c.a = baseColor.a * (1f - t * t);
                sr.color = c;
            }
        }
    }
}
