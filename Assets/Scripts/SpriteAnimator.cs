using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    // Runtime slicing only. No Animator, animation clips or importer sprite slicing.
    [RequireComponent(typeof(SpriteRenderer))]
    [DefaultExecutionOrder(200)]
    public sealed class SpriteAnimator : MonoBehaviour
    {
        public enum Pose { Idle, Run, Jump, Fall, Land, Attack }
        [Serializable] private sealed class SheetLayout { public FramePivot[] frames; }
        [Serializable] private sealed class FramePivot { public int row; public int col; public float x; public float y; }
        private readonly Dictionary<Pose, Sprite[]> clips = new Dictionary<Pose, Sprite[]>();
        private readonly List<Sprite> ownedSprites = new List<Sprite>();
        private SpriteRenderer view;
        private Pose state;
        private float elapsed;
        private bool ready;
        public const float HitFlashSeconds = 0.09f;
        private static readonly int FlashProperty = Shader.PropertyToID("_HitFlashAmount");
        private Material flashMaterial;
        private MaterialPropertyBlock properties;
        private Color baseTint = Color.white;
        private bool invulnerable, dead, telegraph;
        private float flashUntil;

        public void Initialize(string resource)
        {
            view = GetComponent<SpriteRenderer>();
            var shader = Resources.Load<Shader>("Shaders/HitFlash");
            if (shader != null)
            {
                flashMaterial = new Material(shader);
                view.sharedMaterial = flashMaterial;
                properties = new MaterialPropertyBlock();
            }
            else Debug.LogWarning("Missing HitFlash shader (animation continues).", this);
            Texture2D texture = Resources.Load<Texture2D>(resource);
            if (texture == null) throw new InvalidOperationException("Missing GameArt: " + resource);
            var layoutFile = Resources.Load<TextAsset>(resource + "-layout");
            SheetLayout layout = layoutFile == null ? null : JsonUtility.FromJson<SheetLayout>(layoutFile.text);
            float w = texture.width / 8f, h = texture.height / 4f;
            Sprite[] Slice(int row, int first, int count)
            {
                var frames = new Sprite[count];
                for (int i = 0; i < count; i++)
                {
                    int col = first + i;
                    Vector2 pivot = new Vector2(0.5f, 0.04f);
                    if (layout != null && layout.frames != null)
                        foreach (FramePivot p in layout.frames)
                            if (p.row == row && p.col == col) { pivot = new Vector2(p.x, p.y); break; }
                    frames[i] = Sprite.Create(texture, new Rect(col * w, texture.height - (row + 1) * h, w, h),
                        pivot, h / 2.5f, 0, SpriteMeshType.FullRect);
                    frames[i].name = resource + "_" + row + "_" + col;
                    ownedSprites.Add(frames[i]);
                }
                return frames;
            }
            clips[Pose.Idle] = Slice(0, 0, 4);
            clips[Pose.Run] = Slice(1, 0, 8);
            clips[Pose.Jump] = Slice(2, 0, 1);
            clips[Pose.Fall] = Slice(2, 1, 1);
            clips[Pose.Land] = Slice(2, 2, 1);
            clips[Pose.Attack] = Slice(3, 0, 4);
            ready = true;
            Play(Pose.Idle, true);
        }

        public void Face(int direction) { if (view != null) view.flipX = direction < 0; }
        public void Tint(Color color) { baseTint = color; ApplyVisual(); }
        public void SetCombatVisual(bool immune, bool isDead) { invulnerable = immune; dead = isDead; }
        public void SetTelegraph(bool active) { telegraph = active; }
        public void FlashHit() { flashUntil = Time.unscaledTime + HitFlashSeconds; ApplyVisual(); }
        public void ResetVisual()
        {
            flashUntil = 0f; dead = invulnerable = telegraph = false;
            ApplyVisual();
        }
        private void LateUpdate() { ApplyVisual(); }
        private void ApplyVisual()
        {
            if (view == null) return;
            bool flash = Time.unscaledTime < flashUntil;
            // White flash > death > immunity flicker > attack warning > base tint.
            if (flash) view.color = Color.white;
            else if (dead) view.color = new Color(0.55f, 0.6f, 0.7f, 0.55f);
            else if (invulnerable && Mathf.FloorToInt(Time.time * 16f) % 2 == 0)
                view.color = new Color(1f, 0.65f, 0.65f, 0.65f);
            else if (telegraph)
                view.color = Color.Lerp(baseTint, new Color(1f, 0.65f, 0.25f), 0.5f + 0.5f * Mathf.Sin(Time.time * 24f));
            else view.color = baseTint;
            if (properties == null) return;
            view.GetPropertyBlock(properties);
            properties.SetFloat(FlashProperty, flash ? 1f : 0f);
            view.SetPropertyBlock(properties);
        }
        public void Play(Pose next, bool restart = false)
        {
            if (!ready || (state == next && !restart)) return;
            state = next;
            elapsed = 0f;
            view.sprite = clips[state][0];
        }
        private void Update()
        {
            if (!ready) return;
            elapsed += Time.deltaTime;
            float fps = state == Pose.Idle ? 6f : state == Pose.Attack ? 12.5f : 12f;
            var frames = clips[state];
            int index = Mathf.FloorToInt(elapsed * fps);
            index = state == Pose.Attack ? Mathf.Min(index, frames.Length - 1) : index % frames.Length;
            view.sprite = frames[index];
        }
        private void OnDestroy()
        {
            foreach (Sprite sprite in ownedSprites) if (sprite != null) Destroy(sprite);
            if (flashMaterial != null) Destroy(flashMaterial);
        }
    }
}
