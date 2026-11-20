using UnityEngine;
using UnityEngine.UI;

namespace RewindNotary
{
    public sealed class GameHudCanvas : MonoBehaviour
    {
        public struct HudSnapshot
        {
            public bool Visible;
            public string LocationTitle;
            public string RoomKey;
            public int PlayerHp, PlayerMaxHp;
            public string StatusLine;
            public string SubStatusLine;
            public string ObjectiveHint;
            public string NoticeText;
            public string ControlsText;
            public bool BossVisible;
            public string BossName;
            public string BossPhase;
            public int BossHp, BossMaxHp;
            public bool OverlayVisible;
            public string OverlayTitle;
            public string OverlayDetail;
        }

        private Canvas canvas;
        private CanvasGroup rootGroup;
        private Image portraitImage;
        private Text locationText;
        private Image playerHpFill, playerHpGhost;
        private Text playerHpText;
        private Text statusText, subStatusText;

        private GameObject bossPanel;
        private Text bossNameText, bossPhaseText, bossHpText;
        private Image bossHpFill, bossHpGhost;

        private GameObject objectivePanel;
        private Text objectiveText;

        private GameObject noticePanel;
        private Text noticeText;

        private Text controlsText;

        private GameObject overlayPanel;
        private Text overlayTitleText, overlayDetailText;

        private CanvasGroup roomBannerGroup;
        private Text roomBannerText;
        private string lastRoomKey;
        private float roomBannerAge = 99f;

        private float displayedPlayerRatio = 1f;
        private float displayedBossRatio = 1f;
        private string currentLang;

        public void Initialize(Transform parent)
        {
            canvas = NotaryUiTheme.CreateCanvas(parent, "Game HUD Canvas", 80);
            rootGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;

            BuildPlayerCluster();
            BuildRoomBanner();
            BuildBossBar();
            BuildObjectiveAndNotice();
            BuildControlsFooter();
            BuildCenterOverlay();
            currentLang = Loc.Language;
        }

        private void BuildPlayerCluster()
        {
            var cluster = NotaryUiTheme.CreateBorderedPanel(canvas.transform, "PlayerCluster", NotaryUiTheme.PanelBgSoft, NotaryUiTheme.GoldSoft, 1.5f);
            var rt = cluster.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(20f, -18f);
            rt.sizeDelta = new Vector2(420f, 92f);

            var portraitFrame = NotaryUiTheme.CreateBorderedPanel(cluster.transform, "PortraitFrame", NotaryUiTheme.CardBg, NotaryUiTheme.Gold, 1.5f);
            portraitFrame.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            portraitFrame.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            portraitFrame.rectTransform.pivot = new Vector2(0f, 0.5f);
            portraitFrame.rectTransform.anchoredPosition = new Vector2(10f, 0f);
            portraitFrame.rectTransform.sizeDelta = new Vector2(72f, 72f);

            portraitImage = NotaryUiTheme.CreateImage(portraitFrame.transform, "ReaPortrait", Color.white, NotaryUiTheme.LoadPortrait("rea"));
            NotaryUiTheme.Stretch(portraitImage.rectTransform, 2f, 2f, 2f, 2f);
            portraitImage.preserveAspect = true;

            locationText = NotaryUiTheme.CreateText(cluster.transform, "Location", "", 15, NotaryUiTheme.GoldGlow, TextAnchor.MiddleLeft, true, true);
            locationText.rectTransform.anchorMin = new Vector2(0f, 1f);
            locationText.rectTransform.anchorMax = new Vector2(1f, 1f);
            locationText.rectTransform.pivot = new Vector2(0f, 1f);
            locationText.rectTransform.offsetMin = new Vector2(92f, -30f);
            locationText.rectTransform.offsetMax = new Vector2(-12f, -8f);

            var barFrame = NotaryUiTheme.CreateBorderedPanel(cluster.transform, "HpTrack", NotaryUiTheme.BarTrack, NotaryUiTheme.GoldSoft, 1f);
            barFrame.rectTransform.anchorMin = new Vector2(0f, 1f);
            barFrame.rectTransform.anchorMax = new Vector2(1f, 1f);
            barFrame.rectTransform.pivot = new Vector2(0f, 1f);
            barFrame.rectTransform.offsetMin = new Vector2(92f, -49f);
            barFrame.rectTransform.offsetMax = new Vector2(-62f, -34f);

            playerHpGhost = NotaryUiTheme.CreateImage(barFrame.transform, "HpGhost", NotaryUiTheme.HealthGhost);
            playerHpGhost.rectTransform.anchorMin = Vector2.zero;
            playerHpGhost.rectTransform.anchorMax = Vector2.one;
            playerHpGhost.rectTransform.offsetMin = new Vector2(1f, 1f);
            playerHpGhost.rectTransform.offsetMax = new Vector2(-1f, -1f);

            playerHpFill = NotaryUiTheme.CreateImage(barFrame.transform, "HpFill", NotaryUiTheme.HealthFill);
            playerHpFill.rectTransform.anchorMin = Vector2.zero;
            playerHpFill.rectTransform.anchorMax = Vector2.one;
            playerHpFill.rectTransform.offsetMin = new Vector2(1f, 1f);
            playerHpFill.rectTransform.offsetMax = new Vector2(-1f, -1f);

            playerHpText = NotaryUiTheme.CreateText(cluster.transform, "HpText", "6/6", 13, NotaryUiTheme.Ivory, TextAnchor.MiddleRight, false, true);
            playerHpText.rectTransform.anchorMin = new Vector2(1f, 1f);
            playerHpText.rectTransform.anchorMax = new Vector2(1f, 1f);
            playerHpText.rectTransform.pivot = new Vector2(1f, 1f);
            playerHpText.rectTransform.anchoredPosition = new Vector2(-12f, -32f);
            playerHpText.rectTransform.sizeDelta = new Vector2(48f, 19f);

            statusText = NotaryUiTheme.CreateText(cluster.transform, "Status", "", 12, NotaryUiTheme.Ivory, TextAnchor.MiddleLeft, false, true);
            statusText.rectTransform.anchorMin = new Vector2(0f, 0f);
            statusText.rectTransform.anchorMax = new Vector2(1f, 0f);
            statusText.rectTransform.pivot = new Vector2(0f, 0f);
            statusText.rectTransform.offsetMin = new Vector2(92f, 22f);
            statusText.rectTransform.offsetMax = new Vector2(-12f, 40f);

            subStatusText = NotaryUiTheme.CreateText(cluster.transform, "SubStatus", "", 11, NotaryUiTheme.ParchmentMuted, TextAnchor.MiddleLeft, false, true);
            subStatusText.rectTransform.anchorMin = new Vector2(0f, 0f);
            subStatusText.rectTransform.anchorMax = new Vector2(1f, 0f);
            subStatusText.rectTransform.pivot = new Vector2(0f, 0f);
            subStatusText.rectTransform.offsetMin = new Vector2(92f, 6f);
            subStatusText.rectTransform.offsetMax = new Vector2(-12f, 22f);
        }

        private void BuildRoomBanner()
        {
            var go = NotaryUiTheme.CreateRect(canvas.transform, "RoomEntryBanner");
            go.anchorMin = new Vector2(0.5f, 1f);
            go.anchorMax = new Vector2(0.5f, 1f);
            go.pivot = new Vector2(0.5f, 1f);
            go.anchoredPosition = new Vector2(0f, -124f);
            go.sizeDelta = new Vector2(560f, 54f);
            roomBannerGroup = go.gameObject.AddComponent<CanvasGroup>();
            roomBannerGroup.alpha = 0f;

            var bg = NotaryUiTheme.CreateImage(go, "Bg", new Color(0.04f, 0.055f, 0.095f, 0.72f));
            NotaryUiTheme.Stretch(bg.rectTransform);

            var topRule = NotaryUiTheme.CreateImage(go, "TopRule", NotaryUiTheme.Gold);
            topRule.rectTransform.anchorMin = new Vector2(0.15f, 1f);
            topRule.rectTransform.anchorMax = new Vector2(0.85f, 1f);
            topRule.rectTransform.sizeDelta = new Vector2(0f, 1.5f);

            var botRule = NotaryUiTheme.CreateImage(go, "BotRule", NotaryUiTheme.Gold);
            botRule.rectTransform.anchorMin = new Vector2(0.22f, 0f);
            botRule.rectTransform.anchorMax = new Vector2(0.78f, 0f);
            botRule.rectTransform.sizeDelta = new Vector2(0f, 1.5f);

            roomBannerText = NotaryUiTheme.CreateText(go, "Text", "", 22, NotaryUiTheme.GoldGlow, TextAnchor.MiddleCenter, true, true);
            NotaryUiTheme.Stretch(roomBannerText.rectTransform, 20f, 4f, 20f, 4f);
        }

        private void BuildBossBar()
        {
            var frame = NotaryUiTheme.CreateBorderedPanel(canvas.transform, "BossBar", NotaryUiTheme.PanelBg, NotaryUiTheme.Gold, 1.8f);
            bossPanel = frame.gameObject;
            var rt = frame.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 36f);
            rt.sizeDelta = new Vector2(740f, 64f);

            bossNameText = NotaryUiTheme.CreateText(frame.transform, "BossName", "", 17, NotaryUiTheme.GoldGlow, TextAnchor.MiddleLeft, true, true);
            bossNameText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            bossNameText.rectTransform.anchorMax = new Vector2(0.62f, 1f);
            bossNameText.rectTransform.offsetMin = new Vector2(18f, 0f);
            bossNameText.rectTransform.offsetMax = new Vector2(0f, -6f);

            bossPhaseText = NotaryUiTheme.CreateText(frame.transform, "BossPhase", "", 13, NotaryUiTheme.ParchmentMuted, TextAnchor.MiddleRight, false, true);
            bossPhaseText.rectTransform.anchorMin = new Vector2(0.45f, 0.5f);
            bossPhaseText.rectTransform.anchorMax = new Vector2(0.88f, 1f);
            bossPhaseText.rectTransform.offsetMin = Vector2.zero;
            bossPhaseText.rectTransform.offsetMax = new Vector2(-8f, -6f);

            bossHpText = NotaryUiTheme.CreateText(frame.transform, "BossHpText", "", 13, NotaryUiTheme.Ivory, TextAnchor.MiddleRight, false, true);
            bossHpText.rectTransform.anchorMin = new Vector2(0.88f, 0.5f);
            bossHpText.rectTransform.anchorMax = new Vector2(1f, 1f);
            bossHpText.rectTransform.offsetMin = Vector2.zero;
            bossHpText.rectTransform.offsetMax = new Vector2(-16f, -6f);

            var track = NotaryUiTheme.CreateBorderedPanel(frame.transform, "Track", NotaryUiTheme.BarTrack, NotaryUiTheme.GoldSoft, 1f);
            track.rectTransform.anchorMin = new Vector2(0f, 0f);
            track.rectTransform.anchorMax = new Vector2(1f, 0f);
            track.rectTransform.offsetMin = new Vector2(18f, 12f);
            track.rectTransform.offsetMax = new Vector2(-18f, 28f);

            bossHpGhost = NotaryUiTheme.CreateImage(track.transform, "Ghost", NotaryUiTheme.HealthGhost);
            NotaryUiTheme.Stretch(bossHpGhost.rectTransform, 1f, 1f, 1f, 1f);

            bossHpFill = NotaryUiTheme.CreateImage(track.transform, "Fill", NotaryUiTheme.BossFill);
            NotaryUiTheme.Stretch(bossHpFill.rectTransform, 1f, 1f, 1f, 1f);

            bossPanel.SetActive(false);
        }

        private void BuildObjectiveAndNotice()
        {
            var obj = NotaryUiTheme.CreateBorderedPanel(canvas.transform, "ObjectivePill", NotaryUiTheme.PanelBgSoft, NotaryUiTheme.GoldSoft, 1.2f);
            objectivePanel = obj.gameObject;
            var ort = obj.rectTransform;
            ort.anchorMin = new Vector2(1f, 1f);
            ort.anchorMax = new Vector2(1f, 1f);
            ort.pivot = new Vector2(1f, 1f);
            ort.anchoredPosition = new Vector2(-20f, -18f);
            ort.sizeDelta = new Vector2(430f, 58f);

            objectiveText = NotaryUiTheme.CreateText(obj.transform, "Text", "", 14, NotaryUiTheme.Ivory, TextAnchor.MiddleLeft, false, true);
            NotaryUiTheme.Stretch(objectiveText.rectTransform, 14f, 6f, 14f, 6f);
            objectiveText.resizeTextForBestFit = true;
            objectiveText.resizeTextMinSize = 11;
            objectiveText.resizeTextMaxSize = 14;
            objectivePanel.SetActive(false);

            var not = NotaryUiTheme.CreateBorderedPanel(canvas.transform, "NoticePill", NotaryUiTheme.PanelBgSoft, NotaryUiTheme.TealAccent, 1.2f);
            noticePanel = not.gameObject;
            var nrt = not.rectTransform;
            nrt.anchorMin = new Vector2(1f, 1f);
            nrt.anchorMax = new Vector2(1f, 1f);
            nrt.pivot = new Vector2(1f, 1f);
            nrt.anchoredPosition = new Vector2(-20f, -84f);
            nrt.sizeDelta = new Vector2(430f, 54f);

            noticeText = NotaryUiTheme.CreateText(not.transform, "Text", "", 13, NotaryUiTheme.TealAccent, TextAnchor.MiddleLeft, false, true);
            NotaryUiTheme.Stretch(noticeText.rectTransform, 14f, 6f, 14f, 6f);
            noticeText.resizeTextForBestFit = true;
            noticeText.resizeTextMinSize = 11;
            noticeText.resizeTextMaxSize = 13;
            noticePanel.SetActive(false);
        }

        private void BuildControlsFooter()
        {
            var bar = NotaryUiTheme.CreateImage(canvas.transform, "ControlsBar", new Color(0.03f, 0.042f, 0.072f, 0.62f));
            bar.rectTransform.anchorMin = new Vector2(0f, 0f);
            bar.rectTransform.anchorMax = new Vector2(1f, 0f);
            bar.rectTransform.pivot = new Vector2(0.5f, 0f);
            bar.rectTransform.anchoredPosition = Vector2.zero;
            bar.rectTransform.sizeDelta = new Vector2(0f, 26f);

            controlsText = NotaryUiTheme.CreateText(bar.transform, "Controls", "", 12, NotaryUiTheme.ParchmentMuted, TextAnchor.MiddleCenter, false, false);
            NotaryUiTheme.Stretch(controlsText.rectTransform, 18f, 2f, 18f, 2f);
        }

        private void BuildCenterOverlay()
        {
            var panel = NotaryUiTheme.CreateBorderedPanel(canvas.transform, "CenterOverlay", NotaryUiTheme.PanelBg, NotaryUiTheme.Gold, 2f);
            overlayPanel = panel.gameObject;
            var rt = panel.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(540f, 128f);

            overlayTitleText = NotaryUiTheme.CreateText(panel.transform, "Title", "", 28, NotaryUiTheme.GoldGlow, TextAnchor.MiddleCenter, true, true);
            overlayTitleText.rectTransform.anchorMin = new Vector2(0f, 0.45f);
            overlayTitleText.rectTransform.anchorMax = new Vector2(1f, 0.92f);

            overlayDetailText = NotaryUiTheme.CreateText(panel.transform, "Detail", "", 16, NotaryUiTheme.Ivory, TextAnchor.MiddleCenter, false, true);
            overlayDetailText.rectTransform.anchorMin = new Vector2(0f, 0.12f);
            overlayDetailText.rectTransform.anchorMax = new Vector2(1f, 0.48f);

            overlayPanel.SetActive(false);
        }

        public void Render(HudSnapshot s)
        {
            if (canvas == null) return;
            canvas.gameObject.SetActive(s.Visible);
            if (!s.Visible) return;

            if (currentLang != Loc.Language)
            {
                currentLang = Loc.Language;
                RefreshFonts();
            }

            locationText.text = s.LocationTitle ?? "";
            float pMax = Mathf.Max(1, s.PlayerMaxHp);
            float pRatio = Mathf.Clamp01(s.PlayerHp / pMax);
            if (pRatio > displayedPlayerRatio) displayedPlayerRatio = pRatio;
            else displayedPlayerRatio = Mathf.MoveTowards(displayedPlayerRatio, pRatio, Time.unscaledDeltaTime * 0.75f);
            playerHpFill.rectTransform.anchorMax = new Vector2(pRatio, 1f);
            playerHpGhost.rectTransform.anchorMax = new Vector2(displayedPlayerRatio, 1f);
            playerHpText.text = s.PlayerHp + " / " + s.PlayerMaxHp;

            statusText.text = s.StatusLine ?? "";
            subStatusText.text = s.SubStatusLine ?? "";

            if (!string.IsNullOrEmpty(s.RoomKey) && s.RoomKey != lastRoomKey)
            {
                lastRoomKey = s.RoomKey;
                roomBannerText.text = s.LocationTitle ?? "";
                roomBannerAge = 0f;
            }
            if (roomBannerAge < 2.6f)
            {
                roomBannerAge += Time.unscaledDeltaTime;
                float a = roomBannerAge < 0.35f ? roomBannerAge / 0.35f
                    : roomBannerAge > 2.0f ? Mathf.Clamp01((2.6f - roomBannerAge) / 0.6f) : 1f;
                roomBannerGroup.alpha = a;
            }
            else roomBannerGroup.alpha = 0f;

            bossPanel.SetActive(s.BossVisible);
            if (s.BossVisible)
            {
                bossNameText.text = s.BossName ?? "";
                bossPhaseText.text = s.BossPhase ?? "";
                float bMax = Mathf.Max(1, s.BossMaxHp);
                float bRatio = Mathf.Clamp01(s.BossHp / bMax);
                if (bRatio > displayedBossRatio) displayedBossRatio = bRatio;
                else displayedBossRatio = Mathf.MoveTowards(displayedBossRatio, bRatio, Time.unscaledDeltaTime * 0.65f);
                bossHpFill.rectTransform.anchorMax = new Vector2(bRatio, 1f);
                bossHpGhost.rectTransform.anchorMax = new Vector2(displayedBossRatio, 1f);
                bossHpText.text = s.BossHp + " / " + s.BossMaxHp;
            }

            bool hasObj = !string.IsNullOrEmpty(s.ObjectiveHint);
            objectivePanel.SetActive(hasObj);
            if (hasObj) objectiveText.text = s.ObjectiveHint;

            bool hasNotice = !string.IsNullOrEmpty(s.NoticeText);
            noticePanel.SetActive(hasNotice);
            if (hasNotice) noticeText.text = s.NoticeText;

            controlsText.text = s.ControlsText ?? "";

            overlayPanel.SetActive(s.OverlayVisible);
            if (s.OverlayVisible)
            {
                overlayTitleText.text = s.OverlayTitle ?? "";
                overlayDetailText.text = s.OverlayDetail ?? "";
            }
        }

        private void RefreshFonts()
        {
            var body = NotaryUiTheme.BodyFont;
            var title = NotaryUiTheme.TitleFont;
            locationText.font = title;
            roomBannerText.font = title;
            bossNameText.font = title;
            overlayTitleText.font = title;
            playerHpText.font = body;
            statusText.font = body;
            subStatusText.font = body;
            bossPhaseText.font = body;
            bossHpText.font = body;
            objectiveText.font = body;
            noticeText.font = body;
            controlsText.font = body;
            overlayDetailText.font = body;
        }

        private void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }
    }
}
