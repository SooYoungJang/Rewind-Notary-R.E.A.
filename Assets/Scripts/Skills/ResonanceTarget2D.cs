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
        private SpriteRenderer visual;
        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }
        public void InitializeMechanism()
        {
            mechanism = true; Guarded = true; gameObject.layer = LevelBuilder.GroundLayer;
            var box = gameObject.AddComponent<BoxCollider2D>(); box.size = new Vector2(SkillGeometry.GuardWidth, SkillGeometry.GuardHeight);
            visual = gameObject.AddComponent<SpriteRenderer>(); visual.sprite = RoomArt.Station("bell"); visual.sortingOrder = 4;
            visual.drawMode = SpriteDrawMode.Sliced; visual.size = box.size; visual.color = Color.yellow;
        }
        internal const float SealHeight = 2.8f, SealWidth = 1.4f;
        internal void InitializeBell(Sprite picture, bool seal)
        {
            mechanism = Guarded = true;
            gameObject.layer = seal ? LevelBuilder.GroundLayer : 0;
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.autoTiling = false;
            box.isTrigger = !seal;
            float height = seal ? SealHeight : SkillGeometry.GuardHeight;
            box.size = new Vector2(seal ? SealWidth : SkillGeometry.GuardWidth, height);
            box.offset = Vector2.up * ((height - SkillGeometry.GuardHeight) / 2);
            var face = new GameObject("Striking face");
            face.transform.SetParent(transform, false);
            face.transform.localPosition = box.offset;
            face.transform.localScale = new Vector3(box.size.x / picture.bounds.size.x, height / picture.bounds.size.y, 1);
            visual = face.AddComponent<SpriteRenderer>();
            visual.sprite = picture;
            visual.sortingOrder = 4;
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
    }
}
