using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed class LevelBuilder : MonoBehaviour
    {
        public const int GroundLayer = 8;
        public static readonly Vector2 StartPoint = new Vector2(0f, 0.15f);
        public static readonly Vector2 ArenaStart = new Vector2(36f, 0.15f);
        public static readonly Vector2 BossStart = new Vector2(46f, 0.15f);
        private readonly List<Sprite> sprites = new List<Sprite>();
        private Sprite stone, white;
        public void Build()
        {
            Texture2D texture = Resources.Load<Texture2D>("GameArt/backgrounds/stage1-ground");
            if (texture == null) throw new InvalidOperationException("Missing stage1-ground");
            stone = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f,
                texture.height, 0, SpriteMeshType.FullRect);
            white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2);
            sprites.Add(stone); sprites.Add(white);
            Platform("Start ground", -8, 16, 0, 2);
            Platform("Main ground and arena", 20, 61, 0, 2);
            Platform("Step 1", 5, 8, 1.6f, 0.55f);
            Platform("Step 2", 10, 13.5f, 3.1f, 0.55f);
            Platform("Gap landing", 21, 24, 1.6f, 0.55f);
            Platform("Upper route", 26, 30, 3.1f, 0.55f);
            Platform("Arena left ledge", 39, 42, 1.7f, 0.55f);
            Platform("Arena high ledge", 47, 51, 2.0f, 0.55f);
            Platform("Arena right ledge", 55, 58, 1.4f, 0.55f);
            Wall(-8f); Wall(61f);
            Marker("Start marker", 0, new Color(0.2f, 0.85f, 0.78f));
            Marker("Arena boundary", 35, new Color(0.85f, 0.55f, 0.2f));
        }
        private void Platform(string label, float left, float right, float top, float depth)
        {
            var root = new GameObject(label);
            root.layer = GroundLayer;
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3((left + right) / 2f, top - depth / 2f, 0);
            var box = root.AddComponent<BoxCollider2D>();
            box.size = new Vector2(right - left, depth);
            // One solid collision box; visuals tile independently without physics seams.
            float naturalWidth = depth * 3f;
            int count = Mathf.CeilToInt((right - left) / naturalWidth);
            float tileWidth = (right - left) / count;
            for (int i = 0; i < count; i++)
            {
                var tile = new GameObject("Stone tile " + i);
                tile.transform.SetParent(root.transform, false);
                tile.transform.localPosition = new Vector3(-(right - left) / 2 + tileWidth * (i + 0.5f), 0, 0);
                tile.transform.localScale = new Vector3(tileWidth / stone.bounds.size.x, depth / stone.bounds.size.y, 1);
                var render = tile.AddComponent<SpriteRenderer>();
                render.sprite = stone; render.sortingOrder = 0; render.flipX = i % 2 == 1;
            }
        }
        private void Wall(float x)
        {
            var wall = new GameObject("Boundary wall");
            wall.transform.SetParent(transform, false); wall.layer = GroundLayer;
            wall.transform.position = new Vector3(x, 5, 0);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 14f);
        }
        private void Marker(string label, float x, Color color)
        {
            var mark = new GameObject(label); mark.transform.SetParent(transform, false);
            mark.transform.position = new Vector3(x, 0.04f, 0);
            mark.transform.localScale = new Vector3(1.4f, 0.08f, 1);
            var render = mark.AddComponent<SpriteRenderer>(); render.sprite = white;
            render.color = color; render.sortingOrder = 2;
        }
        private void OnDestroy() { foreach (var sprite in sprites) if (sprite != null) Destroy(sprite); }
    }
}
