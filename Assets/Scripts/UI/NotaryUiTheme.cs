using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RewindNotary
{
    public static class NotaryUiTheme
    {
        public static readonly Color PanelBg = new Color(0.055f, 0.072f, 0.118f, 0.92f);
        public static readonly Color PanelBgSoft = new Color(0.055f, 0.072f, 0.118f, 0.80f);
        public static readonly Color CardBg = new Color(0.088f, 0.112f, 0.172f, 0.94f);
        public static readonly Color CardSelectedBg = new Color(0.145f, 0.195f, 0.275f, 0.98f);
        public static readonly Color ButtonBg = new Color(0.092f, 0.116f, 0.180f, 0.88f);
        public static readonly Color ButtonSelectedBg = new Color(0.180f, 0.215f, 0.290f, 0.96f);
        public static readonly Color Gold = new Color(0.855f, 0.706f, 0.412f, 1f);
        public static readonly Color GoldSoft = new Color(0.680f, 0.555f, 0.330f, 0.72f);
        public static readonly Color GoldGlow = new Color(0.965f, 0.840f, 0.540f, 1f);
        public static readonly Color Ivory = new Color(0.955f, 0.925f, 0.855f, 1f);
        public static readonly Color ParchmentMuted = new Color(0.740f, 0.710f, 0.640f, 1f);
        public static readonly Color TealAccent = new Color(0.320f, 0.845f, 0.765f, 1f);
        public static readonly Color HealthFill = new Color(0.240f, 0.785f, 0.705f, 1f);
        public static readonly Color HealthGhost = new Color(0.940f, 0.785f, 0.420f, 0.92f);
        public static readonly Color BossFill = new Color(0.840f, 0.345f, 0.310f, 1f);
        public static readonly Color BarTrack = new Color(0.080f, 0.098f, 0.140f, 0.95f);

        private static Sprite whiteSprite;
        private static Sprite leftScrimSprite;
        private static Sprite bottomScrimSprite;
        private static Sprite titleKeyartSprite;
        private static bool titleKeyartLoaded;
        private static readonly Dictionary<string, Sprite> Portraits = new Dictionary<string, Sprite>();
        private static readonly List<UnityEngine.Object> OwnedAssets = new List<UnityEngine.Object>();

        public static Font BodyFont => LocalizationFonts.LoadBodyFont(Loc.Language);
        public static Font TitleFont => LocalizationFonts.LoadTitleFont(Loc.Language);

        public static Sprite WhiteSprite
        {
            get
            {
                if (whiteSprite == null)
                {
                    whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2f);
                    OwnedAssets.Add(whiteSprite);
                }
                return whiteSprite;
            }
        }

        public static Sprite LeftScrimSprite
        {
            get
            {
                if (leftScrimSprite == null)
                {
                    var tex = new Texture2D(64, 2, TextureFormat.RGBA32, false);
                    tex.wrapMode = TextureWrapMode.Clamp;
                    for (int x = 0; x < 64; x++)
                    {
                        float t = x / 63f;
                        float a = Mathf.Lerp(0.88f, 0.08f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.12f) / 0.65f)));
                        var c = new Color(0.035f, 0.048f, 0.088f, a);
                        tex.SetPixel(x, 0, c);
                        tex.SetPixel(x, 1, c);
                    }
                    tex.Apply();
                    leftScrimSprite = Sprite.Create(tex, new Rect(0, 0, 64, 2), Vector2.one * 0.5f, 64f);
                    OwnedAssets.Add(tex);
                    OwnedAssets.Add(leftScrimSprite);
                }
                return leftScrimSprite;
            }
        }

        public static Sprite BottomScrimSprite
        {
            get
            {
                if (bottomScrimSprite == null)
                {
                    var tex = new Texture2D(2, 64, TextureFormat.RGBA32, false);
                    tex.wrapMode = TextureWrapMode.Clamp;
                    for (int y = 0; y < 64; y++)
                    {
                        float t = y / 63f;
                        float a = Mathf.Lerp(0.84f, 0f, Mathf.SmoothStep(0f, 1f, t));
                        var c = new Color(0.030f, 0.042f, 0.075f, a);
                        tex.SetPixel(0, y, c);
                        tex.SetPixel(1, y, c);
                    }
                    tex.Apply();
                    bottomScrimSprite = Sprite.Create(tex, new Rect(0, 0, 2, 64), Vector2.one * 0.5f, 64f);
                    OwnedAssets.Add(tex);
                    OwnedAssets.Add(bottomScrimSprite);
                }
                return bottomScrimSprite;
            }
        }

        public static Sprite LoadTitleKeyart()
        {
            if (titleKeyartLoaded) return titleKeyartSprite;
            titleKeyartLoaded = true;
            var tex = Resources.Load<Texture2D>("GameArt/ui/title-keyart")
                ?? Resources.Load<Texture2D>("GameArt/backgrounds/opera-auditorium");
            if (tex != null)
            {
                titleKeyartSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f, 100f);
                OwnedAssets.Add(titleKeyartSprite);
            }
            return titleKeyartSprite;
        }

        public static Sprite LoadPortrait(string speakerId)
        {
            if (string.IsNullOrEmpty(speakerId)) return null;
            if (Portraits.TryGetValue(speakerId, out var cached)) return cached;
            var tex = Resources.Load<Texture2D>("GameArt/portraits/portrait_" + speakerId);
            Sprite sprite = null;
            if (tex != null)
            {
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f, 100f);
                OwnedAssets.Add(sprite);
            }
            Portraits[speakerId] = sprite;
            return sprite;
        }

        public static Color SpeakerCrestColor(string speakerId)
        {
            switch (speakerId)
            {
                case "rea": return new Color(0.22f, 0.56f, 0.52f, 1f);
                case "rea_echo": return new Color(0.32f, 0.64f, 0.85f, 1f);
                case "marta": return new Color(0.68f, 0.52f, 0.34f, 1f);
                case "mikhail": return new Color(0.64f, 0.22f, 0.28f, 1f);
                case "ethan": return new Color(0.72f, 0.40f, 0.20f, 1f);
                case "theo": return new Color(0.42f, 0.54f, 0.38f, 1f);
                case "luka": return new Color(0.30f, 0.52f, 0.78f, 1f);
                case "julius": return new Color(0.74f, 0.58f, 0.24f, 1f);
                case "valeria": return new Color(0.74f, 0.28f, 0.48f, 1f);
                case "gabriel": return new Color(0.24f, 0.56f, 0.66f, 1f);
                case "irina": return new Color(0.56f, 0.62f, 0.72f, 1f);
                case "cassel": return new Color(0.58f, 0.64f, 0.78f, 1f);
                default: return new Color(0.35f, 0.42f, 0.55f, 1f);
            }
        }

        public static void EnsureEventSystem(Transform parent)
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("UI EventSystem");
            if (parent != null) es.transform.SetParent(parent, false);
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static Image CreateImage(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var rt = CreateRect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite ?? WhiteSprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Image CreateBorderedPanel(Transform parent, string name, Color fill, Color border, float borderThickness = 2f)
        {
            var outer = CreateImage(parent, name, border);
            var inner = CreateImage(outer.transform, "Inner", fill);
            Stretch(inner.rectTransform, borderThickness, borderThickness, borderThickness, borderThickness);
            return outer;
        }

        public static Text CreateText(Transform parent, string name, string content, int fontSize, Color color,
            TextAnchor align = TextAnchor.MiddleLeft, bool title = false, bool shadow = true)
        {
            var rt = CreateRect(parent, name);
            var txt = rt.gameObject.AddComponent<Text>();
            txt.font = title ? TitleFont : BodyFont;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = align;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;
            txt.supportRichText = true;
            txt.raycastTarget = false;
            txt.text = content ?? "";
            if (shadow)
            {
                var sh = rt.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.72f);
                sh.effectDistance = new Vector2(1.2f, -1.2f);
            }
            return txt;
        }

        public static Button CreateStyledButton(Transform parent, string name, string label, int fontSize, bool selected, Action onClick, out Text labelText, out Image bgImage, out Image accentBar)
        {
            var outer = CreateImage(parent, name, selected ? Gold : GoldSoft);
            outer.raycastTarget = true;

            var inner = CreateImage(outer.transform, "Fill", selected ? ButtonSelectedBg : ButtonBg);
            Stretch(inner.rectTransform, 1.5f, 1.5f, 1.5f, 1.5f);
            bgImage = inner;

            accentBar = CreateImage(inner.transform, "Accent", selected ? GoldGlow : new Color(Gold.r, Gold.g, Gold.b, 0.15f));
            accentBar.rectTransform.anchorMin = new Vector2(0f, 0f);
            accentBar.rectTransform.anchorMax = new Vector2(0f, 1f);
            accentBar.rectTransform.pivot = new Vector2(0f, 0.5f);
            accentBar.rectTransform.anchoredPosition = Vector2.zero;
            accentBar.rectTransform.sizeDelta = new Vector2(selected ? 5f : 2f, 0f);

            labelText = CreateText(inner.transform, "Label", (selected ? "◆  " : "    ") + label, fontSize,
                selected ? GoldGlow : Ivory, TextAnchor.MiddleLeft, false, true);
            Stretch(labelText.rectTransform, 18f, 4f, 14f, 4f);
            labelText.resizeTextForBestFit = true;
            labelText.resizeTextMinSize = 11;
            labelText.resizeTextMaxSize = fontSize;

            var button = outer.gameObject.AddComponent<Button>();
            button.targetGraphic = inner;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.10f, 1.05f, 1f);
            colors.pressedColor = new Color(0.90f, 0.88f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }
    }
}
