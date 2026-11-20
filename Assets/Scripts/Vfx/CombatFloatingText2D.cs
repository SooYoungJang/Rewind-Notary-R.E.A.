using UnityEngine;
using UnityEngine.UI;

namespace RewindNotary
{
    public sealed class CombatFloatingText2D : MonoBehaviour
    {
        private struct Entry
        {
            public RectTransform Rect;
            public Text Label;
            public Vector3 WorldPos;
            public Vector2 Drift;
            public Color BaseColor;
            public float Age;
            public float Duration;
            public float BaseScale;
            public bool Active;
        }

        public static CombatFloatingText2D Instance { get; private set; }
        private const int PoolSize = 24;
        private Canvas canvas;
        private RectTransform canvasRect;
        private Entry[] pool;
        private int cursor;
        private string currentLang;

        public static void Ensure(Transform parent)
        {
            if (Instance != null) return;
            var go = new GameObject("Combat Floating Text");
            if (parent != null) go.transform.SetParent(parent, false);
            Instance = go.AddComponent<CombatFloatingText2D>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            canvas = NotaryUiTheme.CreateCanvas(transform, "Floating Text Canvas", 75);
            canvasRect = (RectTransform)canvas.transform;
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            pool = new Entry[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var txt = NotaryUiTheme.CreateText(canvas.transform, "Pop_" + i, "", 22, Color.white, TextAnchor.MiddleCenter, true, true);
                var outline = txt.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.02f, 0.03f, 0.05f, 0.92f);
                outline.effectDistance = new Vector2(1.4f, -1.4f);
                txt.rectTransform.anchorMin = Vector2.zero;
                txt.rectTransform.anchorMax = Vector2.zero;
                txt.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                txt.rectTransform.sizeDelta = new Vector2(220f, 44f);
                txt.gameObject.SetActive(false);
                pool[i] = new Entry { Rect = txt.rectTransform, Label = txt };
            }
            currentLang = Loc.Language;
        }

        public static void ClearAll()
        {
            if (Instance == null || Instance.pool == null) return;
            for (int i = 0; i < PoolSize; i++)
            {
                Instance.pool[i].Active = false;
                if (Instance.pool[i].Label != null) Instance.pool[i].Label.gameObject.SetActive(false);
            }
        }

        public static void SpawnDamage(Vector3 worldPos, int amount, bool critical = false, bool isPlayer = false)
        {
            if (Instance == null || amount <= 0) return;
            string text = isPlayer ? "-" + amount : (critical ? amount + "!" : amount.ToString());
            Color color = isPlayer
                ? new Color(1f, 0.36f, 0.38f, 1f)
                : critical ? NotaryUiTheme.GoldGlow : NotaryUiTheme.Ivory;
            int size = isPlayer ? 23 : critical ? 27 : 21;
            Instance.Emit(worldPos, text, color, size, critical ? 0.82f : 0.68f, critical ? 1.15f : 1f);
        }

        public static void SpawnTag(Vector3 worldPos, string text, Color color, int fontSize = 23)
        {
            if (Instance == null || string.IsNullOrEmpty(text)) return;
            Instance.Emit(worldPos, text, color, fontSize, 0.85f, 1.12f);
        }

        private void Emit(Vector3 worldPos, string text, Color color, int fontSize, float duration, float baseScale)
        {
            if (pool == null) return;
            int idx = cursor;
            cursor = (cursor + 1) % PoolSize;
            var e = pool[idx];
            e.WorldPos = worldPos + new Vector3(Random.Range(-0.22f, 0.22f), Random.Range(-0.08f, 0.14f), 0f);
            e.Drift = new Vector2(Random.Range(-0.55f, 0.55f), Random.Range(1.25f, 1.75f));
            e.BaseColor = color;
            e.Age = 0f;
            e.Duration = duration;
            e.BaseScale = baseScale;
            e.Active = true;
            e.Label.text = text;
            e.Label.fontSize = fontSize;
            e.Label.color = color;
            e.Label.gameObject.SetActive(true);
            pool[idx] = e;
            PositionEntry(ref pool[idx]);
        }

        private void LateUpdate()
        {
            if (pool == null) return;
            if (currentLang != Loc.Language)
            {
                currentLang = Loc.Language;
                var title = NotaryUiTheme.TitleFont;
                for (int i = 0; i < PoolSize; i++)
                    if (pool[i].Label != null) pool[i].Label.font = title;
            }
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < PoolSize; i++)
            {
                if (!pool[i].Active) continue;
                var e = pool[i];
                e.Age += dt;
                if (e.Age >= e.Duration)
                {
                    e.Active = false;
                    e.Label.gameObject.SetActive(false);
                    pool[i] = e;
                    continue;
                }
                float t = e.Age / e.Duration;
                float pop = t < 0.16f ? Mathf.Lerp(1.42f, 1f, t / 0.16f) : Mathf.Lerp(1f, 0.90f, (t - 0.16f) / 0.84f);
                e.Rect.localScale = Vector3.one * (pop * e.BaseScale);
                float alpha = t > 0.58f ? Mathf.Clamp01(1f - (t - 0.58f) / 0.42f) : 1f;
                e.Label.color = new Color(e.BaseColor.r, e.BaseColor.g, e.BaseColor.b, alpha);
                pool[i] = e;
                PositionEntry(ref pool[i]);
            }
        }

        private void PositionEntry(ref Entry e)
        {
            var cam = Camera.main;
            if (cam == null || canvasRect == null) return;
            float t = Mathf.Clamp01(e.Age / Mathf.Max(0.01f, e.Duration));
            Vector3 wp = e.WorldPos + new Vector3(e.Drift.x * t, e.Drift.y * (1f - (1f - t) * (1f - t)), 0f);
            Vector3 sp = cam.WorldToScreenPoint(wp);
            if (sp.z < 0f) { e.Label.gameObject.SetActive(false); return; }
            Vector2 size = canvasRect.rect.size;
            float nx = Screen.width > 0 ? sp.x / Screen.width : 0.5f;
            float ny = Screen.height > 0 ? sp.y / Screen.height : 0.5f;
            e.Rect.anchoredPosition = new Vector2(nx * size.x, ny * size.y);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (canvas != null) Destroy(canvas.gameObject);
        }
    }
}
