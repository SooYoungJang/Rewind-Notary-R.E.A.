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
        private readonly Transform archway;
        private readonly SpriteRenderer portalGlow;
        private static Sprite frameSprite;
        private static Sprite glowSprite;
        private static Material glowMat;

        internal RoomDoorView(GameObject door, string stage)
        {
            view = door.AddComponent<SpriteRenderer>();
            view.sortingOrder = 1;
            locked = RoomArt.Door(stage, false);
            open = RoomArt.Door(stage, true);
            portalGlow = AttachDoorArchitecture(door.transform, stage, out archway);
        }

        internal void Show(bool canPass, bool visible = true)
        {
            if (view.gameObject.activeSelf != visible) view.gameObject.SetActive(visible);
            if (!visible) return;
            var sprite = canPass ? open : locked;
            if (portalGlow != null) portalGlow.enabled = canPass;
            if (view.sprite == sprite) return;
            view.sprite = sprite;
            float scale = 2.6f / sprite.bounds.size.y;
            view.transform.localScale = Vector3.one * scale;
            if (archway != null && scale > 0.0001f) archway.localScale = Vector3.one * (1f / scale);
        }

        private static SpriteRenderer AttachDoorArchitecture(Transform doorRoot, string stage, out Transform archwayOut)
        {
            if (frameSprite == null)
                frameSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2f);
            if (glowSprite == null)
            {
                const int s = 64;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < s; y++)
                {
                    for (int x = 0; x < s; x++)
                    {
                        float dx = (x - 31.5f) / 31.5f;
                        float dy = (y - 31.5f) / 31.5f;
                        float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), 2.2f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                tex.Apply();
                glowSprite = Sprite.Create(tex, new Rect(0, 0, s, s), Vector2.one * 0.5f, s);
            }
            if (glowMat == null)
                glowMat = new Material(Resources.Load<Shader>("Shaders/AdditiveSprite") ?? Shader.Find("Sprites/Default"));

            Color goldTrim = new Color(0.92f, 0.76f, 0.44f, 0.62f);

            var host = new GameObject("DoorArchway");
            host.transform.SetParent(doorRoot, false);
            archwayOut = host.transform;

            var shadowGo = new GameObject("DoorAmbientShadow");
            shadowGo.transform.SetParent(host.transform, false);
            shadowGo.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            shadowGo.transform.localScale = new Vector3(2.85f, 3.25f, 1f);
            var shadowSr = shadowGo.AddComponent<SpriteRenderer>();
            shadowSr.sprite = glowSprite;
            shadowSr.color = new Color(0.02f, 0.025f, 0.045f, 0.62f);
            shadowSr.sortingOrder = 0;

            var threshGo = new GameObject("ThresholdGlow");
            threshGo.transform.SetParent(host.transform, false);
            threshGo.transform.localPosition = new Vector3(0f, -1.26f, 0f);
            threshGo.transform.localScale = new Vector3(2.15f, 0.22f, 1f);
            var threshSr = threshGo.AddComponent<SpriteRenderer>();
            threshSr.sprite = glowSprite;
            threshSr.sharedMaterial = glowMat;
            threshSr.color = goldTrim;
            threshSr.sortingOrder = 2;

            var glowGo = new GameObject("PortalGlow");
            glowGo.transform.SetParent(host.transform, false);
            glowGo.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            glowGo.transform.localScale = new Vector3(2.8f, 3.1f, 1f);
            var glowSr = glowGo.AddComponent<SpriteRenderer>();
            glowSr.sprite = glowSprite;
            glowSr.sharedMaterial = glowMat;
            glowSr.color = new Color(1.0f, 0.82f, 0.46f, 0.36f);
            glowSr.sortingOrder = 2;
            glowSr.enabled = false;
            return glowSr;
        }
    }
}
