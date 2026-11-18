using UnityEngine;

namespace RewindNotary
{
    // Code-generated ring and radial motes. No colliders or gameplay effects.
    public sealed class SkillEvolutionBurst : MonoBehaviour
    {
        public const float Lifetime = 0.6f, FlashSeconds = 0.1f;
        private const int RingSegments = 48, MoteCount = 12;
        private readonly Transform[] pieces = new Transform[RingSegments + MoteCount];
        private readonly SpriteRenderer[] visuals = new SpriteRenderer[RingSegments + MoteCount];
        private Sprite sprite;
        private Color tint;
        private float age;
        public static SkillEvolutionBurst Show(Transform root, Vector3 position, Color color)
        {
            var go = new GameObject("Skill evolution ring"); go.transform.SetParent(root, false); go.transform.position = position;
            var effect = go.AddComponent<SkillEvolutionBurst>(); effect.tint = color;
            effect.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2);
            for (int i = 0; i < effect.pieces.Length; i++)
            {
                var part = new GameObject(i < RingSegments ? "Ring" : "Mote"); part.transform.SetParent(go.transform, false);
                effect.pieces[i] = part.transform;
                var visual = part.AddComponent<SpriteRenderer>(); visual.sprite = effect.sprite;
                visual.sortingOrder = 25; effect.visuals[i] = visual;
            }
            effect.Draw(); return effect;
        }
        private void Update()
        {
            if (!GameInput.GameplayEnabled) return;
            age += Time.unscaledDeltaTime;
            if (age >= Lifetime) { Destroy(gameObject); return; }
            Draw();
        }
        private void Draw()
        {
            float t = age / Lifetime, radius = Mathf.Lerp(0.25f, 2.3f, 1 - (1 - t) * (1 - t));
            Color color = tint; color.a = (1 - t) * 0.85f;
            for (int i = 0; i < pieces.Length; i++)
            {
                bool ring = i < RingSegments;
                float angle = (ring ? (float)i / RingSegments : (float)(i - RingSegments) / MoteCount) * Mathf.PI * 2;
                float distance = ring ? radius : radius * 1.3f;
                pieces[i].localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * distance;
                pieces[i].localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg + (ring ? 90 : 0));
                pieces[i].localScale = new Vector3(ring ? 2 * radius * Mathf.Tan(Mathf.PI / RingSegments) : 0.16f * (1 - t), 0.035f, 1);
                visuals[i].color = color;
            }
        }
        private void OnGUI()
        {
            if (!GameInput.GameplayEnabled || age >= FlashSeconds) return;
            var previous = GUI.color; int depth = GUI.depth; var matrix = GUI.matrix;
            GUI.depth = -5; GUI.matrix = Matrix4x4.identity;
            GUI.color = new Color(tint.r, tint.g, tint.b, 0.12f * (1 - age / FlashSeconds));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous; GUI.depth = depth; GUI.matrix = matrix;
        }
        private void OnDestroy() { if (sprite != null) Destroy(sprite); }
    }
}
