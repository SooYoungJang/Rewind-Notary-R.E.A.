using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    // Explicit practice breakable, reset with the world on retry; not an immortal input counter.
    public sealed class DestructibleTarget2D : MonoBehaviour
    {
        public static readonly List<DestructibleTarget2D> All = new List<DestructibleTarget2D>();
        public string TargetId => gameObject.name;
        public Bounds Bounds => new Bounds(transform.position, new Vector3(0.7f, 1.2f, 2));
        private int hp = 3;
        private Sprite sprite;
        private SpriteRenderer visual;
        private void Awake()
        {
            sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2);
            visual = gameObject.AddComponent<SpriteRenderer>(); visual.sprite = sprite;
            visual.color = new Color(0.65f, 0.45f, 0.2f); visual.sortingOrder = 5;
            transform.localScale = new Vector3(0.7f, 1.2f, 1);
        }
        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }
        public bool Hit(int damage)
        {
            if (hp <= 0 || damage <= 0) return false;
            hp = Mathf.Max(0, hp - damage); visual.enabled = hp > 0; return true;
        }
        public void ResetAttempt() { hp = 3; visual.enabled = true; }
        private void OnDestroy() { if (sprite != null) Destroy(sprite); }
    }
}
