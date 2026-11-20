using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace RewindNotary
{
    [DefaultExecutionOrder(-245)]
    public sealed class NotaryJournalCanvas : MonoBehaviour
    {
        public static int ConsumedCancelFrame { get; private set; } = -1;
        public static bool IsOpen { get; private set; }

        private struct CodexEntry
        {
            public string Id;
            public string RoleKey;
            public string SkillId;
            public string FlagHint;
            public Color Accent;
        }

        private static readonly CodexEntry[] Characters =
        {
            new CodexEntry { Id = "rea", RoleKey = "journal.role_rea", SkillId = SkillRules.BasicSword, FlagHint = "hub_open", Accent = new Color(0.84f, 0.72f, 0.44f, 1f) },
            new CodexEntry { Id = "marta", RoleKey = "journal.role_marta", SkillId = null, FlagHint = "intro_attic_01_read", Accent = new Color(0.68f, 0.56f, 0.42f, 1f) },
            new CodexEntry { Id = "mikhail", RoleKey = "journal.role_mikhail", SkillId = "silver_stitch", FlagHint = "mikhail", Accent = new Color(0.76f, 0.28f, 0.34f, 1f) },
            new CodexEntry { Id = "julius", RoleKey = "journal.role_julius", SkillId = "pendulum_rewind", FlagHint = "julius", Accent = new Color(0.84f, 0.66f, 0.30f, 1f) },
            new CodexEntry { Id = "valeria", RoleKey = "journal.role_valeria", SkillId = "curtain_call_waltz", FlagHint = "valeria", Accent = new Color(0.42f, 0.62f, 0.90f, 1f) },
            new CodexEntry { Id = "gabriel", RoleKey = "journal.role_gabriel", SkillId = "resonance_forge", FlagHint = "gabriel", Accent = new Color(0.44f, 0.78f, 0.64f, 1f) },
            new CodexEntry { Id = "irina", RoleKey = "journal.role_irina", SkillId = "canvas_carve", FlagHint = "irina", Accent = new Color(0.78f, 0.56f, 0.88f, 1f) },
            new CodexEntry { Id = "cassel", RoleKey = "journal.role_cassel", SkillId = null, FlagHint = "cassel", Accent = new Color(0.88f, 0.84f, 0.72f, 1f) }
        };

        private Canvas canvas;
        private GameObject rootPanel;
        private Text titleText, footerText;
        private Image[] tabBorders, tabFills;
        private Text[] tabLabels;
        private RectTransform contentRoot;
        private int activeTab;
        private bool open;
        private bool dirty = true;
        private string lastLang;
        private SkillSession cachedSession;
        private RepresentativeArea cachedArea;
        private int cachedVisited;

        public void Initialize(Transform parent)
        {
            canvas = NotaryUiTheme.CreateCanvas(parent, "Notary Journal Canvas", 112);
            var backdrop = NotaryUiTheme.CreateImage(canvas.transform, "Backdrop", new Color(0.02f, 0.03f, 0.06f, 0.82f));
            NotaryUiTheme.Stretch(backdrop.rectTransform);
            rootPanel = backdrop.gameObject;

            var frame = NotaryUiTheme.CreateBorderedPanel(backdrop.transform, "JournalFrame", NotaryUiTheme.PanelBg, NotaryUiTheme.Gold, 2f);
            frame.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            frame.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            frame.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            frame.rectTransform.sizeDelta = new Vector2(1080f, 620f);

            titleText = NotaryUiTheme.CreateText(frame.transform, "Title", "", 24, NotaryUiTheme.GoldGlow, TextAnchor.MiddleLeft, true, true);
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(0.55f, 1f);
            titleText.rectTransform.offsetMin = new Vector2(28f, -54f);
            titleText.rectTransform.offsetMax = new Vector2(0f, -12f);

            tabBorders = new Image[3];
            tabFills = new Image[3];
            tabLabels = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                int tabIndex = i;
                var tab = NotaryUiTheme.CreateBorderedPanel(frame.transform, "Tab_" + i, NotaryUiTheme.CardBg, NotaryUiTheme.GoldSoft, 1.4f);
                tab.rectTransform.anchorMin = new Vector2(1f, 1f);
                tab.rectTransform.anchorMax = new Vector2(1f, 1f);
                tab.rectTransform.pivot = new Vector2(1f, 1f);
                tab.rectTransform.anchoredPosition = new Vector2(-24f - (2 - i) * 168f, -16f);
                tab.rectTransform.sizeDelta = new Vector2(158f, 36f);
                tabBorders[i] = tab;
                tabFills[i] = tab.transform.Find("Inner").GetComponent<Image>();

                var btn = tab.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() =>
                {
                    if (activeTab == tabIndex) return;
                    activeTab = tabIndex;
                    UiAudioSynth.PlayMove();
                    dirty = true;
                });

                tabLabels[i] = NotaryUiTheme.CreateText(tab.transform, "Label", "", 14, NotaryUiTheme.Ivory, TextAnchor.MiddleCenter, true, false);
                NotaryUiTheme.Stretch(tabLabels[i].rectTransform, 6f, 2f, 6f, 2f);
            }

            var rule = NotaryUiTheme.CreateImage(frame.transform, "TopRule", NotaryUiTheme.GoldSoft);
            rule.rectTransform.anchorMin = new Vector2(0.025f, 1f);
            rule.rectTransform.anchorMax = new Vector2(0.975f, 1f);
            rule.rectTransform.anchoredPosition = new Vector2(0f, -64f);
            rule.rectTransform.sizeDelta = new Vector2(0f, 1.5f);

            contentRoot = NotaryUiTheme.CreateRect(frame.transform, "Content");
            contentRoot.anchorMin = Vector2.zero;
            contentRoot.anchorMax = Vector2.one;
            contentRoot.offsetMin = new Vector2(26f, 44f);
            contentRoot.offsetMax = new Vector2(-26f, -74f);

            footerText = NotaryUiTheme.CreateText(frame.transform, "Footer", "", 13, NotaryUiTheme.ParchmentMuted, TextAnchor.MiddleCenter, false, false);
            footerText.rectTransform.anchorMin = new Vector2(0f, 0f);
            footerText.rectTransform.anchorMax = new Vector2(1f, 0f);
            footerText.rectTransform.offsetMin = new Vector2(24f, 10f);
            footerText.rectTransform.offsetMax = new Vector2(-24f, 38f);

            rootPanel.SetActive(false);
            lastLang = Loc.Language;
        }

        public void Open(SkillSession session, RepresentativeArea area, int visitedRooms)
        {
            cachedSession = session;
            cachedArea = area;
            cachedVisited = visitedRooms;
            open = true;
            IsOpen = true;
            Time.timeScale = 0f;
            GameInput.SetGameplay(false);
            UiAudioSynth.PlayMapToggle(true);
            dirty = true;
            if (rootPanel != null) rootPanel.SetActive(true);
            Rebuild();
        }

        public void Close(bool restoreGameplay)
        {
            if (open)
            {
                open = false;
                IsOpen = false;
                if (restoreGameplay)
                {
                    if (Time.timeScale == 0f) Time.timeScale = 1f;
                    GameInput.SetGameplay(true);
                }
            }
            if (rootPanel != null) rootPanel.SetActive(false);
        }

        public void Observe(bool playing, SkillSession session, RepresentativeArea area, int visitedRooms, bool blockedByMap)
        {
            if (canvas == null) return;
            cachedSession = session;
            cachedArea = area;
            cachedVisited = visitedRooms;

            if (!playing || session == null)
            {
                if (open) Close(false);
                return;
            }

            if (AutoTestRuntime.Report == null && !blockedByMap && Input.GetKeyDown(KeyCode.N))
            {
                if (open)
                {
                    ConsumedCancelFrame = Time.frameCount;
                    Close(true);
                    UiAudioSynth.PlayMapToggle(false);
                }
                else
                {
                    Open(session, area, visitedRooms);
                }
                return;
            }

            if (!open) return;

            if (GameInput.CancelDown)
            {
                ConsumedCancelFrame = Time.frameCount;
                Close(true);
                UiAudioSynth.PlayMapToggle(false);
                return;
            }

            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Q))
            {
                activeTab = (activeTab + 2) % 3;
                UiAudioSynth.PlayMove();
                dirty = true;
            }
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.E))
            {
                activeTab = (activeTab + 1) % 3;
                UiAudioSynth.PlayMove();
                dirty = true;
            }

            if (lastLang != Loc.Language)
            {
                lastLang = Loc.Language;
                dirty = true;
            }

            if (dirty) Rebuild();
        }

        private void Rebuild()
        {
            dirty = false;
            if (contentRoot == null || cachedSession == null) return;

            titleText.font = NotaryUiTheme.TitleFont;
            titleText.text = Loc.Get("journal.title");
            footerText.font = NotaryUiTheme.BodyFont;
            footerText.text = Loc.Get("journal.footer");

            string[] tKeys = { "journal.tab_skills", "journal.tab_codex", "journal.tab_progress" };
            for (int i = 0; i < 3; i++)
            {
                bool sel = i == activeTab;
                tabBorders[i].color = sel ? NotaryUiTheme.Gold : NotaryUiTheme.GoldSoft;
                tabFills[i].color = sel ? NotaryUiTheme.CardSelectedBg : NotaryUiTheme.CardBg;
                tabLabels[i].font = NotaryUiTheme.TitleFont;
                tabLabels[i].color = sel ? NotaryUiTheme.GoldGlow : NotaryUiTheme.ParchmentMuted;
                tabLabels[i].text = Loc.Get(tKeys[i]);
            }

            for (int i = contentRoot.childCount - 1; i >= 0; i--)
                Destroy(contentRoot.GetChild(i).gameObject);

            if (activeTab == 0) BuildSkillsTab();
            else if (activeTab == 1) BuildCodexTab();
            else BuildProgressTab();
        }

        private void BuildSkillsTab()
        {
            var gridGo = NotaryUiTheme.CreateRect(contentRoot, "SkillsGrid");
            NotaryUiTheme.Stretch(gridGo);
            var grid = gridGo.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.cellSize = new Vector2(502f, 152f);
            grid.spacing = new Vector2(18f, 14f);
            grid.childAlignment = TextAnchor.UpperCenter;

            var rows = SkillStatusView.Read(cachedSession);
            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                var prog = cachedSession.Progression.Record.Get(r.SkillId);
                var def = cachedSession.Progression.Catalog.Get(r.SkillId);
                float ratio = 0f;
                if (r.Owned && prog != null && def != null)
                {
                    int cap = def.stage3At > 0 ? def.stage3At : 100;
                    ratio = Mathf.Clamp01((float)prog.validUseCount / cap);
                }

                var card = NotaryUiTheme.CreateBorderedPanel(gridGo, "Skill_" + i,
                    r.Owned ? NotaryUiTheme.CardSelectedBg : NotaryUiTheme.CardBg,
                    r.Owned ? NotaryUiTheme.Gold : new Color(0.30f, 0.34f, 0.42f, 0.65f), 1.5f);

                var name = NotaryUiTheme.CreateText(card.transform, "Name", r.Name + (r.Owned ? "" : Loc.Get("ui.locked_tag")), 17,
                    r.Owned ? NotaryUiTheme.GoldGlow : NotaryUiTheme.ParchmentMuted, TextAnchor.MiddleLeft, true, true);
                name.rectTransform.anchorMin = new Vector2(0f, 1f);
                name.rectTransform.anchorMax = new Vector2(0.68f, 1f);
                name.rectTransform.offsetMin = new Vector2(14f, -36f);
                name.rectTransform.offsetMax = new Vector2(0f, -4f);
                name.resizeTextForBestFit = true;
                name.resizeTextMinSize = 12;
                name.resizeTextMaxSize = 17;

                var st = NotaryUiTheme.CreateText(card.transform, "Stage", r.StageText, 13,
                    r.Owned ? NotaryUiTheme.TealAccent : NotaryUiTheme.ParchmentMuted, TextAnchor.MiddleRight, false, true);
                st.rectTransform.anchorMin = new Vector2(0.60f, 1f);
                st.rectTransform.anchorMax = new Vector2(1f, 1f);
                st.rectTransform.offsetMin = Vector2.zero;
                st.rectTransform.offsetMax = new Vector2(-14f, -6f);

                var track = NotaryUiTheme.CreateBorderedPanel(card.transform, "Track", NotaryUiTheme.BarTrack, NotaryUiTheme.GoldSoft, 1f);
                track.rectTransform.anchorMin = new Vector2(0f, 1f);
                track.rectTransform.anchorMax = new Vector2(1f, 1f);
                track.rectTransform.offsetMin = new Vector2(14f, -48f);
                track.rectTransform.offsetMax = new Vector2(-14f, -36f);

                var fill = NotaryUiTheme.CreateImage(track.transform, "Fill", r.Owned ? NotaryUiTheme.TealAccent : new Color(0.32f, 0.36f, 0.42f, 0.6f));
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = new Vector2(ratio, 1f);
                fill.rectTransform.offsetMin = new Vector2(1f, 1f);
                fill.rectTransform.offsetMax = new Vector2(-1f, -1f);

                var formula = NotaryUiTheme.CreateText(card.transform, "Input",
                    Loc.Get("journal.input_formula", FormatSkillFormula(r.SkillId)), 13, NotaryUiTheme.Gold, TextAnchor.MiddleLeft, false, false);
                formula.rectTransform.anchorMin = new Vector2(0f, 1f);
                formula.rectTransform.anchorMax = new Vector2(1f, 1f);
                formula.rectTransform.offsetMin = new Vector2(14f, -72f);
                formula.rectTransform.offsetMax = new Vector2(-14f, -52f);

                var progress = NotaryUiTheme.CreateText(card.transform, "Progress", r.ProgressText, 12, NotaryUiTheme.Ivory, TextAnchor.MiddleLeft, false, false);
                progress.rectTransform.anchorMin = new Vector2(0f, 1f);
                progress.rectTransform.anchorMax = new Vector2(1f, 1f);
                progress.rectTransform.offsetMin = new Vector2(14f, -96f);
                progress.rectTransform.offsetMax = new Vector2(-14f, -74f);

                var form = NotaryUiTheme.CreateText(card.transform, "Form", r.FormText, 12, NotaryUiTheme.ParchmentMuted, TextAnchor.UpperLeft, false, false);
                form.rectTransform.anchorMin = new Vector2(0f, 0f);
                form.rectTransform.anchorMax = new Vector2(1f, 0f);
                form.rectTransform.offsetMin = new Vector2(14f, 8f);
                form.rectTransform.offsetMax = new Vector2(-14f, 52f);
            }
        }

        private void BuildCodexTab()
        {
            var gridGo = NotaryUiTheme.CreateRect(contentRoot, "CodexGrid");
            NotaryUiTheme.Stretch(gridGo);
            var grid = gridGo.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.cellSize = new Vector2(502f, 114f);
            grid.spacing = new Vector2(18f, 12f);
            grid.childAlignment = TextAnchor.UpperCenter;

            var record = cachedSession.Progression.Record;
            for (int i = 0; i < Characters.Length; i++)
            {
                var c = Characters[i];
                bool skillOwned = !string.IsNullOrEmpty(c.SkillId) && record.Get(c.SkillId)?.acquired == true;
                bool met = skillOwned || HasFlagPrefix(record, c.FlagHint);
                bool inherited = c.SkillId != null && c.SkillId != SkillRules.BasicSword && skillOwned;

                var card = NotaryUiTheme.CreateBorderedPanel(gridGo, "Char_" + c.Id,
                    met ? NotaryUiTheme.CardSelectedBg : NotaryUiTheme.CardBg,
                    met ? c.Accent : new Color(0.28f, 0.30f, 0.36f, 0.6f), 1.4f);

                var pFrame = NotaryUiTheme.CreateBorderedPanel(card.transform, "PFrame", NotaryUiTheme.PanelBg,
                    met ? NotaryUiTheme.Gold : NotaryUiTheme.GoldSoft, 1.2f);
                pFrame.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                pFrame.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                pFrame.rectTransform.pivot = new Vector2(0f, 0.5f);
                pFrame.rectTransform.anchoredPosition = new Vector2(12f, 0f);
                pFrame.rectTransform.sizeDelta = new Vector2(84f, 84f);

                var portrait = NotaryUiTheme.LoadPortrait(c.Id);
                if (portrait != null)
                {
                    var img = NotaryUiTheme.CreateImage(pFrame.transform, "Portrait", met ? Color.white : new Color(0.25f, 0.25f, 0.30f, 0.85f), portrait);
                    NotaryUiTheme.Stretch(img.rectTransform, 2f, 2f, 2f, 2f);
                    img.preserveAspect = true;
                }
                else
                {
                    var crest = NotaryUiTheme.CreateImage(pFrame.transform, "Crest",
                        met ? new Color(c.Accent.r * 0.42f, c.Accent.g * 0.42f, c.Accent.b * 0.42f, 0.95f) : new Color(0.10f, 0.12f, 0.16f, 0.9f));
                    NotaryUiTheme.Stretch(crest.rectTransform, 4f, 4f, 4f, 4f);
                    string spName = Loc.Get("area.speaker." + c.Id);
                    string initial = met && !string.IsNullOrEmpty(spName) ? spName.Substring(0, 1) : "?";
                    var initTxt = NotaryUiTheme.CreateText(crest.transform, "Initial", initial, 32,
                        met ? NotaryUiTheme.GoldGlow : NotaryUiTheme.ParchmentMuted, TextAnchor.MiddleCenter, true, true);
                    NotaryUiTheme.Stretch(initTxt.rectTransform);
                }

                string displayName = met ? Loc.Get("area.speaker." + c.Id) : "???";
                var nameTxt = NotaryUiTheme.CreateText(card.transform, "Name", displayName, 18,
                    met ? NotaryUiTheme.GoldGlow : NotaryUiTheme.ParchmentMuted, TextAnchor.MiddleLeft, true, true);
                nameTxt.rectTransform.anchorMin = new Vector2(0f, 1f);
                nameTxt.rectTransform.anchorMax = new Vector2(0.68f, 1f);
                nameTxt.rectTransform.offsetMin = new Vector2(108f, -42f);
                nameTxt.rectTransform.offsetMax = new Vector2(0f, -6f);
                nameTxt.resizeTextForBestFit = true;
                nameTxt.resizeTextMinSize = 13;
                nameTxt.resizeTextMaxSize = 18;

                string status = inherited ? Loc.Get("journal.cleared") : met ? Loc.Get("journal.met") : Loc.Get("journal.unmet");
                var statTxt = NotaryUiTheme.CreateText(card.transform, "Status", status, 12,
                    inherited ? NotaryUiTheme.TealAccent : met ? NotaryUiTheme.Gold : NotaryUiTheme.ParchmentMuted, TextAnchor.MiddleRight, false, false);
                statTxt.rectTransform.anchorMin = new Vector2(0.62f, 1f);
                statTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
                statTxt.rectTransform.offsetMin = Vector2.zero;
                statTxt.rectTransform.offsetMax = new Vector2(-14f, -10f);

                var roleTxt = NotaryUiTheme.CreateText(card.transform, "Role", met ? Loc.Get(c.RoleKey) : "—", 14,
                    met ? NotaryUiTheme.Ivory : NotaryUiTheme.ParchmentMuted, TextAnchor.UpperLeft, false, false);
                roleTxt.rectTransform.anchorMin = new Vector2(0f, 0f);
                roleTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
                roleTxt.rectTransform.offsetMin = new Vector2(108f, 12f);
                roleTxt.rectTransform.offsetMax = new Vector2(-14f, -44f);
            }
        }

        private void BuildProgressTab()
        {
            var record = cachedSession.Progression.Record;
            int totalRooms = RepresentativeScript.Load().rooms.Length;
            int visited = Mathf.Max(cachedVisited, cachedArea != null ? 1 : 0);
            int pct = totalRooms > 0 ? Mathf.RoundToInt(100f * visited / totalRooms) : 0;

            int ownedBenefactors = 0;
            for (int i = 0; i < BenefactorIds.All.Length; i++)
                if (record.Get(BenefactorIds.All[i])?.acquired == true) ownedBenefactors++;

            var left = NotaryUiTheme.CreateBorderedPanel(contentRoot, "ProgressCard", NotaryUiTheme.CardBg, NotaryUiTheme.GoldSoft, 1.5f);
            left.rectTransform.anchorMin = new Vector2(0f, 0f);
            left.rectTransform.anchorMax = new Vector2(0.52f, 1f);
            left.rectTransform.offsetMin = Vector2.zero;
            left.rectTransform.offsetMax = new Vector2(-10f, 0f);

            var sb = new StringBuilder();
            if (cachedArea != null && cachedArea.Room != null)
            {
                sb.AppendLine(cachedArea.Room.Label);
                sb.AppendLine();
            }
            sb.AppendLine(Loc.Get("journal.explored_rooms", visited, totalRooms, pct));
            sb.AppendLine(Loc.Get("journal.owned_skills", ownedBenefactors));
            sb.AppendLine(Loc.Get("journal.investigation_stat", record.investigation.clueIds.Count));
            sb.AppendLine();
            var sword = record.Get(SkillRules.BasicSword);
            if (sword != null)
                sb.AppendLine(Loc.Get("hud.skill_progress", cachedSession.Progression.Catalog.Get(SkillRules.BasicSword).DisplayName, sword.validUseCount, sword.stage, "", ""));

            var leftText = NotaryUiTheme.CreateText(left.transform, "Summary", sb.ToString().TrimEnd(), 16, NotaryUiTheme.Ivory, TextAnchor.UpperLeft, false, true);
            NotaryUiTheme.Stretch(leftText.rectTransform, 22f, 22f, 22f, 22f);
            leftText.lineSpacing = 1.4f;

            var right = NotaryUiTheme.CreateBorderedPanel(contentRoot, "KeybindsCard", NotaryUiTheme.CardBg, NotaryUiTheme.GoldSoft, 1.5f);
            right.rectTransform.anchorMin = new Vector2(0.52f, 0f);
            right.rectTransform.anchorMax = new Vector2(1f, 1f);
            right.rectTransform.offsetMin = new Vector2(10f, 0f);
            right.rectTransform.offsetMax = Vector2.zero;

            var ks = new StringBuilder();
            ks.AppendLine(Loc.Get("journal.keybinds_summary"));
            ks.AppendLine();
            ks.AppendLine(Loc.Get("keybind.jump", "[" + GameInput.KeyLabel(GameInput.JumpKey) + "]"));
            ks.AppendLine(Loc.Get("keybind.attack", "[" + GameInput.KeyLabel(GameInput.AttackKey) + "]"));
            ks.AppendLine(Loc.Get("keybind.skill", "[" + GameInput.KeyLabel(GameInput.CastKey) + "]"));
            ks.AppendLine(Loc.Get("keybind.dash", "[" + GameInput.KeyLabel(GameInput.DashKey) + "]"));
            ks.AppendLine(Loc.Get("keybind.parry", "[" + GameInput.KeyLabel(GameInput.ParryKey) + "]"));
            ks.AppendLine(Loc.Get("keybind.interact", "[" + GameInput.KeyLabel(GameInput.InteractKey) + "]"));
            ks.AppendLine(Loc.Get("keybind.map", "[" + GameInput.KeyLabel(GameInput.MapKey) + "]"));

            var rightText = NotaryUiTheme.CreateText(right.transform, "Keys", ks.ToString().TrimEnd(), 15, NotaryUiTheme.GoldGlow, TextAnchor.UpperLeft, false, true);
            NotaryUiTheme.Stretch(rightText.rectTransform, 22f, 22f, 22f, 22f);
            rightText.lineSpacing = 1.45f;
        }

        private static bool HasFlagPrefix(CoreRecord record, string hint)
        {
            if (record == null || string.IsNullOrEmpty(hint)) return false;
            for (int i = 0; i < record.storyFlags.Count; i++)
                if (record.storyFlags[i].IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            for (int i = 0; i < record.completedEncounters.Count; i++)
                if (record.completedEncounters[i].IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        public static string FormatSkillFormula(string skillId)
        {
            string cast = GameInput.KeyLabel(GameInput.CastKey);
            string atk = GameInput.KeyLabel(GameInput.AttackKey);
            switch (skillId)
            {
                case SkillRules.BasicSword: return atk + "  (↑ / ↓ " + atk + ")";
                case "silver_stitch": return cast;
                case "pendulum_rewind": return "← / → + " + cast;
                case "curtain_call_waltz": return "↑ + " + cast;
                case "resonance_forge": return "↓ + " + cast;
                case "canvas_carve": return "↓ + " + cast + " (Air)";
                default: return cast;
            }
        }

        private void OnDestroy()
        {
            if (open) IsOpen = false;
            if (canvas != null) Destroy(canvas.gameObject);
        }
    }
}
