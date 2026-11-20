using UnityEngine;
using UnityEngine.UI;

namespace RewindNotary
{
    public sealed class CinematicJuiceDirector : MonoBehaviour
    {
        private Canvas canvas;
        private CanvasGroup introGroup;
        private RectTransform topRule, botRule;
        private Text introTitle, introSubtitle;
        private string lastBossBannerKey;
        private float introAge = 99f;
        private bool wasBossDead;
        private float slowMoRemaining;
        private bool slowMoOwned;
        private string currentLang;

        public void Initialize(Transform parent)
        {
            canvas = NotaryUiTheme.CreateCanvas(parent, "Boss Intro Canvas", 115);
            introGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            introGroup.blocksRaycasts = false;
            introGroup.interactable = false;
            introGroup.alpha = 0f;

            var band = NotaryUiTheme.CreateImage(canvas.transform, "Band", new Color(0.032f, 0.042f, 0.078f, 0.84f));
            band.rectTransform.anchorMin = new Vector2(0f, 0.56f);
            band.rectTransform.anchorMax = new Vector2(1f, 0.72f);
            band.rectTransform.offsetMin = Vector2.zero;
            band.rectTransform.offsetMax = Vector2.zero;

            var top = NotaryUiTheme.CreateImage(band.transform, "TopGoldRule", NotaryUiTheme.Gold);
            topRule = top.rectTransform;
            topRule.anchorMin = new Vector2(0.5f, 1f);
            topRule.anchorMax = new Vector2(0.5f, 1f);
            topRule.pivot = new Vector2(0.5f, 1f);
            topRule.anchoredPosition = Vector2.zero;
            topRule.sizeDelta = new Vector2(820f, 2f);

            var bot = NotaryUiTheme.CreateImage(band.transform, "BotGoldRule", NotaryUiTheme.Gold);
            botRule = bot.rectTransform;
            botRule.anchorMin = new Vector2(0.5f, 0f);
            botRule.anchorMax = new Vector2(0.5f, 0f);
            botRule.pivot = new Vector2(0.5f, 0f);
            botRule.anchoredPosition = Vector2.zero;
            botRule.sizeDelta = new Vector2(820f, 2f);

            introTitle = NotaryUiTheme.CreateText(band.transform, "BossTitle", "", 36, NotaryUiTheme.GoldGlow, TextAnchor.MiddleCenter, true, true);
            introTitle.rectTransform.anchorMin = new Vector2(0.1f, 0.36f);
            introTitle.rectTransform.anchorMax = new Vector2(0.9f, 0.94f);

            introSubtitle = NotaryUiTheme.CreateText(band.transform, "BossSub", "", 16, NotaryUiTheme.ParchmentMuted, TextAnchor.MiddleCenter, false, true);
            introSubtitle.rectTransform.anchorMin = new Vector2(0.1f, 0.06f);
            introSubtitle.rectTransform.anchorMax = new Vector2(0.9f, 0.40f);
            currentLang = Loc.Language;
        }

        public void Observe(bool playing, bool bossVisible, string bossName, string bossSubtitle, CombatMotor2D bossActor)
        {
            if (currentLang != Loc.Language)
            {
                currentLang = Loc.Language;
                introTitle.font = NotaryUiTheme.TitleFont;
                introSubtitle.font = NotaryUiTheme.BodyFont;
            }

            if (!playing)
            {
                ReleaseSlowMo();
                introGroup.alpha = 0f;
                return;
            }

            if (bossVisible && !string.IsNullOrEmpty(bossName))
            {
                if (bossName != lastBossBannerKey)
                {
                    lastBossBannerKey = bossName;
                    introTitle.text = "◆   " + bossName + "   ◆";
                    introSubtitle.text = string.IsNullOrEmpty(bossSubtitle) ? "" : bossSubtitle;
                    introAge = 0f;
                    AtmospherePostEffect.PulseImpact(0.42f);
                }
            }
            else if (!bossVisible)
            {
                lastBossBannerKey = null;
            }

            bool bossDead = bossActor != null && bossActor.gameObject.activeInHierarchy && bossActor.Health != null && bossActor.Health.Dead;
            if (bossDead && !wasBossDead)
            {
                AtmospherePostEffect.PulseImpact(0.95f);
                if (bossActor != null)
                    ActorVisualPolish2D.SpawnSkillFx(SkillRules.BasicSword, bossActor.transform.position + Vector3.up, 1, 3.6f, 0.55f);
                if (AutoTestRuntime.Report == null)
                {
                    slowMoRemaining = 0.36f;
                    slowMoOwned = true;
                    Time.timeScale = 0.25f;
                }
            }
            wasBossDead = bossDead;

            if (slowMoOwned)
            {
                slowMoRemaining -= Time.unscaledDeltaTime;
                if (slowMoRemaining <= 0f)
                {
                    ReleaseSlowMo();
                }
                else
                {
                    float u = 1f - Mathf.Clamp01(slowMoRemaining / 0.36f);
                    Time.timeScale = Mathf.Lerp(0.25f, 1f, u * u);
                }
            }

            if (introAge < 2.5f)
            {
                introAge += Time.unscaledDeltaTime;
                float alpha = introAge < 0.32f ? introAge / 0.32f
                    : introAge > 1.95f ? Mathf.Clamp01((2.5f - introAge) / 0.55f) : 1f;
                introGroup.alpha = alpha;
                float expand = Mathf.SmoothStep(240f, 880f, Mathf.Clamp01(introAge / 0.45f));
                topRule.sizeDelta = new Vector2(expand, 2f);
                botRule.sizeDelta = new Vector2(expand, 2f);
            }
            else
            {
                introGroup.alpha = 0f;
            }
        }

        public void ResetState()
        {
            lastBossBannerKey = null;
            wasBossDead = false;
            introAge = 99f;
            ReleaseSlowMo();
            if (introGroup != null) introGroup.alpha = 0f;
        }

        private void ReleaseSlowMo()
        {
            if (!slowMoOwned) return;
            slowMoOwned = false;
            slowMoRemaining = 0f;
            if (Time.timeScale > 0f) Time.timeScale = 1f;
        }

        private void OnDisable() { ReleaseSlowMo(); }
        private void OnDestroy()
        {
            ReleaseSlowMo();
            if (canvas != null) Destroy(canvas.gameObject);
        }
    }
}
