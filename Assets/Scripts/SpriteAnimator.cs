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
        [Serializable] private sealed class SheetLayout { public FramePivot[] frames; public int[] columnEdges; public RowSlice[] rows; }
        [Serializable] private sealed class RowSlice { public int top, bottom; public int[] columnEdges; }
        [Serializable] private sealed class FramePivot { public int row; public int col; public float x; public float y; }
        private readonly Dictionary<Pose, Sprite[]> clips = new Dictionary<Pose, Sprite[]>();
        private readonly List<Sprite> ownedSprites = new List<Sprite>();
        private SpriteRenderer view;
        private Sprite[][] groundAttacks, airAttacks, starterAttacks;
        private Sprite[] activeAttack, groundUp, airUp, airDown;
        private static readonly float[] AirUpTilt = { 0f, -5f, 10f, 5f };
        private static readonly float[] AirDownTilt = { 0f, 8f, -24f, -13f };
        private float[] attackTiltFrames;
        private float attackTilt;
        private bool skipGuard;
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

        public SpriteRenderer Renderer => view;
        public Pose CurrentPose => state;
        public bool IsTelegraphing => telegraph;
        public string ActiveResource { get; private set; }

        private float spriteScale = 1f;

        public void Initialize(string resource, float scale = 1f)
        {
            spriteScale = scale;
            view = GetComponent<SpriteRenderer>();
            var shader = Resources.Load<Shader>("Shaders/HitFlash");
            if (shader != null)
            {
                flashMaterial = new Material(shader);
                view.sharedMaterial = flashMaterial;
                properties = new MaterialPropertyBlock();
            }
            else Debug.LogWarning("Missing HitFlash shader (animation continues).", this);
            LoadSheet(resource, true);
            ready = true;
            Play(Pose.Idle, true);
        }

        public bool SwapSheet(string resource)
        {
            if (string.IsNullOrEmpty(resource) || resource == ActiveResource) return false;
            if (Resources.Load<Texture2D>(resource) == null) return false;
            LoadSheet(resource, false);
            if (ready && clips.TryGetValue(state, out var frames) && frames.Length > 0)
                view.sprite = frames[0];
            return true;
        }

        private void LoadSheet(string resource, bool required)
        {
            Texture2D texture = Resources.Load<Texture2D>(resource);
            if (texture == null)
            {
                if (required) throw new InvalidOperationException("Missing GameArt: " + resource);
                return;
            }
            foreach (Sprite sprite in ownedSprites) if (sprite != null) Destroy(sprite);
            ownedSprites.Clear();
            ActiveResource = resource;
            groundAttacks = airAttacks = starterAttacks = null;
            activeAttack = groundUp = airUp = airDown = null;
            attackTiltFrames = null;
            SetAttackTilt(0f);
            var layout = LoadLayout(resource);
            clips[Pose.Idle] = Slice(texture, layout, 8, 4, 0, 0, 4, 2.5f);
            clips[Pose.Run] = Slice(texture, layout, 8, 4, 1, 0, 8, 2.5f);
            clips[Pose.Jump] = Slice(texture, layout, 8, 4, 2, 0, 1, 2.5f);
            clips[Pose.Fall] = Slice(texture, layout, 8, 4, 2, 1, 1, 2.5f);
            clips[Pose.Land] = Slice(texture, layout, 8, 4, 2, 2, 1, 2.5f);
            clips[Pose.Attack] = Slice(texture, layout, 8, 4, 3, 0, 4, 2.5f);
            const string rea = "GameArt/sprites/rea-sheet";
            if (resource == rea || resource.StartsWith(rea + "-tier", StringComparison.Ordinal))
            {
                groundAttacks = LoadAttackRows(rea + "-ground-attacks", 4, 2.43f);
                airAttacks = LoadAttackRows(rea + "-air-attacks", 4, 2.59f);
                groundUp = new[] { groundAttacks[0][0], groundAttacks[1][1], groundAttacks[1][3], groundAttacks[1][2] };
                airUp = new[] { airAttacks[0][0], airAttacks[1][1], airAttacks[1][3], airAttacks[1][2] };
                airDown = new[] { airAttacks[3][0], airAttacks[3][1], airAttacks[3][2], airAttacks[2][3] };
                const string starter = rea + "-starter-attacks";
                if (Resources.Load<Texture2D>(starter) != null)
                    starterAttacks = LoadAttackRows(starter, 4, 2.45f);
            }
        }

        private static SheetLayout LoadLayout(string resource)
        {
            var layout = Resources.Load<TextAsset>(resource + "-layout");
            return layout == null ? null : JsonUtility.FromJson<SheetLayout>(layout.text);
        }

        private Sprite[][] LoadAttackRows(string resource, int rows, float worldHeight)
        {
            var texture = Resources.Load<Texture2D>(resource);
            if (texture == null) throw new InvalidOperationException("Missing GameArt: " + resource);
            var layout = LoadLayout(resource);
            var attacks = new Sprite[rows][];
            for (int row = 0; row < rows; row++)
                attacks[row] = Slice(texture, layout, 4, rows, row, 0, 4, worldHeight);
            return attacks;
        }

        private Sprite[] Slice(Texture2D texture, SheetLayout layout, int columns, int rows,
            int row, int first, int count, float worldHeight)
        {
            var frames = new Sprite[count];
            float cellWidth = texture.width / (float)columns;
            float cellHeight = texture.height / (float)rows;
            var slice = layout?.rows?[row];
            int top = texture.height - (slice?.top ?? Mathf.RoundToInt(row * cellHeight));
            int bottom = texture.height - (slice?.bottom ?? Mathf.RoundToInt((row + 1) * cellHeight));
            var edges = slice?.columnEdges ?? layout?.columnEdges;
            for (int i = 0; i < count; i++)
            {
                int col = first + i;
                int left = edges?[col] ?? Mathf.RoundToInt(col * cellWidth);
                int right = edges?[col + 1] ?? Mathf.RoundToInt((col + 1) * cellWidth);
                var pivot = layout?.frames == null ? null : Array.Find(layout.frames, p => p.row == row && p.col == col);
                var anchor = pivot == null ? new Vector2(.5f, .04f) : new Vector2(pivot.x, pivot.y);
                frames[i] = Sprite.Create(texture, new Rect(left, bottom, right - left, top - bottom),
                    anchor, cellHeight / (worldHeight * spriteScale), 0, SpriteMeshType.FullRect);
                frames[i].name = texture.name + "_" + row + "_" + col;
                ownedSprites.Add(frames[i]);
            }
            return frames;
        }

        public void Face(int direction)
        {
            bool flip = direction < 0;
            if (view == null || view.flipX == flip) return;
            view.flipX = flip;
            if (attackTilt != 0f) ApplyPoseRotation();
        }
        private int wallPoseSide;
        public void SetWallPose(int side)
        {
            if (wallPoseSide == side) return;
            wallPoseSide = side;
            ApplyPoseRotation();
        }

        private void SetAttackTilt(float tilt)
        {
            if (attackTilt == tilt) return;
            attackTilt = tilt;
            ApplyPoseRotation();
        }

        private void ApplyPoseRotation() =>
            transform.localRotation = Quaternion.Euler(0f, 0f, wallPoseSide * 12f + (view.flipX ? -attackTilt : attackTilt));
        public void Tint(Color color) { baseTint = color; ApplyVisual(); }
        public void SetCombatVisual(bool immune, bool isDead) { invulnerable = immune; dead = isDead; }
        public void SetTelegraph(bool active) { telegraph = active; }
        public void FlashHit() { flashUntil = Time.unscaledTime + HitFlashSeconds; ApplyVisual(); }
        public void ResetVisual()
        {
            SetWallPose(0);
            SetAttackTilt(0f);
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
            SetAttackTilt(0f);
            view.sprite = clips[state][0];
        }
        internal void BeginAttack(int step, bool grounded, bool moving, int starter, int directionY)
        {
            if (!ready) return;
            attackTiltFrames = null;
            skipGuard = directionY == 0 && starter < 0 && step == 1 && grounded && moving && groundAttacks != null;
            if (starter >= 0 && starterAttacks != null) activeAttack = starterAttacks[starter];
            else if (groundAttacks == null || step == 0) activeAttack = clips[Pose.Attack];
            else if (directionY > 0)
            {
                activeAttack = grounded ? groundUp : airUp;
                if (!grounded) attackTiltFrames = AirUpTilt;
            }
            else if (directionY < 0)
            {
                activeAttack = airDown;
                attackTiltFrames = AirDownTilt;
            }
            else activeAttack = (grounded ? groundAttacks : airAttacks)[step - 1];
            state = Pose.Attack;
            ShowAttackFrame(skipGuard ? 1 : 0);
        }

        public void PlayAttack(float attackElapsed, float strikeTime, float duration)
        {
            if (!ready) return;
            state = Pose.Attack;
            // The motor owns this clock, so the hit and slash frame cannot drift apart.
            int index = !skipGuard && attackElapsed < strikeTime * 0.5f ? 0 : attackElapsed < strikeTime ? 1
                : attackElapsed < strikeTime + (duration - strikeTime) * 0.5f ? 2 : 3;
            ShowAttackFrame(index);
        }

        private void ShowAttackFrame(int index)
        {
            view.sprite = (activeAttack ?? clips[Pose.Attack])[index];
            SetAttackTilt(attackTiltFrames?[index] ?? 0f);
        }
        private void Update()
        {
            if (!ready || state == Pose.Attack) return;
            elapsed += Time.deltaTime;
            float fps = state == Pose.Idle ? 6f : 12f;
            var frames = clips[state];
            int index = Mathf.FloorToInt(elapsed * fps) % frames.Length;
            view.sprite = frames[index];
        }
        private void OnDestroy()
        {
            foreach (Sprite sprite in ownedSprites) if (sprite != null) Destroy(sprite);
            if (flashMaterial != null) Destroy(flashMaterial);
        }
    }
}
