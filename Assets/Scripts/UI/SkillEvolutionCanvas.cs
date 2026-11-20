using UnityEngine;
using UnityEngine.UI;

namespace RewindNotary
{
    public sealed class SkillEvolutionCanvas : MonoBehaviour
    {
        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform cardRect;
        private Image borderImage;
        private Text titleText;
        private Text detailText;
        private string currentLang;

        public void Initialize(Transform parent)
        {
            canvas = NotaryUiTheme.CreateCanvas(parent, "Skill Evolution Canvas", 120);
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;

            var outer = NotaryUiTheme.CreateBorderedPanel(canvas.transform, "Banner", NotaryUiTheme.PanelBg, NotaryUiTheme.Gold, 2f);
            borderImage = outer;
            cardRect = outer.rectTransform;
            cardRect.anchorMin = new Vector2(0.5f, 1f);
            cardRect.anchorMax = new Vector2(0.5f, 1f);
            cardRect.pivot = new Vector2(0.5f, 1f);
            cardRect.anchoredPosition = new Vector2(0f, -24f);
            cardRect.sizeDelta = new Vector2(680f, 92f);

            var topLine = NotaryUiTheme.CreateImage(outer.transform, "TopAccent", NotaryUiTheme.GoldGlow);
            topLine.rectTransform.anchorMin = new Vector2(0.12f, 1f);
            topLine.rectTransform.anchorMax = new Vector2(0.88f, 1f);
            topLine.rectTransform.pivot = new Vector2(0.5f, 1f);
            topLine.rectTransform.anchoredPosition = new Vector2(0f, -6f);
            topLine.rectTransform.sizeDelta = new Vector2(0f, 1.5f);

            titleText = NotaryUiTheme.CreateText(outer.transform, "Title", "", 22, NotaryUiTheme.GoldGlow, TextAnchor.MiddleCenter, true, true);
            titleText.rectTransform.anchorMin = new Vector2(0f, 0.48f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 0.92f);
            titleText.rectTransform.offsetMin = new Vector2(20f, 0f);
            titleText.rectTransform.offsetMax = new Vector2(-20f, 0f);

            detailText = NotaryUiTheme.CreateText(outer.transform, "Detail", "", 15, NotaryUiTheme.Ivory, TextAnchor.MiddleCenter, false, true);
            detailText.rectTransform.anchorMin = new Vector2(0f, 0.10f);
            detailText.rectTransform.anchorMax = new Vector2(1f, 0.50f);
            detailText.rectTransform.offsetMin = new Vector2(24f, 0f);
            detailText.rectTransform.offsetMax = new Vector2(-24f, 0f);
            currentLang = Loc.Language;
        }

        public void Render(bool visible, string title, string detail, Color tint, float alpha, float age)
        {
            if (canvas == null) return;
            if (!visible)
            {
                group.alpha = 0f;
                return;
            }
            if (currentLang != Loc.Language)
            {
                currentLang = Loc.Language;
                titleText.font = NotaryUiTheme.TitleFont;
                detailText.font = NotaryUiTheme.BodyFont;
            }
            group.alpha = Mathf.Clamp01(alpha);
            borderImage.color = Color.Lerp(NotaryUiTheme.Gold, tint, 0.45f);
            titleText.color = Color.Lerp(NotaryUiTheme.GoldGlow, tint, 0.35f);
            titleText.text = "◆  " + (title ?? "") + "  ◆";
            detailText.text = detail ?? "";
            float pop = age < 0.22f ? Mathf.Lerp(0.92f, 1f, age / 0.22f) : 1f;
            cardRect.localScale = new Vector3(pop, pop, 1f);
        }

        private void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }
    }
}
