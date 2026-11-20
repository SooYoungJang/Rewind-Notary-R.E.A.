using UnityEngine;
using UnityEngine.UI;

namespace RewindNotary
{
    public sealed class GameHudCanvas : MonoBehaviour
    {
        public struct SkillBadge
        {
            public string ShortLabel;
            public string DirCue;
            public bool Owned;
            public int Stage;
            public Color Accent;
        }

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
            public SkillBadge[] Skills;
            public float DashReadyRatio;
            public bool AirDashReady;
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

        private CanvasGroup toastGroup;
        private Text toastText;
        private float toastAge = 99f;

        private Image lowHpVignette;

        private CanvasGroup skillCardGroup;
        private Image skillCardBorder;
        private Text skillCardHeader, skillCardTitle, skillCardInput, skillCardTip;
        private float skillCardAge = 99f;

        private Image[] skillSlotBorders;
        private Image[] skillSlotFills;
        private Text[] skillSlotLabels;
        private Text[] skillSlotPips;
        private Image dashCooldownFill;
        private Image airDashPip;

        private float displayedPlayerRatio = 1f;
        private float displayedBossRatio = 1f;
        private string currentLang;

        public void Initialize(Transform parent)
        {
            canvas = NotaryUiTheme.CreateCanvas(parent, "Game HUD Canvas", 80);
            rootGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;

            BuildScreenOverlays();
            BuildPlayerCluster();
            BuildLoadoutStrip();
            BuildRoomBanner();
            BuildCheckpointToast();
            BuildSkillAcquireCard();
            BuildBossBar();
            BuildObjectiveAndNotice();
            BuildControlsFooter();
            BuildCenterOverlay();
            currentLang = Loc.Language;
        }


        public void ShowToast(string text)
        {
            if (toastText == null) return;
            toastText.text = "◆  " + (text ?? "") + "  ◆";
            toastAge = 0f;
        }

        public void ShowSkillCard(string skillName, string inputFormula, string tip, Color accent)
        {
            if (skillCardGroup == null) return;
            skillCardBorder.color = Color.Lerp(NotaryUiTheme.Gold, accent, 0.35f);
            skillCardHeader.text = Loc.Get("skillcard.acquired");
            skillCardTitle.text = skillName ?? "";
            skillCardInput.text = Loc.Get("journal.input_formula", inputFormula ?? "");
            skillCardTip.text = tip ?? "";
            skillCardAge = 0f;
        }

        private void BuildScreenOverlays()
        {
            var edgeSprite = CreateEdgeVignetteSprite();
            lowHpVignette = NotaryUiTheme.CreateImage(canvas.transform, "LowHpVignette", new Color(0.85f, 0.12f, 0.16f, 0f), edgeSprite);
            NotaryUiTheme.Stretch(lowHpVignette.rectTransform);
            lowHpVignette.raycastTarget = false;

        }

        private void BuildSkillAcquireCard()
        {
            skillCardBorder = NotaryUiTheme.CreateBorderedPanel(canvas.transform, "SkillAcquireCard", NotaryUiTheme.PanelBg, NotaryUiTheme.Gold, 2f);
            var rt = skillCardBorder.rectTransform;
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-24f, 18f);
            rt.sizeDelta = new Vector2(376f, 140f);
            skillCardGroup = skillCardBorder.gameObject.AddComponent<CanvasGroup>();
            skillCardGroup.alpha = 0f;

            skillCardHeader = NotaryUiTheme.CreateText(skillCardBorder.transform, "Header", "", 12, NotaryUiTheme.TealAccent, TextAnchor.MiddleLeft, true, false);
            skillCardHeader.rectTransform.anchorMin = new Vector2(0f, 1f);
            skillCardHeader.rectTransform.anchorMax = new Vector2(1f, 1f);
            skillCardHeader.rectTransform.offsetMin = new Vector2(16f, -28f);
            skillCardHeader.rectTransform.offsetMax = new Vector2(-16f, -8f);

            skillCardTitle = NotaryUiTheme.CreateText(skillCardBorder.transform, "Title", "", 20, NotaryUiTheme.GoldGlow, TextAnchor.MiddleLeft, true, true);
            skillCardTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            skillCardTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            skillCardTitle.rectTransform.offsetMin = new Vector2(16f, -64f);
            skillCardTitle.rectTransform.offsetMax = new Vector2(-16f, -26f);
            skillCardTitle.resizeTextForBestFit = true;
            skillCardTitle.resizeTextMinSize = 14;
            skillCardTitle.resizeTextMaxSize = 20;

            skillCardInput = NotaryUiTheme.CreateText(skillCardBorder.transform, "Input", "", 13, NotaryUiTheme.Gold, TextAnchor.MiddleLeft, true, false);
            skillCardInput.rectTransform.anchorMin = new Vector2(0f, 1f);
            skillCardInput.rectTransform.anchorMax = new Vector2(1f, 1f);
            skillCardInput.rectTransform.offsetMin = new Vector2(16f, -88f);
            skillCardInput.rectTransform.offsetMax = new Vector2(-16f, -64f);

            skillCardTip = NotaryUiTheme.CreateText(skillCardBorder.transform, "Tip", "", 12, NotaryUiTheme.Ivory, TextAnchor.UpperLeft, false, false);
            skillCardTip.rectTransform.anchorMin = new Vector2(0f, 0f);
            skillCardTip.rectTransform.anchorMax = new Vector2(1f, 0f);
            skillCardTip.rectTransform.offsetMin = new Vector2(16f, 8f);
            skillCardTip.rectTransform.offsetMax = new Vector2(-16f, 52f);
        }

        private static Sprite CreateEdgeVignetteSprite()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx * 0.72f + ny * ny * 1.12f);
                    float a = Mathf.Clamp01((r - 0.56f) / 0.62f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
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

        private void BuildLoadoutStrip()
        {
            var bar = NotaryUiTheme.CreateBorderedPanel(canvas.transform, "LoadoutStrip", NotaryUiTheme.PanelBgSoft, NotaryUiTheme.GoldSoft, 1.2f);
            var rt = bar.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(20f, -114f);
            rt.sizeDelta = new Vector2(420f, 36f);

            skillSlotBorders = new Image[5];
            skillSlotFills = new Image[5];
            skillSlotLabels = new Text[5];
            skillSlotPips = new Text[5];

            for (int i = 0; i < 5; i++)
            {
                var slot = NotaryUiTheme.CreateBorderedPanel(bar.transform, "SkillSlot_" + i, NotaryUiTheme.CardBg, NotaryUiTheme.GoldSoft, 1f);
                slot.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                slot.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                slot.rectTransform.pivot = new Vector2(0f, 0.5f);
                slot.rectTransform.anchoredPosition = new Vector2(8f + i * 64f, 0f);
                slot.rectTransform.sizeDelta = new Vector2(58f, 26f);
                skillSlotBorders[i] = slot;
                skillSlotFills[i] = slot.transform.Find("Inner").GetComponent<Image>();

                skillSlotLabels[i] = NotaryUiTheme.CreateText(slot.transform, "Lbl", "", 11, NotaryUiTheme.Ivory, TextAnchor.MiddleLeft, false, false);
                skillSlotLabels[i].rectTransform.anchorMin = new Vector2(0f, 0f);
                skillSlotLabels[i].rectTransform.anchorMax = new Vector2(0.72f, 1f);
                skillSlotLabels[i].rectTransform.offsetMin = new Vector2(5f, 0f);
                skillSlotLabels[i].rectTransform.offsetMax = Vector2.zero;

                skillSlotPips[i] = NotaryUiTheme.CreateText(slot.transform, "Pips", "", 9, NotaryUiTheme.GoldGlow, TextAnchor.MiddleRight, false, false);
                skillSlotPips[i].rectTransform.anchorMin = new Vector2(0.45f, 0f);
                skillSlotPips[i].rectTransform.anchorMax = new Vector2(1f, 1f);
                skillSlotPips[i].rectTransform.offsetMin = Vector2.zero;
                skillSlotPips[i].rectTransform.offsetMax = new Vector2(-4f, 0f);
            }

            var dashTrack = NotaryUiTheme.CreateBorderedPanel(bar.transform, "DashTrack", NotaryUiTheme.BarTrack, NotaryUiTheme.GoldSoft, 1f);
            dashTrack.rectTransform.anchorMin = new Vector2(1f, 0.5f);
            dashTrack.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            dashTrack.rectTransform.pivot = new Vector2(1f, 0.5f);
            dashTrack.rectTransform.anchoredPosition = new Vector2(-24f, 0f);
            dashTrack.rectTransform.sizeDelta = new Vector2(62f, 12f);

            dashCooldownFill = NotaryUiTheme.CreateImage(dashTrack.transform, "DashFill", NotaryUiTheme.GoldGlow);
            NotaryUiTheme.Stretch(dashCooldownFill.rectTransform, 1f, 1f, 1f, 1f);

            airDashPip = NotaryUiTheme.CreateImage(bar.transform, "AirDashPip", NotaryUiTheme.TealAccent);
            airDashPip.rectTransform.anchorMin = new Vector2(1f, 0.5f);
            airDashPip.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            airDashPip.rectTransform.pivot = new Vector2(1f, 0.5f);
            airDashPip.rectTransform.anchoredPosition = new Vector2(-8f, 0f);
            airDashPip.rectTransform.sizeDelta = new Vector2(10f, 10f);
        }

        private void BuildCheckpointToast()
        {
            var go = NotaryUiTheme.CreateBorderedPanel(canvas.transform, "CheckpointToast", NotaryUiTheme.PanelBg, NotaryUiTheme.Gold, 1.5f);
            go.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            go.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            go.rectTransform.pivot = new Vector2(0.5f, 0f);
            go.rectTransform.anchoredPosition = new Vector2(0f, 112f);
            go.rectTransform.sizeDelta = new Vector2(460f, 40f);
            toastGroup = go.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;

            toastText = NotaryUiTheme.CreateText(go.transform, "Text", "", 15, NotaryUiTheme.GoldGlow, TextAnchor.MiddleCenter, true, true);
            NotaryUiTheme.Stretch(toastText.rectTransform, 12f, 2f, 12f, 2f);
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
                CombatFloatingText2D.ClearAll();
                roomBannerText.text = s.LocationTitle ?? "";
                roomBannerAge = s.BossVisible ? 99f : 0f;
            }
            if (s.BossVisible)
            {
                roomBannerAge = 99f;
                roomBannerGroup.alpha = 0f;
            }
            else if (roomBannerAge < 2.6f)
            {
                roomBannerAge += Time.unscaledDeltaTime;
                float a = roomBannerAge < 0.35f ? roomBannerAge / 0.35f
                    : roomBannerAge > 2.0f ? Mathf.Clamp01((2.6f - roomBannerAge) / 0.6f) : 1f;
                roomBannerGroup.alpha = a;
            }
            else roomBannerGroup.alpha = 0f;

            if (toastAge < 2.8f)
            {
                toastAge += Time.unscaledDeltaTime;
                float a = toastAge < 0.25f ? toastAge / 0.25f
                    : toastAge > 2.1f ? Mathf.Clamp01((2.8f - toastAge) / 0.7f) : 1f;
                toastGroup.alpha = a;
            }
            else if (toastGroup != null) toastGroup.alpha = 0f;

            if (skillCardAge < 5.0f)
            {
                skillCardAge += Time.unscaledDeltaTime;
                float a = skillCardAge < 0.30f ? skillCardAge / 0.30f
                    : skillCardAge > 4.2f ? Mathf.Clamp01((5.0f - skillCardAge) / 0.8f) : 1f;
                skillCardGroup.alpha = a;
            }
            else if (skillCardGroup != null) skillCardGroup.alpha = 0f;

            if (lowHpVignette != null)
            {
                if (s.PlayerHp > 0 && s.PlayerHp <= 2)
                {
                    bool critical = s.PlayerHp == 1;
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (critical ? 8.2f : 5.4f));
                    float alpha = critical ? Mathf.Lerp(0.18f, 0.44f, pulse) : Mathf.Lerp(0.06f, 0.20f, pulse);
                    lowHpVignette.color = new Color(0.84f, 0.10f, 0.14f, alpha);
                    UiAudioSynth.PlayHeartbeat(critical);
                }
                else lowHpVignette.color = new Color(0.84f, 0.10f, 0.14f, 0f);
            }

            if (s.Skills != null && skillSlotBorders != null)
            {
                for (int i = 0; i < skillSlotBorders.Length && i < s.Skills.Length; i++)
                {
                    var sk = s.Skills[i];
                    skillSlotBorders[i].color = sk.Owned ? NotaryUiTheme.Gold : new Color(0.28f, 0.32f, 0.40f, 0.55f);
                    skillSlotFills[i].color = sk.Owned
                        ? new Color(sk.Accent.r * 0.38f, sk.Accent.g * 0.38f, sk.Accent.b * 0.38f, 0.94f)
                        : new Color(0.07f, 0.085f, 0.125f, 0.85f);
                    skillSlotLabels[i].color = sk.Owned ? NotaryUiTheme.Ivory : new Color(0.45f, 0.47f, 0.52f, 0.75f);
                    skillSlotLabels[i].text = sk.DirCue + sk.ShortLabel;
                    skillSlotPips[i].text = sk.Owned ? new string('•', Mathf.Clamp(sk.Stage, 1, 3)) : "";
                }
            }
            if (dashCooldownFill != null)
            {
                dashCooldownFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(s.DashReadyRatio), 1f);
                dashCooldownFill.color = s.DashReadyRatio >= 0.99f ? NotaryUiTheme.GoldGlow : NotaryUiTheme.GoldSoft;
            }
            if (airDashPip != null)
                airDashPip.color = s.AirDashReady ? NotaryUiTheme.TealAccent : new Color(0.24f, 0.28f, 0.34f, 0.55f);

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
            if (toastText != null) toastText.font = title;
            if (skillCardTitle != null) skillCardTitle.font = title;
            if (skillCardHeader != null) skillCardHeader.font = body;
            if (skillCardInput != null) skillCardInput.font = body;
            if (skillCardTip != null) skillCardTip.font = body;
            playerHpText.font = body;
            if (skillSlotLabels != null)
                for (int i = 0; i < skillSlotLabels.Length; i++)
                    if (skillSlotLabels[i] != null) skillSlotLabels[i].font = body;
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
