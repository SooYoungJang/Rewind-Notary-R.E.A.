using UnityEngine;

namespace RewindNotary
{
    internal sealed class StarterSlashVisual : MonoBehaviour
    {
        private static Sprite[] sprites;
        private SpriteRenderer view;
        private float age;
        private Vector3 size;

        internal static SpriteRenderer Create(int index, Vector3 position, Vector2 dimensions, int facing)
        {
            if (sprites == null)
            {
                var texture = Resources.Load<Texture2D>("GameArt/sprites/rea-starter-effects");
                if (texture == null) return null;
                int width = texture.width / 2, height = texture.height / 2;
                sprites = new Sprite[4];
                for (int i = 0; i < sprites.Length; i++)
                {
                    var cell = new Rect(i % 2 * width, (1 - i / 2) * height, width, height);
                    sprites[i] = Sprite.Create(texture, cell, Vector2.one * .5f, width, 0, SpriteMeshType.FullRect);
                    sprites[i].name = texture.name + "_" + i;
                }
            }
            var go = new GameObject("Rea sword effect");
            if (SkillStroke.WorldRoot != null) go.transform.SetParent(SkillStroke.WorldRoot, false);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprites[index];
            renderer.sortingOrder = 15;
            renderer.flipX = facing < 0;
            var nativeSize = renderer.sprite.bounds.size;
            go.transform.localScale = new Vector3(dimensions.x / nativeSize.x, dimensions.y / nativeSize.y, 1f);
            return renderer;
        }

        internal static void Burst(int index, Vector3 position, Vector2 dimensions, int facing)
        {
            var renderer = Create(index, position, dimensions * 1.28f, facing);
            if (renderer == null)
            {
                Vector3 reach = index == QuickSkills.Rising ? Vector3.up * dimensions.y : Vector3.right * dimensions.x;
                SkillStroke.Show(position - reach * .5f, position + reach * .5f,
                    index == QuickSkills.Thrust || index == QuickSkills.Sweep ? new Color(1f, .65f, .25f) : new Color(.48f, .9f, 1f), .18f);
                return;
            }
            var burst = renderer.gameObject.AddComponent<StarterSlashVisual>();
            burst.view = renderer;
            burst.size = renderer.transform.localScale;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / .23f);
            transform.localScale = size * Mathf.Lerp(.86f, 1.14f, t);
            view.color = new Color(1f, 1f, 1f, 1f - t * t);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
