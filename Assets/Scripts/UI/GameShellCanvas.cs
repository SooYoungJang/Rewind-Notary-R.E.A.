using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RewindNotary
{
    public sealed class GameShellCanvas : MonoBehaviour
    {
        public struct ButtonItem
        {
            public int Index;
            public string Label;
            public bool Selected;
            public bool IsSetting;
            public bool HasDelete;
            public int DeleteSlot;
        }

        public struct SkillCardItem
        {
            public string Name;
            public bool Owned;
            public string StageText;
            public string ProgressText;
            public float ProgressRatio;
            public string FormText;
            public string TrialText;
        }

        public struct DialogueView
        {
            public string RoomLabel;
            public string SpeakerId;
            public string SpeakerName;
            public string BodyText;
            public bool CanSkip;
            public int SelectedIndex;
            public string ContinueLabel;
            public string SkipLabel;
            public string ErrorText;
            public bool InstantReveal;
        }

        public struct ShellSnapshot
        {
            public bool Visible;
            public ShellPage Page;
            public bool OverLiveWorld;
            public string GameTitle;
            public string Heading;
            public string BodyHeader;
            public string BodyText;
            public float ScrollNormalized;
            public string StatusText;
            public string FooterText;
            public List<ButtonItem> Buttons;
            public List<SkillCardItem> SkillCards;
            public DialogueView Dialogue;
        }

        public Action<int> OnActivateRow;
        public Action<int, int> OnAdjustSetting;
        public Action<int> OnDeleteSlot;

        private Canvas canvas;
        private GameObject mainView;
        private RectTransform keyartRect;
        private Text mainTitleText, mainSubtitleText, mainStatusText, mainFooterText;
        private Transform mainButtonsRoot;

        private GameObject dialogueView;
        private Image dialoguePortrait;
        private GameObject dialogueCrestFallback;
        private Image dialogueCrestBg;
        private Text dialogueCrestInitial;
        private Text dialogueRoomText, dialogueSpeakerText, dialogueBodyText, dialogueErrorText;
        private Transform dialogueButtonsRoot;
        private string lastDialogueKey;
        private string lastDialogueSpeakerId;
        private float dialogueRevealChars;

        private GameObject panelView;
        private Image panelBackdrop;
        private Image panelKeyartTint;
        private Text panelTitleText, panelHeadingText, panelBodyHeaderText, panelBodyText, panelStatusText, panelFooterText;
        private GameObject panelBodyScrollObj;
        private ScrollRect panelBodyScroll;
        private RectTransform panelContentRoot;
        private RectTransform panelButtonsContainer;
        private GridLayoutGroup panelStageGrid;
        private GridLayoutGroup panelSkillGrid;

        private string lastSignature;
        private string lastLanguage;

        public void Initialize(Transform parent)
        {
            NotaryUiTheme.EnsureEventSystem(parent);
            canvas = NotaryUiTheme.CreateCanvas(parent, "Game Shell Canvas", 100);
            BuildMainView();
            BuildDialogueView();
            BuildPanelView();
            lastLanguage = Loc.Language;
        }

        private void BuildMainView()
        {
            mainView = new GameObject("MainTitleView", typeof(RectTransform));
            mainView.transform.SetParent(canvas.transform, false);
            NotaryUiTheme.Stretch((RectTransform)mainView.transform);

            var bgFill = NotaryUiTheme.CreateImage(mainView.transform, "BgFill", new Color(0.03f, 0.04f, 0.075f, 1f));
            NotaryUiTheme.Stretch(bgFill.rectTransform);

            var keyart = NotaryUiTheme.CreateImage(mainView.transform, "Keyart", Color.white, NotaryUiTheme.LoadTitleKeyart());
            keyartRect = keyart.rectTransform;
            NotaryUiTheme.Stretch(keyartRect);
            keyart.preserveAspect = false;

            var scrim = NotaryUiTheme.CreateImage(mainView.transform, "LeftScrim", Color.white, NotaryUiTheme.LeftScrimSprite);
            NotaryUiTheme.Stretch(scrim.rectTransform);

            var titlePanel = NotaryUiTheme.CreateRect(mainView.transform, "TitleHeader");
            titlePanel.anchorMin = new Vector2(0f, 1f);
            titlePanel.anchorMax = new Vector2(0f, 1f);
            titlePanel.pivot = new Vector2(0f, 1f);
            titlePanel.anchoredPosition = new Vector2(72f, -68f);
            titlePanel.sizeDelta = new Vector2(640f, 130f);

            mainTitleText = NotaryUiTheme.CreateText(titlePanel, "Title", "", 40, NotaryUiTheme.GoldGlow, TextAnchor.MiddleLeft, true, true);
            mainTitleText.rectTransform.anchorMin = new Vector2(0f, 0.42f);
            mainTitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            mainTitleText.resizeTextForBestFit = true;
            mainTitleText.resizeTextMinSize = 24;
            mainTitleText.resizeTextMaxSize = 40;

            var rule = NotaryUiTheme.CreateImage(titlePanel, "GoldRule", NotaryUiTheme.Gold);
            rule.rectTransform.anchorMin = new Vector2(0f, 0.36f);
            rule.rectTransform.anchorMax = new Vector2(0.76f, 0.36f);
            rule.rectTransform.sizeDelta = new Vector2(0f, 2f);

            mainSubtitleText = NotaryUiTheme.CreateText(titlePanel, "Subtitle", "", 16, NotaryUiTheme.ParchmentMuted, TextAnchor.MiddleLeft, false, true);
            mainSubtitleText.rectTransform.anchorMin = new Vector2(0f, 0f);
            mainSubtitleText.rectTransform.anchorMax = new Vector2(1f, 0.32f);

            var btnRoot = NotaryUiTheme.CreateRect(mainView.transform, "MenuButtons");
            btnRoot.anchorMin = new Vector2(0f, 1f);
            btnRoot.anchorMax = new Vector2(0f, 1f);
            btnRoot.pivot = new Vector2(0f, 1f);
            btnRoot.anchoredPosition = new Vector2(72f, -228f);
            btnRoot.sizeDelta = new Vector2(400f, 340f);
            var vlg = btnRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            mainButtonsRoot = btnRoot;

            mainStatusText = NotaryUiTheme.CreateText(mainView.transform, "Status", "", 15, NotaryUiTheme.TealAccent, TextAnchor.MiddleLeft, false, true);
            mainStatusText.rectTransform.anchorMin = new Vector2(0f, 0f);
            mainStatusText.rectTransform.anchorMax = new Vector2(0.75f, 0f);
            mainStatusText.rectTransform.pivot = new Vector2(0f, 0f);
            mainStatusText.rectTransform.anchoredPosition = new Vector2(72f, 52f);
            mainStatusText.rectTransform.sizeDelta = new Vector2(0f, 34f);

            mainFooterText = NotaryUiTheme.CreateText(mainView.transform, "Footer", "", 13, NotaryUiTheme.ParchmentMuted, TextAnchor.MiddleLeft, false, true);
            mainFooterText.rectTransform.anchorMin = new Vector2(0f, 0f);
            mainFooterText.rectTransform.anchorMax = new Vector2(1f, 0f);
            mainFooterText.rectTransform.pivot = new Vector2(0f, 0f);
            mainFooterText.rectTransform.anchoredPosition = new Vector2(72f, 18f);
            mainFooterText.rectTransform.sizeDelta = new Vector2(-144f, 26f);
        }

        private void BuildDialogueView()
        {
            dialogueView = new GameObject("StoryDialogueView", typeof(RectTransform));
            dialogueView.transform.SetParent(canvas.transform, false);
            NotaryUiTheme.Stretch((RectTransform)dialogueView.transform);

            var scrim = NotaryUiTheme.CreateImage(dialogueView.transform, "BottomScrim", Color.white, NotaryUiTheme.BottomScrimSprite);
            scrim.rectTransform.anchorMin = new Vector2(0f, 0f);
            scrim.rectTransform.anchorMax = new Vector2(1f, 0.48f);
            scrim.rectTransform.offsetMin = Vector2.zero;
            scrim.rectTransform.offsetMax = Vector2.zero;

            var roomTag = NotaryUiTheme.CreateBorderedPanel(dialogueView.transform, "RoomTag", NotaryUiTheme.PanelBgSoft, NotaryUiTheme.GoldSoft, 1.2f);
            roomTag.rectTransform.anchorMin = new Vector2(0f, 1f);
            roomTag.rectTransform.anchorMax = new Vector2(0f, 1f);
            roomTag.rectTransform.pivot = new Vector2(0f, 1f);
            roomTag.rectTransform.anchoredPosition = new Vector2(28f, -20f);
            roomTag.rectTransform.sizeDelta = new Vector2(440f, 36f);
            dialogueRoomText = NotaryUiTheme.CreateText(roomTag.transform, "Label", "", 15, NotaryUiTheme.GoldGlow, TextAnchor.MiddleLeft, true, true);
            NotaryUiTheme.Stretch(dialogueRoomText.rectTransform, 14f, 2f, 14f, 2f);

            var box = NotaryUiTheme.CreateBorderedPanel(dialogueView.transform, "DialogueBox", NotaryUiTheme.PanelBg, NotaryUiTheme.Gold, 2f);
            box.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            box.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            box.rectTransform.pivot = new Vector2(0.5f, 0f);
            box.rectTransform.anchoredPosition = new Vector2(0f, 28f);
            box.rectTransform.sizeDelta = new Vector2(1152f, 232f);

            var innerRule = NotaryUiTheme.CreateImage(box.transform, "InnerTopRule", NotaryUiTheme.GoldSoft);
            innerRule.rectTransform.anchorMin = new Vector2(0f, 1f);
            innerRule.rectTransform.anchorMax = new Vector2(1f, 1f);
            innerRule.rectTransform.offsetMin = new Vector2(218f, -10f);
            innerRule.rectTransform.offsetMax = new Vector2(-20f, -8.5f);

            var portraitOuter = NotaryUiTheme.CreateBorderedPanel(box.transform, "PortraitFrame", NotaryUiTheme.CardBg, NotaryUiTheme.Gold, 2f);
            portraitOuter.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            portraitOuter.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            portraitOuter.rectTransform.pivot = new Vector2(0f, 0.5f);
            portraitOuter.rectTransform.anchoredPosition = new Vector2(22f, 0f);
            portraitOuter.rectTransform.sizeDelta = new Vector2(184f, 184f);

            dialoguePortrait = NotaryUiTheme.CreateImage(portraitOuter.transform, "Portrait", Color.white);
            NotaryUiTheme.Stretch(dialoguePortrait.rectTransform, 3f, 3f, 3f, 3f);
            dialoguePortrait.preserveAspect = true;

            var crest = NotaryUiTheme.CreateBorderedPanel(portraitOuter.transform, "CrestFallback", NotaryUiTheme.CardBg, NotaryUiTheme.GoldSoft, 1.5f);
            dialogueCrestFallback = crest.gameObject;
            NotaryUiTheme.Stretch(crest.rectTransform, 6f, 6f, 6f, 6f);
            dialogueCrestBg = NotaryUiTheme.CreateImage(crest.transform, "CrestFill", NotaryUiTheme.CardSelectedBg);
            NotaryUiTheme.Stretch(dialogueCrestBg.rectTransform, 10f, 10f, 10f, 10f);
            dialogueCrestInitial = NotaryUiTheme.CreateText(dialogueCrestBg.transform, "Initial", "", 54, NotaryUiTheme.GoldGlow, TextAnchor.MiddleCenter, true, true);
            NotaryUiTheme.Stretch(dialogueCrestInitial.rectTransform);

            var nameplate = NotaryUiTheme.CreateBorderedPanel(box.transform, "SpeakerPlate", NotaryUiTheme.CardSelectedBg, NotaryUiTheme.Gold, 1.5f);
            nameplate.rectTransform.anchorMin = new Vector2(0f, 1f);
            nameplate.rectTransform.anchorMax = new Vector2(0f, 1f);
            nameplate.rectTransform.pivot = new Vector2(0f, 0.5f);
            nameplate.rectTransform.anchoredPosition = new Vector2(222f, 2f);
            nameplate.rectTransform.sizeDelta = new Vector2(320f, 38f);
            dialogueSpeakerText = NotaryUiTheme.CreateText(nameplate.transform, "SpeakerName", "", 19, NotaryUiTheme.GoldGlow, TextAnchor.MiddleLeft, true, true);
            NotaryUiTheme.Stretch(dialogueSpeakerText.rectTransform, 16f, 2f, 16f, 2f);

            dialogueBodyText = NotaryUiTheme.CreateText(box.transform, "BodyText", "", 19, NotaryUiTheme.Ivory, TextAnchor.UpperLeft, false, true);
            dialogueBodyText.rectTransform.anchorMin = new Vector2(0f, 0f);
            dialogueBodyText.rectTransform.anchorMax = new Vector2(1f, 1f);
            dialogueBodyText.rectTransform.offsetMin = new Vector2(226f, 60f);
            dialogueBodyText.rectTransform.offsetMax = new Vector2(-28f, -30f);
            dialogueBodyText.lineSpacing = 1.25f;
            dialogueBodyText.resizeTextForBestFit = true;
            dialogueBodyText.resizeTextMinSize = 13;
            dialogueBodyText.resizeTextMaxSize = 19;

            dialogueErrorText = NotaryUiTheme.CreateText(box.transform, "Error", "", 13, NotaryUiTheme.BossFill, TextAnchor.MiddleLeft, false, true);
            dialogueErrorText.rectTransform.anchorMin = new Vector2(0f, 0f);
            dialogueErrorText.rectTransform.anchorMax = new Vector2(0.55f, 0f);
            dialogueErrorText.rectTransform.offsetMin = new Vector2(226f, 14f);
            dialogueErrorText.rectTransform.offsetMax = new Vector2(0f, 44f);

            var btnBar = NotaryUiTheme.CreateRect(box.transform, "DialogueButtons");
            btnBar.anchorMin = new Vector2(1f, 0f);
            btnBar.anchorMax = new Vector2(1f, 0f);
            btnBar.pivot = new Vector2(1f, 0f);
            btnBar.anchoredPosition = new Vector2(-24f, 14f);
            btnBar.sizeDelta = new Vector2(460f, 40f);
            var hlg = btnBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            dialogueButtonsRoot = btnBar;
        }

        private void BuildPanelView()
        {
            panelView = new GameObject("PanelMenuView", typeof(RectTransform));
            panelView.transform.SetParent(canvas.transform, false);
            NotaryUiTheme.Stretch((RectTransform)panelView.transform);

            panelKeyartTint = NotaryUiTheme.CreateImage(panelView.transform, "KeyartWatermark", new Color(0.25f, 0.28f, 0.36f, 1f), NotaryUiTheme.LoadTitleKeyart());
            NotaryUiTheme.Stretch(panelKeyartTint.rectTransform);

            panelBackdrop = NotaryUiTheme.CreateImage(panelView.transform, "Backdrop", new Color(0.032f, 0.044f, 0.078f, 0.86f));
            NotaryUiTheme.Stretch(panelBackdrop.rectTransform);

            var frame = NotaryUiTheme.CreateBorderedPanel(panelView.transform, "MainPanel", NotaryUiTheme.PanelBg, NotaryUiTheme.Gold, 2f);
            frame.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            frame.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            frame.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            frame.rectTransform.anchoredPosition = Vector2.zero;
            frame.rectTransform.sizeDelta = new Vector2(1140f, 650f);

            panelTitleText = NotaryUiTheme.CreateText(frame.transform, "GameTitle", "", 15, NotaryUiTheme.GoldSoft, TextAnchor.MiddleCenter, true, true);
            panelTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            panelTitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            panelTitleText.rectTransform.offsetMin = new Vector2(24f, -34f);
            panelTitleText.rectTransform.offsetMax = new Vector2(-24f, -10f);

            panelHeadingText = NotaryUiTheme.CreateText(frame.transform, "Heading", "", 28, NotaryUiTheme.GoldGlow, TextAnchor.MiddleCenter, true, true);
            panelHeadingText.rectTransform.anchorMin = new Vector2(0f, 1f);
            panelHeadingText.rectTransform.anchorMax = new Vector2(1f, 1f);
            panelHeadingText.rectTransform.offsetMin = new Vector2(28f, -76f);
            panelHeadingText.rectTransform.offsetMax = new Vector2(-28f, -34f);

            var divider = NotaryUiTheme.CreateImage(frame.transform, "Divider", NotaryUiTheme.GoldSoft);
            divider.rectTransform.anchorMin = new Vector2(0.06f, 1f);
            divider.rectTransform.anchorMax = new Vector2(0.94f, 1f);
            divider.rectTransform.anchoredPosition = new Vector2(0f, -84f);
            divider.rectTransform.sizeDelta = new Vector2(0f, 1.5f);

            panelBodyHeaderText = NotaryUiTheme.CreateText(frame.transform, "BodyHeader", "", 15, NotaryUiTheme.TealAccent, TextAnchor.MiddleLeft, false, true);
            panelBodyHeaderText.rectTransform.anchorMin = new Vector2(0f, 1f);
            panelBodyHeaderText.rectTransform.anchorMax = new Vector2(1f, 1f);
            panelBodyHeaderText.rectTransform.offsetMin = new Vector2(44f, -118f);
            panelBodyHeaderText.rectTransform.offsetMax = new Vector2(-44f, -92f);

            var scrollOuter = NotaryUiTheme.CreateBorderedPanel(frame.transform, "BodyScrollBox", NotaryUiTheme.CardBg, NotaryUiTheme.GoldSoft, 1f);
            panelBodyScrollObj = scrollOuter.gameObject;
            scrollOuter.rectTransform.anchorMin = new Vector2(0f, 1f);
            scrollOuter.rectTransform.anchorMax = new Vector2(1f, 1f);
            scrollOuter.rectTransform.offsetMin = new Vector2(44f, -365f);
            scrollOuter.rectTransform.offsetMax = new Vector2(-44f, -122f);

            var viewport = NotaryUiTheme.CreateRect(scrollOuter.transform, "Viewport");
            NotaryUiTheme.Stretch(viewport, 12f, 10f, 12f, 10f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = NotaryUiTheme.CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 320f);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            panelBodyText = NotaryUiTheme.CreateText(content, "BodyText", "", 16, NotaryUiTheme.Ivory, TextAnchor.UpperLeft, false, true);
            NotaryUiTheme.Stretch(panelBodyText.rectTransform);
            panelBodyText.verticalOverflow = VerticalWrapMode.Overflow;
            panelBodyText.lineSpacing = 1.24f;

            panelBodyScroll = scrollOuter.gameObject.AddComponent<ScrollRect>();
            panelBodyScroll.viewport = viewport;
            panelBodyScroll.content = content;
            panelBodyScroll.horizontal = false;
            panelBodyScroll.vertical = true;
            panelBodyScroll.scrollSensitivity = 28f;

            panelContentRoot = NotaryUiTheme.CreateRect(frame.transform, "ContentRoot");
            panelContentRoot.anchorMin = new Vector2(0f, 0f);
            panelContentRoot.anchorMax = new Vector2(1f, 1f);
            panelContentRoot.offsetMin = new Vector2(44f, 76f);
            panelContentRoot.offsetMax = new Vector2(-44f, -96f);

            panelButtonsContainer = NotaryUiTheme.CreateRect(panelContentRoot, "ButtonsContainer");
            NotaryUiTheme.Stretch(panelButtonsContainer);

            panelStatusText = NotaryUiTheme.CreateText(frame.transform, "StatusText", "", 14, NotaryUiTheme.TealAccent, TextAnchor.MiddleCenter, false, true);
            panelStatusText.rectTransform.anchorMin = new Vector2(0f, 0f);
            panelStatusText.rectTransform.anchorMax = new Vector2(1f, 0f);
            panelStatusText.rectTransform.offsetMin = new Vector2(40f, 40f);
            panelStatusText.rectTransform.offsetMax = new Vector2(-40f, 72f);

            panelFooterText = NotaryUiTheme.CreateText(frame.transform, "FooterText", "", 13, NotaryUiTheme.ParchmentMuted, TextAnchor.MiddleCenter, false, false);
            panelFooterText.rectTransform.anchorMin = new Vector2(0f, 0f);
            panelFooterText.rectTransform.anchorMax = new Vector2(1f, 0f);
            panelFooterText.rectTransform.offsetMin = new Vector2(40f, 12f);
            panelFooterText.rectTransform.offsetMax = new Vector2(-40f, 38f);
        }

        private void Update()
        {
            if (mainView != null && mainView.activeSelf && keyartRect != null)
            {
                float s = 1.015f + Mathf.Sin(Time.unscaledTime * 0.35f) * 0.015f;
                keyartRect.localScale = new Vector3(s, s, 1f);
            }
            if (dialogueView != null && dialogueView.activeSelf && !string.IsNullOrEmpty(lastDialogueBody))
            {
                if (dialogueRevealChars < lastDialogueBody.Length)
                {
                    int prev = Mathf.FloorToInt(dialogueRevealChars);
                    dialogueRevealChars = Mathf.Min(lastDialogueBody.Length, dialogueRevealChars + Time.unscaledDeltaTime * 58f);
                    int next = Mathf.FloorToInt(dialogueRevealChars);
                    if (next > prev)
                    {
                        dialogueBodyText.text = lastDialogueBody.Substring(0, next);
                        char ch = lastDialogueBody[Mathf.Clamp(next - 1, 0, lastDialogueBody.Length - 1)];
                        if (!char.IsWhiteSpace(ch)) UiAudioSynth.PlayVoiceBlip(lastDialogueSpeakerId);
                    }
                }
            }
        }

        private string lastDialogueBody = "";

        public void Render(ShellSnapshot snap)
        {
            if (canvas == null) return;
            canvas.gameObject.SetActive(snap.Visible);
            if (!snap.Visible) return;

            bool langChanged = lastLanguage != Loc.Language;
            if (langChanged)
            {
                lastLanguage = Loc.Language;
                RefreshFonts();
                lastSignature = null;
            }

            bool isMain = snap.Page == ShellPage.Main;
            bool isDialogue = snap.Page == ShellPage.StoryDialogue;
            mainView.SetActive(isMain);
            dialogueView.SetActive(isDialogue);
            panelView.SetActive(!isMain && !isDialogue);

            string sig = BuildSignature(snap);
            if (sig == lastSignature) return;
            lastSignature = sig;

            if (isMain) RenderMain(snap);
            else if (isDialogue) RenderDialogue(snap.Dialogue);
            else RenderPanel(snap);
        }

        private void RenderMain(ShellSnapshot snap)
        {
            mainTitleText.text = snap.GameTitle ?? "";
            mainSubtitleText.text = snap.Heading ?? "";
            mainStatusText.text = snap.StatusText ?? "";
            mainFooterText.text = snap.FooterText ?? "";

            ClearChildren(mainButtonsRoot);
            if (snap.Buttons == null) return;
            for (int i = 0; i < snap.Buttons.Count; i++)
            {
                var item = snap.Buttons[i];
                int capturedIndex = item.Index;
                var btn = NotaryUiTheme.CreateStyledButton(mainButtonsRoot, "MainBtn_" + i, item.Label, 21, item.Selected,
                    () => OnActivateRow?.Invoke(capturedIndex), out _, out _, out _);
                var le = btn.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 52f;
            }
        }

        private void RenderDialogue(DialogueView d)
        {
            dialogueRoomText.text = d.RoomLabel ?? "";
            dialogueSpeakerText.text = d.SpeakerName ?? "";
            dialogueErrorText.text = d.ErrorText ?? "";

            var portrait = NotaryUiTheme.LoadPortrait(d.SpeakerId);
            if (portrait != null)
            {
                dialoguePortrait.enabled = true;
                dialoguePortrait.sprite = portrait;
                dialogueCrestFallback.SetActive(false);
            }
            else
            {
                dialoguePortrait.enabled = false;
                dialogueCrestFallback.SetActive(true);
                dialogueCrestBg.color = NotaryUiTheme.SpeakerCrestColor(d.SpeakerId);
                string initial = !string.IsNullOrEmpty(d.SpeakerName) ? d.SpeakerName.Substring(0, 1) : "◆";
                dialogueCrestInitial.text = initial;
            }

            string key = (d.SpeakerId ?? "") + "|" + (d.BodyText ?? "");
            if (key != lastDialogueKey)
            {
                lastDialogueKey = key;
                lastDialogueSpeakerId = d.SpeakerId ?? "";
                lastDialogueBody = d.BodyText ?? "";
                dialogueRevealChars = d.InstantReveal ? lastDialogueBody.Length : 0f;
                dialogueBodyText.text = d.InstantReveal ? lastDialogueBody : "";
            }

            ClearChildren(dialogueButtonsRoot);
            NotaryUiTheme.CreateStyledButton(dialogueButtonsRoot, "ContinueBtn", d.ContinueLabel ?? "", 16, d.SelectedIndex == 0,
                () => OnActivateRow?.Invoke(0), out _, out _, out _);
            if (d.CanSkip)
            {
                NotaryUiTheme.CreateStyledButton(dialogueButtonsRoot, "SkipBtn", d.SkipLabel ?? "", 16, d.SelectedIndex == 1,
                    () => OnActivateRow?.Invoke(1), out _, out _, out _);
            }
        }

        private void RenderPanel(ShellSnapshot snap)
        {
            panelKeyartTint.enabled = !snap.OverLiveWorld;
            panelBackdrop.color = snap.OverLiveWorld
                ? new Color(0.025f, 0.036f, 0.068f, 0.78f)
                : new Color(0.032f, 0.044f, 0.078f, 0.88f);

            panelTitleText.text = snap.GameTitle ?? "";
            panelHeadingText.text = snap.Heading ?? "";
            panelStatusText.text = snap.StatusText ?? "";
            panelFooterText.text = snap.FooterText ?? "";

            bool hasHeader = !string.IsNullOrEmpty(snap.BodyHeader);
            panelBodyHeaderText.gameObject.SetActive(hasHeader);
            if (hasHeader) panelBodyHeaderText.text = snap.BodyHeader;

            bool hasBody = !string.IsNullOrEmpty(snap.BodyText);
            panelBodyScrollObj.SetActive(hasBody);
            if (hasBody)
            {
                panelBodyText.text = snap.BodyText;
                panelBodyScroll.verticalNormalizedPosition = Mathf.Clamp01(1f - snap.ScrollNormalized);
            }

            ClearChildren(panelButtonsContainer);
            RemoveLayoutGroups(panelButtonsContainer.gameObject);

            if (snap.Page == ShellPage.Skills && snap.SkillCards != null)
            {
                RenderSkillsGrid(snap);
                return;
            }

            float topOffset = hasBody ? 280f : hasHeader ? 34f : 8f;
            panelButtonsContainer.offsetMin = new Vector2(0f, 0f);
            panelButtonsContainer.offsetMax = new Vector2(0f, -topOffset);

            if (snap.Page == ShellPage.Stages)
            {
                var grid = panelButtonsContainer.gameObject.AddComponent<GridLayoutGroup>();
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 2;
                grid.cellSize = new Vector2(516f, 44f);
                grid.spacing = new Vector2(18f, 8f);
                grid.childAlignment = TextAnchor.UpperCenter;
                SpawnButtons(snap.Buttons, 14, 44f, false);
                return;
            }

            var vlg = panelButtonsContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = snap.Buttons != null && snap.Buttons.Count > 9 ? 4f : snap.Buttons != null && snap.Buttons.Count > 7 ? 6f : 10f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(snap.Page == ShellPage.Slots ? 24 : 140, snap.Page == ShellPage.Slots ? 24 : 140, 6, 6);

            float rowHeight = snap.Page == ShellPage.Slots ? 88f
                : snap.Buttons != null && snap.Buttons.Count > 9 ? 32f
                : snap.Buttons != null && snap.Buttons.Count > 7 ? 38f
                : snap.Buttons != null && snap.Buttons.Count > 5 ? 46f : 54f;
            int fontSize = snap.Page == ShellPage.Slots ? 16 : rowHeight < 35f ? 14 : rowHeight < 42f ? 15 : 19;
            SpawnButtons(snap.Buttons, fontSize, rowHeight, snap.Page == ShellPage.Slots);
        }

        private void RenderSkillsGrid(ShellSnapshot snap)
        {
            panelButtonsContainer.offsetMin = Vector2.zero;
            panelButtonsContainer.offsetMax = new Vector2(0f, -34f);

            var gridObj = NotaryUiTheme.CreateRect(panelButtonsContainer, "SkillsGrid");
            gridObj.anchorMin = new Vector2(0f, 0f);
            gridObj.anchorMax = new Vector2(1f, 1f);
            gridObj.offsetMin = new Vector2(0f, 64f);
            gridObj.offsetMax = Vector2.zero;

            var grid = gridObj.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.cellSize = new Vector2(516f, 128f);
            grid.spacing = new Vector2(18f, 12f);
            grid.childAlignment = TextAnchor.UpperCenter;

            for (int i = 0; i < snap.SkillCards.Count; i++)
            {
                var c = snap.SkillCards[i];
                var card = NotaryUiTheme.CreateBorderedPanel(gridObj, "SkillCard_" + i,
                    c.Owned ? NotaryUiTheme.CardSelectedBg : NotaryUiTheme.CardBg,
                    c.Owned ? NotaryUiTheme.Gold : new Color(0.32f, 0.35f, 0.42f, 0.65f), 1.5f);

                var nameTxt = NotaryUiTheme.CreateText(card.transform, "Name", c.Name, 17,
                    c.Owned ? NotaryUiTheme.GoldGlow : NotaryUiTheme.ParchmentMuted, TextAnchor.MiddleLeft, true, true);
                nameTxt.rectTransform.anchorMin = new Vector2(0f, 1f);
                nameTxt.rectTransform.anchorMax = new Vector2(0.65f, 1f);
                nameTxt.rectTransform.offsetMin = new Vector2(14f, -30f);
                nameTxt.rectTransform.offsetMax = new Vector2(0f, -6f);

                var stageTxt = NotaryUiTheme.CreateText(card.transform, "Stage", c.StageText, 13,
                    c.Owned ? NotaryUiTheme.TealAccent : NotaryUiTheme.ParchmentMuted, TextAnchor.MiddleRight, false, true);
                stageTxt.rectTransform.anchorMin = new Vector2(0.55f, 1f);
                stageTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
                stageTxt.rectTransform.offsetMin = Vector2.zero;
                stageTxt.rectTransform.offsetMax = new Vector2(-14f, -6f);

                var track = NotaryUiTheme.CreateBorderedPanel(card.transform, "Track", NotaryUiTheme.BarTrack, NotaryUiTheme.GoldSoft, 1f);
                track.rectTransform.anchorMin = new Vector2(0f, 1f);
                track.rectTransform.anchorMax = new Vector2(1f, 1f);
                track.rectTransform.offsetMin = new Vector2(14f, -46f);
                track.rectTransform.offsetMax = new Vector2(-14f, -34f);

                var fill = NotaryUiTheme.CreateImage(track.transform, "Fill", c.Owned ? NotaryUiTheme.TealAccent : new Color(0.35f, 0.38f, 0.44f, 0.7f));
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(c.ProgressRatio), 1f);
                fill.rectTransform.offsetMin = new Vector2(1f, 1f);
                fill.rectTransform.offsetMax = new Vector2(-1f, -1f);

                var progTxt = NotaryUiTheme.CreateText(card.transform, "Prog", c.ProgressText, 13, NotaryUiTheme.Ivory, TextAnchor.MiddleLeft, false, false);
                progTxt.rectTransform.anchorMin = new Vector2(0f, 1f);
                progTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
                progTxt.rectTransform.offsetMin = new Vector2(14f, -70f);
                progTxt.rectTransform.offsetMax = new Vector2(-14f, -48f);

                var formTxt = NotaryUiTheme.CreateText(card.transform, "Form", c.FormText, 12, NotaryUiTheme.ParchmentMuted, TextAnchor.MiddleLeft, false, false);
                formTxt.rectTransform.anchorMin = new Vector2(0f, 1f);
                formTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
                formTxt.rectTransform.offsetMin = new Vector2(14f, -94f);
                formTxt.rectTransform.offsetMax = new Vector2(-14f, -72f);

                var trialTxt = NotaryUiTheme.CreateText(card.transform, "Trial", c.TrialText, 12, NotaryUiTheme.Gold, TextAnchor.MiddleLeft, false, false);
                trialTxt.rectTransform.anchorMin = new Vector2(0f, 0f);
                trialTxt.rectTransform.anchorMax = new Vector2(1f, 0f);
                trialTxt.rectTransform.offsetMin = new Vector2(14f, 6f);
                trialTxt.rectTransform.offsetMax = new Vector2(-14f, 28f);
            }

            var footerBtns = NotaryUiTheme.CreateRect(panelButtonsContainer, "SkillFooterBtns");
            footerBtns.anchorMin = new Vector2(0f, 0f);
            footerBtns.anchorMax = new Vector2(1f, 0f);
            footerBtns.pivot = new Vector2(0.5f, 0f);
            footerBtns.anchoredPosition = Vector2.zero;
            footerBtns.sizeDelta = new Vector2(0f, 48f);
            var hlg = footerBtns.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            if (snap.Buttons != null)
            {
                for (int i = 0; i < snap.Buttons.Count; i++)
                {
                    var item = snap.Buttons[i];
                    int idx = item.Index;
                    NotaryUiTheme.CreateStyledButton(footerBtns, "SkillFooterBtn_" + i, item.Label, 17, item.Selected,
                        () => OnActivateRow?.Invoke(idx), out _, out _, out _);
                }
            }
        }

        private void SpawnButtons(List<ButtonItem> buttons, int fontSize, float height, bool withDelete)
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Count; i++)
            {
                var item = buttons[i];
                int capturedIndex = item.Index;
                if (withDelete && item.HasDelete)
                {
                    var row = NotaryUiTheme.CreateRect(panelButtonsContainer, "SlotRow_" + i);
                    var le = row.gameObject.AddComponent<LayoutElement>();
                    le.preferredHeight = height;
                    var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                    hlg.spacing = 12f;
                    hlg.childControlWidth = true;
                    hlg.childControlHeight = true;
                    hlg.childForceExpandWidth = false;
                    hlg.childForceExpandHeight = true;

                    var mainBtn = NotaryUiTheme.CreateStyledButton(row, "SlotBtn_" + i, item.Label, fontSize, item.Selected,
                        () => OnActivateRow?.Invoke(capturedIndex), out _, out _, out _);
                    mainBtn.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

                    int capturedSlot = item.DeleteSlot;
                    var delBtn = NotaryUiTheme.CreateStyledButton(row, "DelBtn_" + i, Loc.Get("ui.delete"), 15, false,
                        () => OnDeleteSlot?.Invoke(capturedSlot), out var delLabel, out var delBg, out var delAcc);
                    delBg.color = new Color(0.28f, 0.10f, 0.12f, 0.92f);
                    delAcc.color = NotaryUiTheme.BossFill;
                    delLabel.alignment = TextAnchor.MiddleCenter;
                    delLabel.text = Loc.Get("ui.delete");
                    var dle = delBtn.gameObject.AddComponent<LayoutElement>();
                    dle.preferredWidth = 112f;
                }
                else
                {
                    bool isSetting = item.IsSetting;
                    var btn = NotaryUiTheme.CreateStyledButton(panelButtonsContainer, "Btn_" + i, item.Label, fontSize, item.Selected,
                        () =>
                        {
                            if (isSetting) OnAdjustSetting?.Invoke(capturedIndex, 1);
                            else OnActivateRow?.Invoke(capturedIndex);
                        }, out _, out _, out _);
                    var le = btn.gameObject.AddComponent<LayoutElement>();
                    le.preferredHeight = height;
                }
            }
        }

        private static string BuildSignature(ShellSnapshot s)
        {
            var sb = new System.Text.StringBuilder(256);
            sb.Append((int)s.Page).Append('|').Append(s.OverLiveWorld).Append('|')
              .Append(s.Heading).Append('|').Append(s.BodyHeader).Append('|')
              .Append(s.BodyText).Append('|').Append(s.StatusText).Append('|')
              .Append(s.FooterText).Append('|').Append(Mathf.RoundToInt(s.ScrollNormalized * 100));
            if (s.Page == ShellPage.StoryDialogue)
            {
                sb.Append('|').Append(s.Dialogue.RoomLabel).Append('|').Append(s.Dialogue.SpeakerId)
                  .Append('|').Append(s.Dialogue.BodyText).Append('|').Append(s.Dialogue.CanSkip)
                  .Append('|').Append(s.Dialogue.SelectedIndex).Append('|').Append(s.Dialogue.ErrorText);
            }
            if (s.Buttons != null)
            {
                for (int i = 0; i < s.Buttons.Count; i++)
                    sb.Append('|').Append(s.Buttons[i].Index).Append(':').Append(s.Buttons[i].Selected ? '1' : '0').Append(':').Append(s.Buttons[i].Label);
            }
            if (s.SkillCards != null)
            {
                for (int i = 0; i < s.SkillCards.Count; i++)
                    sb.Append('|').Append(s.SkillCards[i].Name).Append(':').Append(s.SkillCards[i].ProgressText).Append(':').Append(s.SkillCards[i].TrialText);
            }
            return sb.ToString();
        }

        private void RefreshFonts()
        {
            var body = NotaryUiTheme.BodyFont;
            var title = NotaryUiTheme.TitleFont;
            mainTitleText.font = title;
            mainSubtitleText.font = body;
            mainStatusText.font = body;
            mainFooterText.font = body;
            dialogueRoomText.font = title;
            dialogueSpeakerText.font = title;
            dialogueCrestInitial.font = title;
            dialogueBodyText.font = body;
            dialogueErrorText.font = body;
            panelTitleText.font = title;
            panelHeadingText.font = title;
            panelBodyHeaderText.font = body;
            panelBodyText.font = body;
            panelStatusText.font = body;
            panelFooterText.font = body;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        private static void RemoveLayoutGroups(GameObject go)
        {
            var vlg = go.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) DestroyImmediate(vlg);
            var grid = go.GetComponent<GridLayoutGroup>();
            if (grid != null) DestroyImmediate(grid);
        }

        private void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }
    }
}
