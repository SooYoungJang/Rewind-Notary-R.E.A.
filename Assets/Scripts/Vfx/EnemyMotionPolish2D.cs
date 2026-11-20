using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    [DefaultExecutionOrder(300)]
    public sealed class EnemyMotionPolish2D : MonoBehaviour
    {
        [Serializable] private sealed class SheetLayouts { public SheetLayout[] sheets; }
        [Serializable] private sealed class SheetLayout
        {
            public string name;
            public float pixelsPerUnit;
            public float[] footPivots;
        }

        private static SheetLayout[] sheetLayouts;
        private WaveTarget2D target;
        private CombatMotor2D player;
        private SpriteRenderer pictureRenderer;
        private Transform pictureTransform;
        private Vector3 baseLocalPos;
        private Vector3 baseLocalScale;
        private SpriteRenderer shadowRenderer;
        private SpriteRenderer stateRing;
        private SpriteRenderer telegraphRing;
        private GameObject hpBarRoot;
        private Transform hpBarFill;
        private SpriteRenderer hpBarFillSr;
        private int maxRemaining = 1;
        private static Sprite sharedWhiteSprite;
        private Sprite[] sheetFrames;
        private readonly List<Sprite> ownedSprites = new List<Sprite>();
        private int lastRemaining = -1;
        private bool wasCleared;
        private float hitRecoilUntil;
        private float animClock;
        private float phaseOffset;
        private static Texture2D sharedShadowTex;
        private static Sprite sharedShadowSprite;
        private static Texture2D sharedRingTex;
        private static Sprite sharedRingSprite;
        private static Material sharedAdditiveMat;

        public void Initialize(WaveTarget2D owner, CombatMotor2D playerActor)
        {
            target = owner;
            player = playerActor;
            phaseOffset = Mathf.Abs(transform.position.x * 1.37f + transform.position.y * 0.73f);
            var child = transform.Find("Target silhouette");
            if (child == null) return;
            pictureTransform = child;
            pictureRenderer = child.GetComponent<SpriteRenderer>();
            if (pictureRenderer == null) return;

            baseLocalPos = pictureTransform.localPosition;
            baseLocalScale = pictureTransform.localScale;
            lastRemaining = target.State != null ? target.State.Remaining : 1;
            maxRemaining = Mathf.Max(1, lastRemaining);
            wasCleared = target.State != null && target.State.Cleared;

            EnsureSharedAssets();
            TryLoadAnimatedSheet();
            BuildContactShadow();
            BuildStateRings();
            BuildOverheadHealthBar();
        }

        private static void EnsureSharedAssets()
        {
            if (sharedShadowTex == null)
            {
                const int w = 64, h = 32;
                sharedShadowTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                sharedShadowTex.wrapMode = TextureWrapMode.Clamp;
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float nx = (x - (w - 1) * 0.5f) / (w * 0.5f);
                        float ny = (y - (h - 1) * 0.5f) / (h * 0.5f);
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny));
                        px[y * w + x] = new Color(0.02f, 0.03f, 0.06f, a * a * 0.54f);
                    }
                }
                sharedShadowTex.SetPixels(px);
                sharedShadowTex.Apply();
                sharedShadowSprite = Sprite.Create(sharedShadowTex, new Rect(0, 0, w, h), Vector2.one * 0.5f, 32f);
            }
            if (sharedRingTex == null)
            {
                const int s = 64;
                sharedRingTex = new Texture2D(s, s, TextureFormat.RGBA32, false);
                sharedRingTex.wrapMode = TextureWrapMode.Clamp;
                var px = new Color[s * s];
                float half = (s - 1) * 0.5f;
                for (int y = 0; y < s; y++)
                {
                    for (int x = 0; x < s; x++)
                    {
                        float dx = (x - half) / half;
                        float dy = (y - half) / half;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Exp(-Mathf.Pow((d - 0.80f) / 0.09f, 2f)) * Mathf.Clamp01(1.04f - d);
                        px[y * s + x] = new Color(1f, 1f, 1f, a);
                    }
                }
                sharedRingTex.SetPixels(px);
                sharedRingTex.Apply();
                sharedRingSprite = Sprite.Create(sharedRingTex, new Rect(0, 0, s, s), Vector2.one * 0.5f, s);
            }
            if (sharedAdditiveMat == null)
            {
                var sh = Resources.Load<Shader>("Shaders/AdditiveSprite") ?? Shader.Find("Sprites/Default");
                sharedAdditiveMat = new Material(sh);
            }
            if (sharedWhiteSprite == null)
                sharedWhiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2f);
        }

        private void TryLoadAnimatedSheet()
        {
            if (pictureRenderer.sprite == null || pictureRenderer.sprite.texture == null) return;
            string texName = pictureRenderer.sprite.texture.name;
            if (string.IsNullOrEmpty(texName)) return;
            var sheetTex = Resources.Load<Texture2D>("GameArt/sprites/" + texName + "-sheet");
            if (sheetTex == null) return;

            float cw = sheetTex.width / 4f;
            float ch = sheetTex.height / 2f;
            var layout = FindSheetLayout(sheetTex.name);
            bool groundedFrames = layout?.footPivots?.Length == 8 && layout.pixelsPerUnit > 0f;
            float pixelsPerUnit = groundedFrames ? layout.pixelsPerUnit : ch / 1.8f;
            sheetFrames = new Sprite[8];
            for (int r = 0; r < 2; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    int frame = r * 4 + c;
                    float pivotY = groundedFrames ? layout.footPivots[frame] : .5f;
                    var sp = Sprite.Create(sheetTex, new Rect(c * cw, sheetTex.height - (r + 1) * ch, cw, ch),
                        new Vector2(.5f, pivotY), pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    ownedSprites.Add(sp);
                    sheetFrames[frame] = sp;
                }
            }
            pictureTransform.localScale = Vector3.one;
            if (groundedFrames) pictureTransform.localPosition = Vector3.zero;
            pictureRenderer.sprite = sheetFrames[0];
        }

        private static SheetLayout FindSheetLayout(string textureName)
        {
            if (sheetLayouts == null)
            {
                var data = Resources.Load<TextAsset>("GameArt/sprites/enemy-sheet-grounding");
                sheetLayouts = data == null ? Array.Empty<SheetLayout>() :
                    JsonUtility.FromJson<SheetLayouts>(data.text)?.sheets ?? Array.Empty<SheetLayout>();
            }
            foreach (var sheet in sheetLayouts)
                if (sheet.name == textureName) return sheet;
            return null;
        }

        private void BuildContactShadow()
        {
            var go = new GameObject("EnemyContactShadow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            go.transform.localScale = new Vector3(0.82f, 0.38f, 1f);
            shadowRenderer = go.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = sharedShadowSprite;
            shadowRenderer.sortingOrder = 1;
        }

        private void BuildStateRings()
        {
            var sGo = new GameObject("EnemyStateRing");
            sGo.transform.SetParent(transform, false);
            sGo.transform.localPosition = new Vector3(0f, 0.92f, 0f);
            stateRing = sGo.AddComponent<SpriteRenderer>();
            stateRing.sprite = sharedRingSprite;
            stateRing.sharedMaterial = sharedAdditiveMat;
            stateRing.sortingOrder = 7;

            var tGo = new GameObject("EnemyTelegraphRing");
            tGo.transform.SetParent(transform, false);
            tGo.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            telegraphRing = tGo.AddComponent<SpriteRenderer>();
            telegraphRing.sprite = sharedRingSprite;
            telegraphRing.sharedMaterial = sharedAdditiveMat;
            telegraphRing.color = new Color(1.0f, 0.65f, 0.24f, 0.92f);
            telegraphRing.sortingOrder = 13;
            telegraphRing.enabled = false;
        }

        private void BuildOverheadHealthBar()
        {
            if (target.Kind != EffectTargetKind.Enemy) return;
            hpBarRoot = new GameObject("EnemyHpBar");
            hpBarRoot.transform.SetParent(transform, false);
            hpBarRoot.transform.localPosition = new Vector3(0f, 2.08f, 0f);

            var border = new GameObject("Border");
            border.transform.SetParent(hpBarRoot.transform, false);
            border.transform.localScale = new Vector3(1.06f, 0.14f, 1f);
            var bSr = border.AddComponent<SpriteRenderer>();
            bSr.sprite = sharedWhiteSprite;
            bSr.color = new Color(0.84f, 0.70f, 0.42f, 0.85f);
            bSr.sortingOrder = 14;

            var bg = new GameObject("Track");
            bg.transform.SetParent(hpBarRoot.transform, false);
            bg.transform.localScale = new Vector3(1.0f, 0.09f, 1f);
            var tSr = bg.AddComponent<SpriteRenderer>();
            tSr.sprite = sharedWhiteSprite;
            tSr.color = new Color(0.06f, 0.08f, 0.12f, 0.92f);
            tSr.sortingOrder = 15;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(hpBarRoot.transform, false);
            fill.transform.localScale = new Vector3(1.0f, 0.085f, 1f);
            hpBarFill = fill.transform;
            hpBarFillSr = fill.AddComponent<SpriteRenderer>();
            hpBarFillSr.sprite = sharedWhiteSprite;
            hpBarFillSr.color = new Color(0.86f, 0.34f, 0.32f, 0.96f);
            hpBarFillSr.sortingOrder = 16;
        }

        private void LateUpdate()
        {
            if (target == null || target.State == null || pictureRenderer == null) return;

            bool cleared = target.State.Cleared;
            int remaining = target.State.Remaining;

            if (!wasCleared && cleared)
            {
                wasCleared = true;
                ActorVisualPolish2D.SpawnSkillFx(SkillRules.BasicSword, transform.position + Vector3.up * 0.95f, 1, 2.2f, 0.28f);
                AtmospherePostEffect.PulseImpact(0.32f);
            }
            else if (wasCleared && !cleared)
            {
                wasCleared = false;
            }

            if (remaining < lastRemaining)
            {
                int delta = lastRemaining - remaining;
                CombatFloatingText2D.SpawnDamage(transform.position + Vector3.up * 1.65f, delta, cleared, false);
                if (!cleared) hitRecoilUntil = Time.time + 0.18f;
            }
            lastRemaining = remaining;

            bool alive = !cleared && pictureRenderer.enabled;
            shadowRenderer.enabled = alive && target.FeetSupported;
            stateRing.enabled = alive;
            if (hpBarRoot != null)
            {
                bool showBar = alive && (remaining < maxRemaining || (player != null && Vector2.Distance(player.transform.position, transform.position) < 9.5f));
                hpBarRoot.SetActive(showBar);
                if (showBar)
                {
                    if (remaining > maxRemaining) maxRemaining = remaining;
                    float ratio = Mathf.Clamp01((float)remaining / Mathf.Max(1, maxRemaining));
                    hpBarFill.localScale = new Vector3(Mathf.Max(0.02f, ratio), 0.085f, 1f);
                    hpBarFill.localPosition = new Vector3(-(1f - ratio) * 0.5f, 0f, 0f);
                    hpBarFillSr.color = target.State.Ready(Time.time)
                        ? new Color(0.32f, 0.86f, 0.78f, 0.96f)
                        : new Color(0.86f, 0.34f, 0.32f, 0.96f);
                }
            }
            if (!alive)
            {
                telegraphRing.enabled = false;
                return;
            }

            animClock += Time.deltaTime;
            float t = animClock + phaseOffset;
            bool winding = !float.IsPositiveInfinity(target.StrikeIn);
            bool ready = target.State.Ready(Time.time);
            bool recoiling = Time.time < hitRecoilUntil;

            if (player != null && target.Kind == EffectTargetKind.Enemy)
                pictureRenderer.flipX = target.Facing < 0;

            if (recoiling)
            {
                pictureRenderer.color = new Color(1.0f, 0.88f, 0.84f, 1f);
            }
            else if (winding)
            {
                float flash = 0.5f + 0.5f * Mathf.Sin(Time.time * 26f);
                pictureRenderer.color = Color.Lerp(Color.white, new Color(1.0f, 0.90f, 0.68f, 1f), flash * 0.45f);
            }
            else
            {
                pictureRenderer.color = Color.white;
            }

            if (sheetFrames != null)
            {
                float sinceStrike = Time.time - target.LastStrikeAt;
                int frame;
                if (recoiling) frame = 7;
                else if (winding) frame = 4;
                else if (sinceStrike < 0.09f) frame = 5;
                else if (sinceStrike < 0.18f) frame = 6;
                else if (target.Walking) frame = 2 + (Mathf.FloorToInt(t * 8f) % 2);
                else frame = Mathf.FloorToInt(t * 4f) % 2;
                pictureRenderer.sprite = sheetFrames[frame];
            }
            else ApplyStillSpriteMotion(t, recoiling, winding);

            float ringPulse = 0.92f + 0.10f * Mathf.Sin(t * 5.2f);
            if (ready)
            {
                stateRing.color = new Color(0.42f, 0.96f, 0.88f, 0.58f);
                stateRing.transform.localScale = new Vector3(1.55f * ringPulse, 1.55f * ringPulse, 1f);
            }
            else
            {
                stateRing.color = new Color(0.84f, 0.72f, 0.45f, 0.28f);
                stateRing.transform.localScale = new Vector3(1.38f * ringPulse, 1.38f * ringPulse, 1f);
            }
            stateRing.transform.localRotation = Quaternion.Euler(0f, 0f, t * 38f);

            telegraphRing.enabled = winding;
            if (winding)
            {
                float s = 1.35f + Mathf.PingPong(Time.time * 5.5f, 0.75f);
                telegraphRing.transform.localScale = new Vector3(s, s, 1f);
                telegraphRing.transform.localRotation = Quaternion.Euler(0f, 0f, -Time.time * 160f);
            }
        }

        private void ApplyStillSpriteMotion(float t, bool recoiling, bool winding)
        {
            float sx = 1f, sy = 1f, dy = 0f, rotZ = 0f;
            if (recoiling)
            {
                sx = 1.16f;
                sy = 0.86f;
                rotZ = (pictureRenderer.flipX ? 1f : -1f) * 10f;
            }
            else if (winding)
            {
                float wind = Mathf.Sin(Time.time * 32f) * 0.05f;
                sx = 1.12f + wind;
                sy = 0.88f - wind;
                rotZ = (pictureRenderer.flipX ? -1f : 1f) * -8f;
            }
            else
            {
                float breath = Mathf.Sin(t * 3.1f);
                sx = 1f - breath * 0.032f;
                sy = 1f + breath * 0.038f;
                dy = Mathf.Max(0f, breath) * 0.045f;
                rotZ = Mathf.Sin(t * 1.8f) * 2.2f;
            }

            pictureTransform.localScale = new Vector3(baseLocalScale.x * sx, baseLocalScale.y * sy, 1f);
            pictureTransform.localPosition = baseLocalPos + new Vector3(0f, dy, 0f);
            pictureTransform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
        }

        private void OnDestroy()
        {
            foreach (var sp in ownedSprites) if (sp != null) Destroy(sp);
            ownedSprites.Clear();
        }
    }
}
