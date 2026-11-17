using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    [DefaultExecutionOrder(1000)]
    public sealed class ParallaxBackground : MonoBehaviour
    {
        private sealed class Layer
        {
            public float factor, width, y;
            public Transform[] tiles;
            public SpriteRenderer[] renderers;
        }
        private readonly List<Layer> layers = new List<Layer>();
        private readonly List<Sprite> owned = new List<Sprite>();
        private Camera target;
        public void Initialize(Camera camera)
        {
            target = camera;
            Add("GameArt/backgrounds/stage1-far", 0.12f, 22f, -3f, -30);
            Add("GameArt/backgrounds/stage1-mid", 0.45f, 7.5f, -0.8f, -20);
            LateUpdate();
        }
        private void Add(string resource, float factor, float height, float bottom, int order)
        {
            var texture = Resources.Load<Texture2D>(resource);
            if (texture == null) throw new System.InvalidOperationException("Missing background: " + resource);
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0),
                texture.height / height, 0, SpriteMeshType.FullRect);
            owned.Add(sprite);
            var layer = new Layer { factor = factor, width = sprite.bounds.size.x, y = bottom,
                tiles = new Transform[5], renderers = new SpriteRenderer[5] };
            for (int i = 0; i < 5; i++)
            {
                var tile = new GameObject(resource + " tile " + i);
                tile.transform.SetParent(transform, false);
                layer.tiles[i] = tile.transform;
                layer.renderers[i] = tile.AddComponent<SpriteRenderer>();
                layer.renderers[i].sprite = sprite; layer.renderers[i].sortingOrder = order;
            }
            layers.Add(layer);
        }
        private void LateUpdate()
        {
            if (target == null) return;
            foreach (Layer layer in layers)
            {
                float drift = target.transform.position.x * (1f - layer.factor);
                int center = Mathf.FloorToInt((target.transform.position.x - drift) / layer.width);
                for (int i = 0; i < layer.tiles.Length; i++)
                {
                    int tileIndex = center + i - 2;
                    layer.tiles[i].position = new Vector3(drift + tileIndex * layer.width, layer.y, 5f);
                    // Global tile parity stays stable when the strip is recycled.
                    layer.renderers[i].flipX = (tileIndex & 1) != 0;
                }
            }
        }
        private void OnDestroy() { foreach (Sprite sprite in owned) if (sprite != null) Destroy(sprite); }
    }
}
