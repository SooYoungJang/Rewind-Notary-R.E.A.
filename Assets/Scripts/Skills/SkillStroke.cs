using UnityEngine;

namespace RewindNotary
{
    // Small code-native prototype VFX. No damage, movement or independent progression callbacks.
    public sealed class SkillStroke : MonoBehaviour
    {
        private Sprite sprite;
        private SpriteRenderer visual;
        private float lifetime, started;
        public static void Show(Vector3 from, Vector3 to, Color color, float seconds)
        {
            var go = new GameObject("Skill stroke");
            var effect = go.AddComponent<SkillStroke>();
            effect.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2);
            effect.visual = go.AddComponent<SpriteRenderer>();
            effect.visual.sprite = effect.sprite; effect.visual.color = color; effect.visual.sortingOrder = 14;
            go.transform.position = (from + to) * 0.5f;
            Vector3 delta = to - from;
            go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            go.transform.localScale = new Vector3(delta.magnitude, 0.055f, 1);
            effect.lifetime = seconds; effect.started = Time.time;
        }
        public static void Afterimage(SpriteRenderer source, float seconds)
        {
            var go = new GameObject("Sword mastery afterimage");
            var effect = go.AddComponent<SkillStroke>();
            effect.visual = go.AddComponent<SpriteRenderer>();
            effect.visual.sprite = source.sprite; effect.visual.flipX = source.flipX;
            effect.visual.color = new Color(0.35f, 0.8f, 1f, 0.45f); effect.visual.sortingOrder = 9;
            go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            go.transform.localScale = source.transform.lossyScale;
            effect.lifetime = seconds; effect.started = Time.time;
        }
        private void Update()
        {
            float t = (Time.time - started) / lifetime;
            if (t >= 1) { Destroy(gameObject); return; }
            var color = visual.color; color.a = Mathf.Min(color.a, 1 - t); visual.color = color;
        }
        private void OnDestroy() { if (sprite != null) Destroy(sprite); }
    }
}
