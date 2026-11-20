using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed class RoomArchitectureDecorator : MonoBehaviour
    {
        private Camera targetCamera;
        private RepresentativeArea area;
        private Transform worldRoot;
        private string explicitDistrict;
        private string decoratedRoomKey;
        private readonly HashSet<int> decoratedColliders = new HashSet<int>();
        private Transform fgRoot;
        private readonly List<Transform> fgElements = new List<Transform>();
        private float fgSpan = 42f;

        private Texture2D downGradTex;
        private Texture2D softSilhouetteTex;
        private Texture2D glowTex;
        private Sprite whiteSprite;
        private Sprite downGradSprite;
        private Sprite silhouetteSprite;
        private Sprite glowSprite;
        private Material additiveMat;

        private struct DistrictPalette
        {
            public Color Rim;
            public Color Cap;
            public Color Bracket;
            public Color WallGrip;
            public Color FgSilhouette;
        }

        public void Initialize(Camera cam, RepresentativeArea storyArea, Transform root, string forcedDistrict = null)
        {
            targetCamera = cam;
            area = storyArea;
            worldRoot = root;
            explicitDistrict = forcedDistrict;
            BuildTextures();
            BuildForegroundLayer(CurrentDistrict);
            DecorateAllGrounds(true);
        }

        private string CurrentDistrict => !string.IsNullOrEmpty(explicitDistrict)
            ? explicitDistrict
            : (area?.Room?.stage ?? "hub");

        private void BuildTextures()
        {
            whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2f);

            downGradTex = new Texture2D(4, 32, TextureFormat.RGBA32, false);
            downGradTex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 32; y++)
            {
                float t = y / 31f;
                float a = Mathf.Pow(t, 1.8f) * 0.68f;
                var c = new Color(0.02f, 0.025f, 0.045f, a);
                for (int x = 0; x < 4; x++) downGradTex.SetPixel(x, y, c);
            }
            downGradTex.Apply();
            downGradSprite = Sprite.Create(downGradTex, new Rect(0, 0, 4, 32), new Vector2(0.5f, 1f), 32f);

            softSilhouetteTex = new Texture2D(32, 64, TextureFormat.RGBA32, false);
            softSilhouetteTex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 64; y++)
            {
                float vy = y / 63f;
                for (int x = 0; x < 32; x++)
                {
                    float vx = Mathf.Abs((x / 31f) * 2f - 1f);
                    float edge = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((vx - 0.55f) / 0.45f));
                    float vFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(vy * 2.6f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - vy) * 2.6f));
                    softSilhouetteTex.SetPixel(x, y, new Color(1f, 1f, 1f, edge * (0.45f + 0.55f * vFade)));
                }
            }
            softSilhouetteTex.Apply();
            silhouetteSprite = Sprite.Create(softSilhouetteTex, new Rect(0, 0, 32, 64), Vector2.one * 0.5f, 32f);

            glowTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            glowTex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dx = (x - 31.5f) / 31.5f;
                    float dy = (y - 31.5f) / 31.5f;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), 2.4f);
                    glowTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            glowTex.Apply();
            glowSprite = Sprite.Create(glowTex, new Rect(0, 0, 64, 64), Vector2.one * 0.5f, 64f);

            var sh = Resources.Load<Shader>("Shaders/AdditiveSprite") ?? Shader.Find("Sprites/Default");
            additiveMat = new Material(sh);
        }

        private static DistrictPalette PaletteFor(string district)
        {
            switch (district)
            {
                case "velvet":
                    return new DistrictPalette
                    {
                        Rim = new Color(0.95f, 0.72f, 0.44f, 0.82f),
                        Cap = new Color(0.56f, 0.28f, 0.24f, 0.96f),
                        Bracket = new Color(0.24f, 0.11f, 0.13f, 0.88f),
                        WallGrip = new Color(1.0f, 0.78f, 0.42f, 0.78f),
                        FgSilhouette = new Color(0.11f, 0.04f, 0.06f, 0.34f)
                    };
                case "clock":
                    return new DistrictPalette
                    {
                        Rim = new Color(1.0f, 0.86f, 0.50f, 0.86f),
                        Cap = new Color(0.68f, 0.54f, 0.28f, 0.96f),
                        Bracket = new Color(0.24f, 0.19f, 0.10f, 0.88f),
                        WallGrip = new Color(1.0f, 0.84f, 0.44f, 0.82f),
                        FgSilhouette = new Color(0.10f, 0.08f, 0.04f, 0.30f)
                    };
                case "opera":
                    return new DistrictPalette
                    {
                        Rim = new Color(1.0f, 0.88f, 0.62f, 0.88f),
                        Cap = new Color(0.54f, 0.24f, 0.28f, 0.96f),
                        Bracket = new Color(0.18f, 0.10f, 0.14f, 0.86f),
                        WallGrip = new Color(1.0f, 0.82f, 0.52f, 0.80f),
                        FgSilhouette = new Color(0.07f, 0.05f, 0.12f, 0.32f)
                    };
                case "bell":
                    return new DistrictPalette
                    {
                        Rim = new Color(0.68f, 0.92f, 0.96f, 0.80f),
                        Cap = new Color(0.36f, 0.42f, 0.44f, 0.96f),
                        Bracket = new Color(0.10f, 0.16f, 0.20f, 0.86f),
                        WallGrip = new Color(0.56f, 0.92f, 0.96f, 0.80f),
                        FgSilhouette = new Color(0.02f, 0.07f, 0.11f, 0.36f)
                    };
                case "gallery":
                    return new DistrictPalette
                    {
                        Rim = new Color(0.96f, 0.84f, 0.56f, 0.90f),
                        Cap = new Color(0.76f, 0.64f, 0.42f, 0.96f),
                        Bracket = new Color(0.34f, 0.32f, 0.30f, 0.78f),
                        WallGrip = new Color(0.94f, 0.76f, 0.40f, 0.85f),
                        FgSilhouette = new Color(0.14f, 0.13f, 0.16f, 0.20f)
                    };
                case "cassel":
                    return new DistrictPalette
                    {
                        Rim = new Color(0.96f, 0.88f, 0.62f, 0.92f),
                        Cap = new Color(0.66f, 0.64f, 0.58f, 0.96f),
                        Bracket = new Color(0.14f, 0.17f, 0.24f, 0.90f),
                        WallGrip = new Color(0.88f, 0.92f, 1.0f, 0.85f),
                        FgSilhouette = new Color(0.04f, 0.06f, 0.12f, 0.38f)
                    };
                default:
                    return new DistrictPalette
                    {
                        Rim = new Color(0.94f, 0.80f, 0.56f, 0.78f),
                        Cap = new Color(0.46f, 0.34f, 0.22f, 0.95f),
                        Bracket = new Color(0.18f, 0.12f, 0.08f, 0.84f),
                        WallGrip = new Color(0.98f, 0.80f, 0.46f, 0.78f),
                        FgSilhouette = new Color(0.09f, 0.06f, 0.04f, 0.28f)
                    };
            }
        }

        private void BuildForegroundLayer(string district)
        {
            if (fgRoot != null) Destroy(fgRoot.gameObject);
            fgElements.Clear();
            fgRoot = new GameObject("ForegroundParallax").transform;
            fgRoot.SetParent(transform, false);

            var pal = PaletteFor(district);
            int count = 6;
            for (int i = 0; i < count; i++)
            {
                var col = new GameObject("FgSilhouette_" + i);
                col.transform.SetParent(fgRoot, false);
                var sr = col.AddComponent<SpriteRenderer>();
                sr.sprite = silhouetteSprite;
                sr.color = pal.FgSilhouette;
                sr.sortingOrder = 22;
                bool topBeam = (i % 2) == 1;
                float w = topBeam ? 2.4f : 1.5f;
                float h = topBeam ? 3.8f : 4.2f;
                col.transform.localScale = new Vector3(w, h, 1f);
                col.transform.localPosition = new Vector3((i - count * 0.5f) * (fgSpan / count), topBeam ? 5.2f : -5.4f, 0f);
                fgElements.Add(col.transform);
            }
        }

        private void LateUpdate()
        {
            string roomKey = area?.Room?.id ?? explicitDistrict ?? "static";
            if (roomKey != decoratedRoomKey)
            {
                decoratedRoomKey = roomKey;
                decoratedColliders.Clear();
                BuildForegroundLayer(CurrentDistrict);
                DecorateAllGrounds(false);
            }
            else if (Time.frameCount % 20 == 0)
            {
                DecorateAllGrounds(false);
            }

            UpdateForegroundParallax();
        }

        private void UpdateForegroundParallax()
        {
            if (targetCamera == null || fgRoot == null || fgElements.Count == 0) return;
            Vector3 cam = targetCamera.transform.position;
            float shift = -cam.x * 0.22f;
            float spacing = fgSpan / fgElements.Count;
            for (int i = 0; i < fgElements.Count; i++)
            {
                var t = fgElements[i];
                if (t == null) continue;
                float rawX = i * spacing + shift;
                float wrapped = Mathf.Repeat(rawX - cam.x + fgSpan * 0.5f, fgSpan) - fgSpan * 0.5f;
                bool topBeam = (i % 2) == 1;
                t.position = new Vector3(cam.x + wrapped, cam.y + (topBeam ? 5.65f : -5.85f), 0f);
            }
        }

        private void DecorateAllGrounds(bool initial)
        {
            if (worldRoot == null) return;
            var pal = PaletteFor(CurrentDistrict);
            var boxes = worldRoot.GetComponentsInChildren<BoxCollider2D>(false);
            for (int i = 0; i < boxes.Length; i++)
            {
                var box = boxes[i];
                if (box == null || box.gameObject.layer != LevelBuilder.GroundLayer) continue;
                int id = box.GetInstanceID();
                if (decoratedColliders.Contains(id)) continue;
                decoratedColliders.Add(id);
                DecorateBox(box, pal);
            }
        }

        private void DecorateBox(BoxCollider2D box, DistrictPalette pal)
        {
            string n = box.gameObject.name;
            if (n.Contains("west_wall") || n.Contains("east_wall") || n.Contains("경계") || n.Contains("Boundary"))
                return;

            Vector3 lossy = box.transform.lossyScale;
            float w = Mathf.Abs(box.size.x * lossy.x);
            float h = Mathf.Abs(box.size.y * lossy.y);
            if (w < 0.2f || h < 0.08f) return;

            var authored = box.GetComponent<AuthoredSurface>();
            bool isKickWall = authored != null && authored.Kickable && h > w * 1.4f;

            var decoRoot = new GameObject("ArchTrim");
            decoRoot.transform.SetParent(box.transform, false);
            decoRoot.transform.localPosition = new Vector3(box.offset.x, box.offset.y, 0f);
            float invX = Mathf.Abs(lossy.x) > 0.0001f ? 1f / lossy.x : 1f;
            float invY = Mathf.Abs(lossy.y) > 0.0001f ? 1f / lossy.y : 1f;
            decoRoot.transform.localScale = new Vector3(invX, invY, 1f);

            if (isKickWall)
            {
                AddRect(decoRoot.transform, "WallGripLeft", whiteSprite, new Color(pal.WallGrip.r, pal.WallGrip.g, pal.WallGrip.b, 0.55f), new Vector3(-w * 0.5f + 0.025f, 0f, 0f), new Vector3(0.04f, h * 0.96f, 1f), 2);
                AddRect(decoRoot.transform, "WallGripRight", whiteSprite, new Color(pal.WallGrip.r, pal.WallGrip.g, pal.WallGrip.b, 0.55f), new Vector3(w * 0.5f - 0.025f, 0f, 0f), new Vector3(0.04f, h * 0.96f, 1f), 2);
                int rungs = Mathf.Clamp(Mathf.FloorToInt(h / 1.3f), 2, 10);
                for (int r = 0; r < rungs; r++)
                {
                    float ry = Mathf.Lerp(-h * 0.42f, h * 0.42f, rungs == 1 ? 0.5f : (float)r / (rungs - 1));
                    AddRect(decoRoot.transform, "Rung_" + r, whiteSprite, new Color(pal.Rim.r, pal.Rim.g, pal.Rim.b, 0.50f), new Vector3(0f, ry, 0f), new Vector3(w * 0.96f, 0.035f, 1f), 2);
                }
                return;
            }

            if (w < 0.8f || h > 2.6f) return;

            AddRect(decoRoot.transform, "TopRim", whiteSprite, new Color(pal.Rim.r, pal.Rim.g, pal.Rim.b, 0.68f), new Vector3(0f, h * 0.5f - 0.02f, 0f), new Vector3(w, 0.04f, 1f), 2);
            AddRect(decoRoot.transform, "TopBevelDark", whiteSprite, new Color(pal.Bracket.r, pal.Bracket.g, pal.Bracket.b, 0.65f), new Vector3(0f, h * 0.5f - 0.065f, 0f), new Vector3(w, 0.035f, 1f), 2);

            if (h <= 1.25f)
            {
                AddRect(decoRoot.transform, "UnderShadow", downGradSprite, Color.white, new Vector3(0f, -h * 0.5f, 0f), new Vector3(w * 0.99f, 0.42f, 1f), -1);

                float capW = Mathf.Min(0.08f, w * 0.10f);
                AddRect(decoRoot.transform, "CapLeft", whiteSprite, new Color(pal.Bracket.r, pal.Bracket.g, pal.Bracket.b, 0.85f), new Vector3(-w * 0.5f + capW * 0.5f, 0f, 0f), new Vector3(capW, h, 1f), 2);
                AddRect(decoRoot.transform, "CapRight", whiteSprite, new Color(pal.Bracket.r, pal.Bracket.g, pal.Bracket.b, 0.85f), new Vector3(w * 0.5f - capW * 0.5f, 0f, 0f), new Vector3(capW, h, 1f), 2);

                if (w >= 3.2f && box.attachedRigidbody == null)
                {
                    int brackets = Mathf.Clamp(Mathf.FloorToInt(w / 4.8f) + 1, 2, 5);
                    for (int b = 0; b < brackets; b++)
                    {
                        float u = brackets == 1 ? 0.5f : (float)b / (brackets - 1);
                        float bx = Mathf.Lerp(-w * 0.42f, w * 0.42f, u);
                        AddRect(decoRoot.transform, "Corbel_" + b, downGradSprite, new Color(pal.Bracket.r, pal.Bracket.g, pal.Bracket.b, 0.65f), new Vector3(bx, -h * 0.5f, 0f), new Vector3(0.28f, 0.72f, 1f), -1);
                    }
                }
            }
        }

        private static SpriteRenderer AddRect(Transform parent, string label, Sprite sprite, Color color, Vector3 pos, Vector3 scale, int order)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        private void OnDestroy()
        {
            if (whiteSprite != null) Destroy(whiteSprite);
            if (downGradSprite != null) Destroy(downGradSprite);
            if (silhouetteSprite != null) Destroy(silhouetteSprite);
            if (glowSprite != null) Destroy(glowSprite);
            if (downGradTex != null) Destroy(downGradTex);
            if (softSilhouetteTex != null) Destroy(softSilhouetteTex);
            if (glowTex != null) Destroy(glowTex);
            if (additiveMat != null) Destroy(additiveMat);
        }
    }
}
