using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RewindNotary
{
    [DefaultExecutionOrder(-250)]
    public sealed class DistrictMinimapCanvas : MonoBehaviour
    {
        public static int ConsumedCancelFrame { get; private set; } = -1;
        private Canvas canvas;
        private GameObject miniRoot;
        private RectTransform miniGraphRoot;
        private Text miniTitleText;
        private Text miniHintText;

        private GameObject fullRoot;
        private RectTransform fullGraphRoot;
        private Text fullTitleText;
        private Text fullProgressText;
        private Text fullLegendText;

        private RepresentativeScript script;
        private readonly HashSet<string> visited = new HashSet<string>();
        private string activeStage;
        private string activeRoomId;
        private bool fullOpen;
        private bool dirty = true;
        private string lastLang;

        public bool FullMapOpen => fullOpen;
        public int VisitedCount => visited.Count;

        public void Initialize(Transform parent)
        {
            canvas = NotaryUiTheme.CreateCanvas(parent, "District Minimap Canvas", 95);
            BuildCompactWidget();
            BuildFullAtlas();
            lastLang = Loc.Language;
        }

        private void BuildCompactWidget()
        {
            var panel = NotaryUiTheme.CreateBorderedPanel(canvas.transform, "CompactMinimap", NotaryUiTheme.PanelBgSoft, NotaryUiTheme.GoldSoft, 1.2f);
            miniRoot = panel.gameObject;
            var rt = panel.rectTransform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -146f);
            rt.sizeDelta = new Vector2(216f, 114f);

            miniTitleText = NotaryUiTheme.CreateText(panel.transform, "MiniTitle", "", 11, NotaryUiTheme.GoldGlow, TextAnchor.MiddleLeft, true, false);
            miniTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            miniTitleText.rectTransform.anchorMax = new Vector2(0.72f, 1f);
            miniTitleText.rectTransform.offsetMin = new Vector2(8f, -22f);
            miniTitleText.rectTransform.offsetMax = new Vector2(0f, -3f);

            miniHintText = NotaryUiTheme.CreateText(panel.transform, "MiniHint", "", 10, NotaryUiTheme.ParchmentMuted, TextAnchor.MiddleRight, false, false);
            miniHintText.rectTransform.anchorMin = new Vector2(0.55f, 1f);
            miniHintText.rectTransform.anchorMax = new Vector2(1f, 1f);
            miniHintText.rectTransform.offsetMin = Vector2.zero;
            miniHintText.rectTransform.offsetMax = new Vector2(-8f, -3f);

            miniGraphRoot = NotaryUiTheme.CreateRect(panel.transform, "MiniGraph");
            miniGraphRoot.anchorMin = new Vector2(0f, 0f);
            miniGraphRoot.anchorMax = new Vector2(1f, 1f);
            miniGraphRoot.offsetMin = new Vector2(10f, 8f);
            miniGraphRoot.offsetMax = new Vector2(-10f, -24f);

            miniRoot.SetActive(false);
        }

        private void BuildFullAtlas()
        {
            fullRoot = new GameObject("FullDistrictAtlas", typeof(RectTransform));
            fullRoot.transform.SetParent(canvas.transform, false);
            NotaryUiTheme.Stretch((RectTransform)fullRoot.transform);

            var scrim = NotaryUiTheme.CreateImage(fullRoot.transform, "Scrim", new Color(0.024f, 0.034f, 0.064f, 0.86f));
            NotaryUiTheme.Stretch(scrim.rectTransform);

            var frame = NotaryUiTheme.CreateBorderedPanel(fullRoot.transform, "AtlasPanel", NotaryUiTheme.PanelBg, NotaryUiTheme.Gold, 2f);
            frame.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            frame.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            frame.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            frame.rectTransform.anchoredPosition = Vector2.zero;
            frame.rectTransform.sizeDelta = new Vector2(1060f, 590f);

            fullTitleText = NotaryUiTheme.CreateText(frame.transform, "AtlasTitle", "", 26, NotaryUiTheme.GoldGlow, TextAnchor.MiddleLeft, true, true);
            fullTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            fullTitleText.rectTransform.anchorMax = new Vector2(0.68f, 1f);
            fullTitleText.rectTransform.offsetMin = new Vector2(32f, -64f);
            fullTitleText.rectTransform.offsetMax = new Vector2(0f, -14f);

            fullProgressText = NotaryUiTheme.CreateText(frame.transform, "AtlasProgress", "", 16, NotaryUiTheme.TealAccent, TextAnchor.MiddleRight, false, true);
            fullProgressText.rectTransform.anchorMin = new Vector2(0.62f, 1f);
            fullProgressText.rectTransform.anchorMax = new Vector2(1f, 1f);
            fullProgressText.rectTransform.offsetMin = Vector2.zero;
            fullProgressText.rectTransform.offsetMax = new Vector2(-32f, -14f);

            var rule = NotaryUiTheme.CreateImage(frame.transform, "Rule", NotaryUiTheme.GoldSoft);
            rule.rectTransform.anchorMin = new Vector2(0.03f, 1f);
            rule.rectTransform.anchorMax = new Vector2(0.97f, 1f);
            rule.rectTransform.anchoredPosition = new Vector2(0f, -68f);
            rule.rectTransform.sizeDelta = new Vector2(0f, 1.5f);

            fullGraphRoot = NotaryUiTheme.CreateRect(frame.transform, "FullGraph");
            fullGraphRoot.anchorMin = new Vector2(0f, 0f);
            fullGraphRoot.anchorMax = new Vector2(1f, 1f);
            fullGraphRoot.offsetMin = new Vector2(36f, 54f);
            fullGraphRoot.offsetMax = new Vector2(-36f, -84f);

            fullLegendText = NotaryUiTheme.CreateText(frame.transform, "AtlasLegend", "", 13, NotaryUiTheme.ParchmentMuted, TextAnchor.MiddleCenter, false, false);
            fullLegendText.rectTransform.anchorMin = new Vector2(0f, 0f);
            fullLegendText.rectTransform.anchorMax = new Vector2(1f, 0f);
            fullLegendText.rectTransform.offsetMin = new Vector2(24f, 12f);
            fullLegendText.rectTransform.offsetMax = new Vector2(-24f, 44f);

            fullRoot.SetActive(false);
        }

        public void CloseFullMap()
        {
            if (fullOpen)
            {
                fullOpen = false;
                if (Time.timeScale == 0f) Time.timeScale = 1f;
                GameInput.SetGameplay(true);
            }
            if (fullRoot != null) fullRoot.SetActive(false);
        }

        public void Observe(bool playing, RepresentativeArea area, bool bossActive)
        {
            if (canvas == null) return;
            if (!playing || area == null || area.Room == null)
            {
                if (fullOpen)
                {
                    fullOpen = false;
                    if (fullRoot != null) fullRoot.SetActive(false);
                }
                miniRoot.SetActive(false);
                return;
            }

            if (GameInput.MapDown && !NotaryJournalCanvas.IsOpen)
            {
                fullOpen = !fullOpen;
                Time.timeScale = fullOpen ? 0f : 1f;
                GameInput.SetGameplay(!fullOpen);
                UiAudioSynth.PlayMapToggle(fullOpen);
                dirty = true;
            }
            else if (fullOpen && GameInput.CancelDown)
            {
                ConsumedCancelFrame = Time.frameCount;
                CloseFullMap();
                UiAudioSynth.PlayMapToggle(false);
            }

            string roomId = area.Room.id;
            string stage = area.Room.stage;
            if (visited.Add(roomId)) dirty = true;
            if (roomId != activeRoomId || stage != activeStage || lastLang != Loc.Language)
            {
                activeRoomId = roomId;
                activeStage = stage;
                lastLang = Loc.Language;
                dirty = true;
            }

            miniRoot.SetActive(!bossActive && !fullOpen);
            fullRoot.SetActive(fullOpen);

            if (!dirty) return;
            dirty = false;
            RebuildGraphs();
        }

        private void RebuildGraphs()
        {
            if (script == null) script = RepresentativeScript.Load();
            var rooms = new List<StoryRoom>();
            if (script?.rooms != null)
            {
                foreach (var r in script.rooms)
                    if (r.stage == activeStage) rooms.Add(r);
            }
            if (rooms.Count == 0) return;

            int visitedCount = 0;
            for (int i = 0; i < rooms.Count; i++)
                if (visited.Contains(rooms[i].id)) visitedCount++;

            string districtLabel = DistrictTitle(activeStage);
            miniTitleText.font = NotaryUiTheme.TitleFont;
            miniHintText.font = NotaryUiTheme.BodyFont;
            miniTitleText.text = districtLabel;
            miniHintText.text = Loc.Get("map.button", GameInput.KeyLabel(GameInput.MapKey));

            fullTitleText.font = NotaryUiTheme.TitleFont;
            fullProgressText.font = NotaryUiTheme.BodyFont;
            fullLegendText.font = NotaryUiTheme.BodyFont;
            fullTitleText.text = "◆  " + districtLabel + "  (" + visitedCount + " / " + rooms.Count + ")";
            fullProgressText.text = Loc.Get("map.explored_total", visited.Count, script.rooms.Length);
            fullLegendText.text = Loc.Get("map.legend", GameInput.KeyLabel(GameInput.MapKey));

            RenderGraph(miniGraphRoot, rooms, false);
            if (fullOpen) RenderGraph(fullGraphRoot, rooms, true);
        }

        private void RenderGraph(RectTransform root, List<StoryRoom> rooms, bool detailed)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);

            int n = rooms.Count;
            int cols = detailed ? Mathf.Min(5, n) : Mathf.Min(5, n);
            int rows = Mathf.CeilToInt((float)n / cols);
            float width = detailed ? 960f : 186f;
            float height = detailed ? 420f : 74f;
            float stepX = cols > 1 ? width / cols : width;
            float stepY = rows > 1 ? height / rows : height;

            var positions = new Dictionary<string, Vector2>();
            for (int i = 0; i < n; i++)
            {
                int row = i / cols;
                int col = i % cols;
                if (row % 2 == 1) col = (cols - 1) - col;
                float x = (col + 0.5f) * stepX - width * 0.5f;
                float y = height * 0.5f - (row + 0.5f) * stepY;
                positions[rooms[i].id] = new Vector2(x, y);
            }

            for (int i = 0; i < n; i++)
            {
                var r = rooms[i];
                if (r.exits == null || !positions.TryGetValue(r.id, out var from)) continue;
                foreach (var ex in r.exits)
                {
                    if (string.IsNullOrEmpty(ex.room) || !positions.TryGetValue(ex.room, out var to)) continue;
                    if (string.CompareOrdinal(r.id, ex.room) > 0) continue;
                    DrawLine(root, from, to, detailed ? 3f : 1.5f, new Color(NotaryUiTheme.Gold.r, NotaryUiTheme.Gold.g, NotaryUiTheme.Gold.b, 0.48f));
                }
            }

            for (int i = 0; i < n; i++)
            {
                var r = rooms[i];
                var pos = positions[r.id];
                bool isCurrent = r.id == activeRoomId;
                bool isVisited = visited.Contains(r.id);
                bool isBoss = r.id.Contains("arena") || r.id.Contains("tribunal") || r.id.Contains("sanctuary");

                Color fill = isCurrent ? NotaryUiTheme.GoldGlow
                    : isBoss ? new Color(0.84f, 0.34f, 0.32f, isVisited ? 0.95f : 0.45f)
                    : isVisited ? NotaryUiTheme.TealAccent
                    : new Color(0.24f, 0.28f, 0.36f, 0.72f);
                Color border = isCurrent ? Color.white : NotaryUiTheme.GoldSoft;

                float nw = detailed ? 148f : (isCurrent ? 18f : 14f);
                float nh = detailed ? 52f : (isCurrent ? 12f : 10f);
                var node = NotaryUiTheme.CreateBorderedPanel(root, "Node_" + r.id, fill, border, detailed ? 1.8f : 1f);
                node.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                node.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                node.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                node.rectTransform.anchoredPosition = pos;
                node.rectTransform.sizeDelta = new Vector2(nw, nh);

                if (detailed)
                {
                    var inner = node.transform.Find("Inner")?.GetComponent<Image>();
                    if (inner != null)
                        inner.color = isCurrent ? new Color(0.24f, 0.22f, 0.14f, 0.98f)
                            : isBoss ? new Color(0.24f, 0.10f, 0.12f, 0.94f)
                            : isVisited ? NotaryUiTheme.CardSelectedBg : NotaryUiTheme.CardBg;
                    var lbl = NotaryUiTheme.CreateText(node.transform, "Label",
                        (isCurrent ? "◆ " : isBoss ? "❖ " : "") + r.Label, 12,
                        isCurrent ? NotaryUiTheme.GoldGlow : isVisited ? NotaryUiTheme.Ivory : NotaryUiTheme.ParchmentMuted,
                        TextAnchor.MiddleCenter, false, false);
                    NotaryUiTheme.Stretch(lbl.rectTransform, 6f, 4f, 6f, 4f);
                    lbl.resizeTextForBestFit = true;
                    lbl.resizeTextMinSize = 9;
                    lbl.resizeTextMaxSize = 12;
                }
            }
        }

        private static void DrawLine(Transform parent, Vector2 a, Vector2 b, float thickness, Color color)
        {
            var img = NotaryUiTheme.CreateImage(parent, "Link", color);
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            Vector2 d = b - a;
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.sizeDelta = new Vector2(d.magnitude, thickness);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        private static string DistrictTitle(string stage)
        {
            string key = "map.district." + (string.IsNullOrEmpty(stage) ? "hub" : stage);
            return Loc.Contains(key) ? Loc.Get(key) : (stage ?? "");
        }

        private void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }
    }
}
