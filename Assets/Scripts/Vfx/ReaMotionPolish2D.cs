using UnityEngine;

namespace RewindNotary
{
    public sealed class ReaMotionPolish2D : MonoBehaviour
    {
        private CombatMotor2D motor;
        private ReaGrowthVisual2D growth;
        private Transform visual;
        private Vector2 springScale = Vector2.one;
        private bool wasGrounded = true;
        private float nextDashGhost;
        private int lastParryCount;
        private SpriteRenderer parryRing;
        private Texture2D ringTex;
        private Sprite ringSprite;
        private Material additiveMat;
        private float counterWaveAge = 99f;
        private SpriteRenderer counterWave;

        public void Initialize(CombatMotor2D owner, ReaGrowthVisual2D growthVisual)
        {
            motor = owner;
            growth = growthVisual;
            visual = owner.Animation.transform;
            motor.Landed += OnLanded;
            motor.AttackStarted += OnAttackStarted;
            BuildParryVisuals();
        }

        private void BuildParryVisuals()
        {
            const int size = 96;
            ringTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            ringTex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float outer = Mathf.Exp(-Mathf.Pow((d - 0.84f) / 0.06f, 2f));
                    float inner = Mathf.Exp(-Mathf.Pow((d - 0.62f) / 0.04f, 2f)) * 0.55f;
                    float a = Mathf.Clamp01((outer + inner) * Mathf.Clamp01(1.04f - d));
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            ringTex.SetPixels(px);
            ringTex.Apply();
            ringSprite = Sprite.Create(ringTex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);

            Shader sh = Resources.Load<Shader>("Shaders/AdditiveSprite") ?? Shader.Find("Sprites/Default");
            additiveMat = new Material(sh);

            var pGo = new GameObject("ParryNotarySeal");
            pGo.transform.SetParent(transform, false);
            pGo.transform.localPosition = Vector3.up * 0.98f;
            parryRing = pGo.AddComponent<SpriteRenderer>();
            parryRing.sprite = ringSprite;
            parryRing.sharedMaterial = additiveMat;
            parryRing.color = new Color(1.0f, 0.84f, 0.42f, 0.92f);
            parryRing.sortingOrder = 15;
            parryRing.enabled = false;

            var cGo = new GameObject("ParryCounterWave");
            cGo.transform.SetParent(transform, false);
            cGo.transform.localPosition = Vector3.up * 0.98f;
            counterWave = cGo.AddComponent<SpriteRenderer>();
            counterWave.sprite = ringSprite;
            counterWave.sharedMaterial = additiveMat;
            counterWave.sortingOrder = 16;
            counterWave.enabled = false;
        }

        private void OnLanded()
        {
            springScale = new Vector2(1.22f, 0.80f);
        }

        private void OnAttackStarted()
        {
            int step = Mathf.Clamp(motor.ComboStep, 1, 4);
            int tier = growth != null ? growth.CurrentTier : 1;
            float size = (step == 1 ? 1.95f : step == 2 ? 2.25f : step == 3 ? 2.65f : 3.25f) * (1f + (tier - 1) * 0.10f);
            int dirY = motor.AttackDirectionY;
            float angle = dirY == -1 ? -90f : dirY == 1 ? 90f : (step == 1 ? 0f : step == 2 ? 18f : step == 3 ? -15f : 6f) * motor.Facing;
            Color tint = step == 4 || tier >= 3
                ? new Color(1.0f, 0.92f, 0.60f, 0.95f)
                : step == 3 ? new Color(1.0f, 0.86f, 0.56f, 0.90f)
                : step == 2 ? new Color(0.70f, 0.94f, 1.0f, 0.90f)
                : new Color(0.92f, 0.97f, 1.0f, 0.86f);
            Vector3 at = dirY == -1
                ? transform.position + new Vector3(0f, -0.15f, 0f)
                : dirY == 1
                    ? transform.position + new Vector3(0f, 2.05f, 0f)
                    : transform.position + new Vector3(motor.Facing * (0.92f + step * 0.08f), 1.02f, 0f);
            ActorVisualPolish2D.SpawnRotatedSlash("fx-basic-sword-mastery-additive", at, motor.Facing, size, 0.22f + step * 0.03f, angle, tint);
            springScale = step == 4 ? new Vector2(1.20f, 0.86f) : new Vector2(1.10f, 0.92f);
            if (step == 4)
            {
                if (motor.Animation.Renderer != null) SkillStroke.Afterimage(motor.Animation.Renderer, 0.24f);
                AtmospherePostEffect.PulseImpact(0.50f);
            }
        }

        private void LateUpdate()
        {
            if (motor == null || visual == null) return;
            float dt = Time.deltaTime;

            if (wasGrounded && !motor.Grounded && motor.Body.linearVelocity.y > 2.5f)
                springScale = new Vector2(0.84f, 1.18f);
            wasGrounded = motor.Grounded;

            var phase = motor.Defense != null ? motor.Defense.State.Phase : DefensePhase.Ready;
            if (phase == DefensePhase.Dash)
            {
                springScale = Vector2.Lerp(springScale, new Vector2(1.26f, 0.84f), dt * 22f);
                if (Time.time >= nextDashGhost && motor.Animation.Renderer != null)
                {
                    SkillStroke.Afterimage(motor.Animation.Renderer, 0.20f);
                    nextDashGhost = Time.time + 0.045f;
                }
            }
            else
            {
                springScale = Vector2.Lerp(springScale, Vector2.one, 1f - Mathf.Exp(-dt * 14f));
            }

            visual.localScale = new Vector3(springScale.x, springScale.y, 1f);

            bool parrying = phase == DefensePhase.ParryWindow;
            parryRing.enabled = parrying;
            if (parrying)
            {
                float s = 1.65f + Mathf.Sin(Time.time * 28f) * 0.12f;
                parryRing.transform.localScale = new Vector3(s, s, 1f);
                parryRing.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 140f);
            }

            if (motor.Skills != null)
            {
                int parryNow = motor.Skills.ParrySuccessCount;
                if (parryNow > lastParryCount)
                {
                    counterWaveAge = 0f;
                    AtmospherePostEffect.PulseImpact(0.85f);
                }
                lastParryCount = parryNow;
            }

            if (counterWaveAge < 0.34f)
            {
                counterWaveAge += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(counterWaveAge / 0.34f);
                counterWave.enabled = true;
                float s = Mathf.Lerp(1.2f, 4.6f, 1f - (1f - u) * (1f - u));
                counterWave.transform.localScale = new Vector3(s, s, 1f);
                counterWave.color = new Color(1.0f, 0.90f, 0.54f, (1f - u) * 0.95f);
            }
            else
            {
                counterWave.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (motor != null)
            {
                motor.Landed -= OnLanded;
                motor.AttackStarted -= OnAttackStarted;
            }
            if (ringSprite != null) Destroy(ringSprite);
            if (ringTex != null) Destroy(ringTex);
            if (additiveMat != null) Destroy(additiveMat);
        }
    }
}
