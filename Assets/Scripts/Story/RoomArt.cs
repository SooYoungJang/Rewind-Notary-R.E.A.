using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    internal static class RoomArt
    {
        private static readonly Dictionary<(string, float), Sprite> sprites = new Dictionary<(string, float), Sprite>();

        private static string District(string stage)
        {
            switch (stage)
            {
                case "velvet":
                case "clock":
                case "opera":
                case "bell":
                case "gallery": return stage;
                case "hub": return "opera";
                case "janhyang":
                case "cassel": return "bell";
                default: throw new ArgumentException("Unknown room art district " + stage);
            }
        }

        internal static Sprite Enemy(string stage, string id) =>
            Picture("enemy-" + id.Replace('_', '-') + "-" + District(stage));

        internal static Sprite Door(string stage, bool open)
        {
            string theme = stage == "hub" || stage == "janhyang" || stage == "cassel" ? "hub" : District(stage);
            return Picture("door-" + theme + (open ? "-open" : "-locked"));
        }

        internal static Sprite Station(string stage) => Picture("station-" + District(stage));

        internal static Sprite Tile(string stage, bool plated) =>
            Load("GameArt/backgrounds/tile-" + District(stage), plated ? 1.6f : 1f);

        internal static Sprite Picture(string name) => Load("GameArt/sprites/" + name, 1f);
        internal static Sprite PaintBrush() => Load("GameArt/backgrounds/gallery-oil", 2.4f);

        private static Sprite Load(string path, float width)
        {
            var key = (path, width);
            if (sprites.TryGetValue(key, out var sprite)) return sprite;
            var art = Resources.Load<Texture2D>(path);
            if (art == null) throw new InvalidOperationException("Missing room art resource " + path);
            sprite = Sprite.Create(art, new Rect(0, 0, art.width, art.height), Vector2.one * .5f,
                art.width / width, 0, SpriteMeshType.FullRect);
            sprite.name = art.name;
            sprites.Add(key, sprite);
            return sprite;
        }
    }

    internal sealed class RoomDoorView
    {
        private readonly SpriteRenderer view;
        private readonly Sprite locked, open;

        internal RoomDoorView(GameObject door, string stage)
        {
            view = door.AddComponent<SpriteRenderer>();
            view.sortingOrder = 1;
            locked = RoomArt.Door(stage, false);
            open = RoomArt.Door(stage, true);
        }

        internal void Show(bool canPass)
        {
            var sprite = canPass ? open : locked;
            if (view.sprite == sprite) return;
            view.sprite = sprite;
            view.transform.localScale = Vector3.one * (2.6f / sprite.bounds.size.y);
        }
    }
}
