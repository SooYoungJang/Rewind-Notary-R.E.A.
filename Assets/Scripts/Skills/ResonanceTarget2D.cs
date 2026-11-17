using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed class ResonanceTarget2D : MonoBehaviour
    {
        public static readonly List<ResonanceTarget2D> All = new List<ResonanceTarget2D>();
        public int Marks { get; private set; }
        public bool Guarded { get; private set; }
        public Bounds Bounds => GetComponent<Collider2D>().bounds;
        private float markUntil;
        private bool mechanism;
        private Sprite sprite;
        private SpriteRenderer visual;
        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }
        public void InitializeMechanism()
        {
            mechanism = true; Guarded = true; gameObject.layer = LevelBuilder.GroundLayer;
            var box = gameObject.AddComponent<BoxCollider2D>(); box.size = new Vector2(SkillGeometry.GuardWidth, SkillGeometry.GuardHeight);
            sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2);
            visual = gameObject.AddComponent<SpriteRenderer>(); visual.sprite = sprite; visual.sortingOrder = 4;
            visual.drawMode = SpriteDrawMode.Sliced; visual.size = box.size; visual.color = Color.yellow;
        }
        public bool BreakGuard()
        {
            if (!Guarded) return false;
            Guarded = false;
            if (mechanism) { GetComponent<Collider2D>().enabled = false; visual.enabled = false; }
            return true;
        }
        public int LiveMarks => Time.time < markUntil ? Marks : 0;
        // Called only after a real enemy HP change. Immune hits do not manufacture marks.
        public bool AddMark(int limit, float seconds)
        {
            if (limit <= 0) return false;
            Marks = Mathf.Min(LiveMarks + 1, limit); markUntil = Time.time + seconds;
            bool burst = Marks == limit;
            if (burst) Marks = 0;
            return burst;
        }
        public void ResetAttempt()
        {
            Marks = 0; markUntil = 0;
            if (mechanism) { Guarded = true; GetComponent<Collider2D>().enabled = true; visual.enabled = true; }
        }
        private void OnDestroy() { if (sprite != null) Destroy(sprite); }
    }
}
