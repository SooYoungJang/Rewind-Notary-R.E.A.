using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed class ReaGrowthVisual2D : MonoBehaviour
    {
        private CombatMotor2D motor;
        private Material additiveMat;
        private Texture2D glowTex;
        private Texture2D ringTex;
        private Sprite glowSprite;
        private Sprite ringSprite;
        private SpriteRenderer swordGlow;
        private SpriteRenderer swordHalo;
        private SpriteRenderer[] ribbonNodes;
        private Vector3[] ribbonPositions;
        private SpriteRenderer pendulumOrb;
        private SpriteRenderer waltzAura;
        private SpriteRenderer forgeRing;
        private SpriteRenderer canvasOrb;
        private int currentTier = -1;
        private int lastSkillMask = -1;

        public int CurrentTier => Mathf.Max(1, currentTier);

        public void Initialize(CombatMotor2D owner)
        {
            motor = owner;
            BuildProceduralAssets();
            BuildOverlays();
            RefreshGrowth(true);
        }

        private void BuildProceduralAssets()
        {
            glowTex = CreateRadialTexture(64, false);
            ringTex = CreateRadialTexture(64, true);
            glowSprite = Sprite.Create(glowTex, new Rect(0, 0, 64, 64), Vector2.one * 0.5f, 64f);
            ringSprite = Sprite.Create(ringTex, new Rect(0, 0, 64, 64), Vector2.one * 0.5f, 64f);
            Shader sh = Resources.Load<Shader>("Shaders/AdditiveSprite") ?? Shader.Find("Sprites/Default");
            additiveMat = new Material(sh);
        }

        private static Texture2D CreateRadialTexture(int size, bool ring)
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
                        ? Mathf.Exp(-Mathf.Pow((d - 0.78f) / 0.12f, 2f)) * Mathf.Clamp01(1.05f - d)
                        : Mathf.Pow(Mathf.Clamp01(1f - d), 2.4f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private SpriteRenderer MakeSprite(string label, Sprite sprite, int order, Vector3 scale)
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

        private void BuildOverlays()
        {
            swordGlow = MakeSprite("SwordGrowthAura", glowSprite, 12, new Vector3(1.1f, 0.36f, 1f));
            swordHalo = MakeSprite("SwordGrowthHalo", ringSprite, 11, new Vector3(0.7f, 0.7f, 1f));

            ribbonNodes = new SpriteRenderer[5];
            ribbonPositions = new Vector3[5];
            for (int i = 0; i < ribbonNodes.Length; i++)
            {
                float s = Mathf.Lerp(0.32f, 0.14f, i / 4f);
                ribbonNodes[i] = MakeSprite("SilverRibbon_" + i, glowSprite, 9, new Vector3(s * 1.4f, s * 0.7f, 1f));
                ribbonNodes[i].color = i % 2 == 0
                    ? new Color(0.92f, 0.34f, 0.42f, 0.72f)
                    : new Color(0.88f, 0.96f, 1.0f, 0.78f);
                ribbonNodes[i].enabled = false;
                ribbonPositions[i] = transform.position + Vector3.up * 1.05f;
            }

            pendulumOrb = MakeSprite("JuliusPendulumMark", ringSprite, 11, new Vector3(0.46f, 0.46f, 1f));
            pendulumOrb.color = new Color(1.0f, 0.80f, 0.36f, 0.78f);
            pendulumOrb.enabled = false;

            waltzAura = MakeSprite("ValeriaWaltzMark", glowSprite, 9, new Vector3(1.05f, 0.55f, 1f));
            waltzAura.color = new Color(1.0f, 0.52f, 0.72f, 0.42f);
            waltzAura.enabled = false;

            forgeRing = MakeSprite("GabrielForgeMark", ringSprite, 8, new Vector3(1.25f, 0.38f, 1f));
            forgeRing.color = new Color(0.38f, 0.88f, 0.96f, 0.52f);
            forgeRing.enabled = false;

            canvasOrb = MakeSprite("IrinaCanvasMark", glowSprite, 12, new Vector3(0.48f, 0.48f, 1f));
            canvasOrb.color = new Color(0.58f, 0.88f, 1.0f, 0.70f);
            canvasOrb.enabled = false;
        }

        private void LateUpdate()
        {
            if (motor == null) return;
            RefreshGrowth(false);
            AnimateVisuals();
        }

        private void RefreshGrowth(bool force)
        {
            int tier = 1;
            int mask = 0;
            var session = motor.Skills;
            if (session != null && session.Progression?.Record != null)
            {
                var record = session.Progression.Record;
                var sword = record.Get(SkillRules.BasicSword);
                tier = session.PreviewStage > 0 ? session.PreviewStage : (sword != null ? sword.stage : 1);
                int benefactorCount = 0;
                for (int i = 0; i < BenefactorIds.All.Length; i++)
                {
                    var p = record.Get(BenefactorIds.All[i]);
                    if (p != null && p.acquired)
                    {
                        mask |= 1 << i;
                        benefactorCount++;
                    }
                }
                if (tier >= 3 && benefactorCount >= 3) tier = 4;
            }

            if (!force && tier == currentTier && mask == lastSkillMask) return;
            currentTier = tier;
            lastSkillMask = mask;

            string tierSheet = tier >= 2 ? "GameArt/sprites/rea-sheet-tier" + tier : "GameArt/sprites/rea-sheet";
            if (!motor.Animation.SwapSheet(tierSheet) && tier == 1)
                motor.Animation.SwapSheet("GameArt/sprites/rea-sheet");

            bool hasSilver = (mask & (1 << 0)) != 0;
            bool hasPendulum = (mask & (1 << 1)) != 0;
            bool hasWaltz = (mask & (1 << 2)) != 0;
            bool hasForge = (mask & (1 << 3)) != 0;
            bool hasCanvas = (mask & (1 << 4)) != 0;

            for (int i = 0; i < ribbonNodes.Length; i++) ribbonNodes[i].enabled = hasSilver;
            pendulumOrb.enabled = hasPendulum;
            waltzAura.enabled = hasWaltz;
            forgeRing.enabled = hasForge;
            canvasOrb.enabled = hasCanvas;

            switch (tier)
            {
                case 1:
                    swordGlow.color = new Color(0.75f, 0.90f, 1.0f, 0.24f);
                    swordGlow.transform.localScale = new Vector3(0.78f, 0.24f, 1f);
                    swordHalo.enabled = false;
                    break;
                case 2:
                    swordGlow.color = new Color(0.45f, 0.88f, 1.0f, 0.56f);
                    swordGlow.transform.localScale = new Vector3(1.18f, 0.34f, 1f);
                    swordHalo.enabled = true;
                    swordHalo.color = new Color(0.42f, 0.86f, 1.0f, 0.34f);
                    swordHalo.transform.localScale = new Vector3(0.62f, 0.62f, 1f);
                    break;
                case 3:
                    swordGlow.color = new Color(1.0f, 0.84f, 0.42f, 0.78f);
                    swordGlow.transform.localScale = new Vector3(1.52f, 0.44f, 1f);
                    swordHalo.enabled = true;
                    swordHalo.color = new Color(1.0f, 0.82f, 0.38f, 0.55f);
                    swordHalo.transform.localScale = new Vector3(0.88f, 0.88f, 1f);
                    break;
                default:
                    swordGlow.color = new Color(1.0f, 0.94f, 0.72f, 0.90f);
                    swordGlow.transform.localScale = new Vector3(1.78f, 0.54f, 1f);
                    swordHalo.enabled = true;
                    swordHalo.color = new Color(0.98f, 0.90f, 0.60f, 0.72f);
                    swordHalo.transform.localScale = new Vector3(1.12f, 1.12f, 1f);
                    break;
            }
        }

        private void AnimateVisuals()
        {
            bool dead = motor.Health != null && motor.Health.Dead;
            float t = Time.time;
            int f = motor.Facing;
            bool attacking = motor.Attacking;

            swordGlow.enabled = !dead;
            if (!dead)
            {
                float pulse = 0.92f + 0.10f * Mathf.Sin(t * (attacking ? 22f : 4.5f));
                Vector3 handOffset = attacking
                    ? new Vector3(f * 0.82f, 0.96f, 0f)
                    : new Vector3(f * 0.46f, 0.62f, 0f);
                handOffset *= CombatMotor2D.ReaVisualScale;
                swordGlow.transform.localPosition = handOffset;
                swordGlow.transform.localRotation = Quaternion.Euler(0f, 0f, attacking ? (f * -8f) : (f * -32f));
                float baseScaleX = currentTier == 1 ? 0.78f : currentTier == 2 ? 1.18f : currentTier == 3 ? 1.52f : 1.78f;
                float baseScaleY = currentTier == 1 ? 0.24f : currentTier == 2 ? 0.34f : currentTier == 3 ? 0.44f : 0.54f;
                swordGlow.transform.localScale = new Vector3(baseScaleX * pulse * (attacking ? 1.35f : 1f), baseScaleY * pulse, 1f);

                if (swordHalo.enabled)
                {
                    swordHalo.transform.localPosition = handOffset;
                    swordHalo.transform.localRotation = Quaternion.Euler(0f, 0f, t * 55f * f);
                }
            }

            if (ribbonNodes[0].enabled)
            {
                Vector3 anchor = transform.position + new Vector3(-f * 0.24f, 1.08f, 0f) * CombatMotor2D.ReaVisualScale;
                ribbonPositions[0] = anchor;
                for (int i = 1; i < ribbonPositions.Length; i++)
                {
                    Vector3 desired = ribbonPositions[i - 1] + new Vector3(-f * 0.22f, -0.05f + Mathf.Sin(t * 7f - i * 1.1f) * 0.06f, 0f);
                    ribbonPositions[i] = Vector3.Lerp(ribbonPositions[i], desired, Mathf.Clamp01(Time.deltaTime * 18f));
                    ribbonNodes[i].transform.position = ribbonPositions[i];
                }
                ribbonNodes[0].transform.position = anchor;
            }

            if (pendulumOrb.enabled)
            {
                pendulumOrb.transform.localPosition = new Vector3(-f * 0.42f + Mathf.Sin(t * 3.2f) * 0.10f, 1.48f + Mathf.Cos(t * 3.2f) * 0.08f, 0f) * CombatMotor2D.ReaVisualScale;
                pendulumOrb.transform.localRotation = Quaternion.Euler(0f, 0f, t * 90f);
            }

            if (waltzAura.enabled)
            {
                float p = 0.9f + 0.14f * Mathf.Sin(t * 5.2f);
                waltzAura.transform.localPosition = new Vector3(0f, 0.52f + Mathf.Sin(t * 3.6f) * 0.08f, 0f);
                waltzAura.transform.localScale = new Vector3(1.05f * p, 0.55f * p, 1f);
            }

            if (forgeRing.enabled)
            {
                float p = 0.94f + 0.09f * Mathf.Sin(t * 4.0f);
                forgeRing.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                forgeRing.transform.localScale = new Vector3(1.25f * p, 0.36f * p, 1f);
            }

            if (canvasOrb.enabled)
            {
                canvasOrb.transform.localPosition = new Vector3(f * 0.38f + Mathf.Cos(t * 4.4f) * 0.12f, 1.32f + Mathf.Sin(t * 4.4f) * 0.10f, 0f) * CombatMotor2D.ReaVisualScale;
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
