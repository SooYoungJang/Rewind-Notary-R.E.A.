using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    [DefaultExecutionOrder(1000)]
    public sealed class ParallaxBackground : MonoBehaviour
    {
        private sealed class Layer
        {
            public float factor, width, y, vertical, offset;
            public bool fixedInWorld;
            public Transform[] tiles;
            public SpriteRenderer[] renderers;
        }
        private readonly List<Layer> layers = new List<Layer>();
        private readonly List<Sprite> owned = new List<Sprite>();
        [System.Serializable] private sealed class Library { public Set[] sets; }
        [System.Serializable] private sealed class Set { public string id; public Strip[] layers; }
        [System.Serializable] private sealed class Strip
        {
            public string resource, tint, foundationTint;
            public StoryRect crop;
            public float factor, height, bottom, vertical, spacing, offset;
            public bool fixedInWorld;
        }
        private RepresentativeArea area;
        private Library library;
        private string current;
        private Camera target;
        public void Initialize(Camera camera, RepresentativeArea story = null)
        {
            target = camera;
            area = story;
            LoadRoom(area?.Room.backgroundSet);
            LateUpdate();
        }

        private void LoadRoom(string id)
        {
            current = id;
            foreach (var layer in layers)
                foreach (var tile in layer.tiles) { tile.gameObject.SetActive(false); Destroy(tile.gameObject); }
            layers.Clear();
            foreach (var sprite in owned) Destroy(sprite);
            owned.Clear();
            if (!string.IsNullOrEmpty(id))
            {
                if (library == null) library = JsonUtility.FromJson<Library>(
                    Resources.Load<TextAsset>("GameArt/backgrounds/room-sets").text);
                var set = System.Array.Find(library.sets, s => s.id == id);
                if (set == null) throw new System.InvalidOperationException("Unknown background set: " + id);
                for (int i = 0; i < set.layers.Length; i++)
                {
                    var strip = set.layers[i];
                    Add("GameArt/backgrounds/" + strip.resource, strip.factor, strip.height, strip.bottom, -30 + i * 5, strip);
                }
                return;
            }
            Add("GameArt/backgrounds/stage1-far", 0.12f, 22f, -3f, -30);
            Add("GameArt/backgrounds/stage1-mid", 0.45f, 7.5f, -0.8f, -20);
        }
        private void Add(string resource, float factor, float height, float bottom, int order, Strip strip = null)
        {
            var texture = Resources.Load<Texture2D>(resource);
            if (texture == null) throw new System.InvalidOperationException("Missing background: " + resource);
            var rect = new Rect(0, 0, texture.width, texture.height);
            var tint = Color.white;
            if (strip != null)
            {
                var uv = strip.crop;
                rect = Rect.MinMaxRect(Mathf.Round(uv.x * texture.width), Mathf.Round(uv.y * texture.height),
                    Mathf.Round((uv.x + uv.width) * texture.width), Mathf.Round((uv.y + uv.height) * texture.height));
                ColorUtility.TryParseHtmlString(strip.tint, out tint);
            }
            var sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0),
                rect.height / height, 0, SpriteMeshType.FullRect);
            owned.Add(sprite);
            bool fixedInWorld = strip?.fixedInWorld == true;
            int tileCount = fixedInWorld ? 1 : 5;
            var layer = new Layer { factor = factor, width = sprite.bounds.size.x * (strip?.spacing ?? 1), y = bottom,
                vertical = strip?.vertical ?? 0, offset = strip?.offset ?? 0,
                fixedInWorld = fixedInWorld, tiles = new Transform[tileCount], renderers = new SpriteRenderer[tileCount] };
            for (int i = 0; i < tileCount; i++)
            {
                var tile = new GameObject(resource + " tile " + i);
                tile.transform.SetParent(transform, false);
                layer.tiles[i] = tile.transform;
                layer.renderers[i] = tile.AddComponent<SpriteRenderer>();
                layer.renderers[i].color = tint;
                layer.renderers[i].sprite = sprite; layer.renderers[i].sortingOrder = order;
                if (fixedInWorld)
                {
                    tile.transform.position = new Vector3(layer.offset, bottom, 5f);
                    if (ColorUtility.TryParseHtmlString(strip.foundationTint, out var foundation))
                        AddFoundation(tile.transform, sprite.bounds.size.x, order - 1, foundation);
                }
            }
            layers.Add(layer);
        }

        private void AddFoundation(Transform strip, float width, int order, Color tint)
        {
            var baseboard = new GameObject("Foundation beneath panorama").AddComponent<SpriteRenderer>();
            baseboard.transform.SetParent(strip, false);
            baseboard.transform.localPosition = new Vector3(0f, -4f, 0f);
            baseboard.transform.localScale = new Vector3(width, 8.02f, 1f);
            var sheet = Texture2D.whiteTexture;
            var sprite = Sprite.Create(sheet, new Rect(0f, 0f, sheet.width, sheet.height), Vector2.one * .5f, sheet.width);
            owned.Add(sprite);
            baseboard.sprite = sprite;
            baseboard.sortingOrder = order;
            baseboard.color = tint;
        }
        private void LateUpdate()
        {
            if (target == null) return;
            if (area?.Room.backgroundSet != current) LoadRoom(area?.Room.backgroundSet);
            foreach (Layer layer in layers)
            {
                if (layer.fixedInWorld) continue;
                float drift = target.transform.position.x * (1f - layer.factor) + layer.offset;
                int center = Mathf.FloorToInt((target.transform.position.x - drift) / layer.width);
                for (int i = 0; i < layer.tiles.Length; i++)
                {
                    int tileIndex = center + i - 2;
                    layer.tiles[i].position = new Vector3(drift + tileIndex * layer.width, layer.y + target.transform.position.y * layer.vertical, 5f);
                    // Global tile parity stays stable when the strip is recycled.
                    layer.renderers[i].flipX = (tileIndex & 1) != 0;
                }
            }
        }
        private void OnDestroy() { foreach (Sprite sprite in owned) if (sprite != null) Destroy(sprite); }
    }
}
