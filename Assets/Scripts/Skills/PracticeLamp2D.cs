using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed class PracticeLamp2D : MonoBehaviour
    {
        public static readonly List<PracticeLamp2D> All = new List<PracticeLamp2D>();
        public Bounds Bounds => SkillGeometry.Area(transform.position, SkillGeometry.LampRadius);
        private bool spent;
        private Sprite sprite;
        private SpriteRenderer visual;
        private void Awake()
        {
            sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2);
            visual = gameObject.AddComponent<SpriteRenderer>(); visual.sprite = sprite; visual.sortingOrder = 5;
            transform.localScale = Vector3.one * SkillGeometry.LampRadius * 2; ResetAttempt();
        }
        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }
        public bool Trigger()
        { if (spent) return false; spent = true; visual.color = Color.gray; return true; }
        public void ResetAttempt() { spent = false; visual.color = Color.cyan; }
        private void OnDestroy() { if (sprite != null) Destroy(sprite); }
    }
}
