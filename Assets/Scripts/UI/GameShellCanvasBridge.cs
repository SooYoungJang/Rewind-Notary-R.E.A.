using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RewindNotary
{
    public sealed partial class GameShell
    {
        private GameShellCanvas shellCanvas;

        private void InitShellCanvas()
        {
            shellCanvas = gameObject.AddComponent<GameShellCanvas>();
            shellCanvas.Initialize(transform);
            shellCanvas.OnActivateRow = index =>
            {
                selected = index;
                Activate(index);
            };
            shellCanvas.OnAdjustSetting = (index, dir) =>
            {
                selected = index;
                AdjustSetting(index, dir);
            };
            shellCanvas.OnDeleteSlot = slot => RequestDelete(slot);
        }

        private void LateUpdate()
        {
            if (shellCanvas == null || Settings == null) return;
            if (Page == ShellPage.StoryDialogue && openingScene != null)
            {
                shellCanvas.Render(default);
                return;
            }
            var snap = new GameShellCanvas.ShellSnapshot
            {
                Visible = !Playing,
                Page = Page,
                OverLiveWorld = Page == ShellPage.Pause || Page == ShellPage.Skills || Page == ShellPage.QuickSlots || Page == ShellPage.Investigation
                    || Page == ShellPage.CoreReading || Page == ShellPage.ReturnSelection || Page == ShellPage.ReturnConfirm
                    || Page == ShellPage.StoryDialogue || Page == ShellPage.StorySaving,
                GameTitle = Loc.Get("game.menu_title"),
                Heading = Heading(),
                StatusText = CurrentStatusMessage(),
                FooterText = Page == ShellPage.Investigation || Page == ShellPage.CoreReading
                    ? Loc.Get("input.reading_menu")
                    : Loc.Get("input.shell_menu"),
                Buttons = new List<GameShellCanvas.ButtonItem>()
            };

            if (!Playing) PopulatePageSnapshot(ref snap);
            shellCanvas.Render(snap);
        }

        private string CurrentStatusMessage()
        {
            if (Page == ShellPage.Investigation || Page == ShellPage.CoreReading)
            {
                if (game.ActiveSkills == null || string.IsNullOrEmpty(game.ActiveSkills.StorageError)) return null;
                return game.ActiveSkills.LastSaveSucceeded ? Loc.Get("save.log_path_hint") : Loc.Get("save.retrying_memory");
            }
            if (!string.IsNullOrEmpty(message)) return message;
            if (Page == ShellPage.Settings) return Settings.Error;
            if (Page == ShellPage.Slots && selected < records.Length) return slotErrors[selected];
            return null;
        }

        private void PopulatePageSnapshot(ref GameShellCanvas.ShellSnapshot snap)
        {
            switch (Page)
            {
                case ShellPage.StoryDialogue:
                    var row = DialogueRow;
                    snap.Dialogue = new GameShellCanvas.DialogueView
                    {
                        RoomLabel = Heading(),
                        SpeakerId = row?.speakerId ?? "",
                        SpeakerName = row != null ? Loc.Get("area.speaker." + row.speakerId) : "",
                        BodyText = row != null ? Loc.Get(row.textKey) : "",
                        CanSkip = CanSkipLine,
                        SelectedIndex = selected,
                        ContinueLabel = Loc.Get("area.continue"),
                        SkipLabel = Loc.Get("area.skip"),
                        ErrorText = game.ActiveSkills != null && !game.ActiveSkills.LastSaveSucceeded ? game.ActiveSkills.StorageError : null,
                        InstantReveal = AutoTestRuntime.Report != null || CanSkipLine
                    };
                    break;

                case ShellPage.Slots:
                    for (int i = 0; i < records.Length; i++)
                    {
                        snap.Buttons.Add(new GameShellCanvas.ButtonItem
                        {
                            Index = i,
                            Label = SlotLabel(i),
                            Selected = selected == i,
                            HasDelete = true,
                            DeleteSlot = i + 1
                        });
                    }
                    snap.Buttons.Add(new GameShellCanvas.ButtonItem
                    {
                        Index = records.Length,
                        Label = Loc.Get("ui.back"),
                        Selected = selected == records.Length
                    });
                    break;

                case ShellPage.Stages:
                    for (int i = 0; i < StageCatalog.All.Length; i++)
                    {
                        var st = StageCatalog.All[i];
                        snap.Buttons.Add(new GameShellCanvas.ButtonItem
                        {
                            Index = i,
                            Label = st.Name + "  [" + st.Status(selectedRecord) + "]",
                            Selected = selected == i
                        });
                    }
                    snap.Buttons.Add(new GameShellCanvas.ButtonItem
                    {
                        Index = StageCatalog.All.Length,
                        Label = Loc.Get("ui.back"),
                        Selected = selected == StageCatalog.All.Length
                    });
                    if (EndingCheckpoint.Marker(selectedRecord) != null)
                    {
                        snap.Buttons.Add(new GameShellCanvas.ButtonItem
                        {
                            Index = StageCatalog.All.Length + 1,
                            Label = Loc.Get("story.resume_previous"),
                            Selected = selected == StageCatalog.All.Length + 1
                        });
                    }
                    break;

                case ShellPage.Skills:
                    PopulateSkills(ref snap);
                    break;

                case ShellPage.QuickSlots:
                    snap.BodyHeader = Loc.Get("quick_skill.assign_hint");
                    for (int i = 0; i < QuickSkills.Keys.Length; i++)
                    {
                        string equipped = QuickSkills.Equipped(Settings.Value, game.ActiveSkills.Progression.Record, i);
                        snap.Buttons.Add(new GameShellCanvas.ButtonItem
                        {
                            Index = i,
                            Label = QuickSkills.Keys[i] + "   " + QuickSkills.Name(catalog, equipped),
                            Selected = selected == i
                        });
                    }
                    snap.Buttons.Add(new GameShellCanvas.ButtonItem { Index = QuickSkills.Keys.Length,
                        Label = Loc.Get("ui.back"), Selected = selected == QuickSkills.Keys.Length });
                    break;

                case ShellPage.Investigation:
                    PopulateInvestigation(ref snap);
                    break;

                case ShellPage.CoreReading:
                    PopulateCoreReading(ref snap);
                    break;

                case ShellPage.CourtLift:
                    var line = LiftLine();
                    if (line != null)
                    {
                        snap.BodyText = SkillSession.StoryText(line);
                        snap.ScrollNormalized = Mathf.Clamp01(dialogueScroll.y / 600f);
                    }
                    var liftLabels = LiftLabels();
                    for (int i = 0; i < liftLabels.Length; i++)
                    {
                        snap.Buttons.Add(new GameShellCanvas.ButtonItem
                        {
                            Index = i,
                            Label = liftLabels[i],
                            Selected = selected == i
                        });
                    }
                    break;

                case ShellPage.ReturnSelection:
                    var state = game.ActiveSkills.Finale.State;
                    snap.BodyHeader = Loc.Get("finale.return_instructions", state.returnCount, state.ReleasedPercent);
                    for (int i = 0; i < BenefactorIds.All.Length; i++)
                    {
                        var id = BenefactorIds.All[i];
                        snap.Buttons.Add(new GameShellCanvas.ButtonItem
                        {
                            Index = i,
                            Label = catalog.Get(id).DisplayName + (state.CanUse(id) ? Loc.Get("finale.available_suffix") : Loc.Get("finale.pledged_suffix")),
                            Selected = selected == i
                        });
                    }
                    snap.Buttons.Add(new GameShellCanvas.ButtonItem
                    {
                        Index = 5,
                        Label = Loc.Get("finale.back_to_battle"),
                        Selected = selected == 5
                    });
                    break;

                case ShellPage.ReturnConfirm:
                    var rState = game.ActiveSkills.Finale.State;
                    string details = Loc.Get("finale.confirm_return_body", catalog.Get(returnSkill).DisplayName, (rState.returnCount + 1) * 20);
                    var confirmRow = game.ActiveCassel.Scenes?.ConfirmMenu;
                    if (confirmRow != null) details = SkillSession.StoryText(confirmRow) + "\n\n" + details;
                    snap.BodyText = details;
                    snap.Buttons.Add(new GameShellCanvas.ButtonItem { Index = 0, Label = Loc.Get("finale.cancel_return"), Selected = selected == 0 });
                    snap.Buttons.Add(new GameShellCanvas.ButtonItem { Index = 1, Label = Loc.Get("finale.arm_return"), Selected = selected == 1 });
                    break;

                case ShellPage.StorySaving:
                    snap.BodyText = Loc.Get("save.retrying_memory") + "\n" + game.ActiveSkills.StorageError;
                    snap.Buttons.Add(new GameShellCanvas.ButtonItem { Index = 0, Label = Loc.Get("ui.save"), Selected = selected == 0 });
                    break;

                default:
                    string[] labels = Labels();
                    if (Page == ShellPage.Settings) snap.BodyHeader = Loc.Get("settings.adjust_hint");
                    for (int i = 0; i < labels.Length; i++)
                    {
                        snap.Buttons.Add(new GameShellCanvas.ButtonItem
                        {
                            Index = i,
                            Label = labels[i],
                            Selected = selected == i,
                            IsSetting = Page == ShellPage.Settings && i < 5
                        });
                    }
                    break;
            }
        }

        private void PopulateSkills(ref GameShellCanvas.ShellSnapshot snap)
        {
            var session = game.ActiveSkills;
            snap.SkillCards = new List<GameShellCanvas.SkillCardItem>();
            if (session == null)
            {
                snap.BodyText = Loc.Get("skills.no_active_record");
                return;
            }
            snap.BodyHeader = SkillStatusView.Mode(session);
            var rows = SkillStatusView.Read(session);
            var trialCatalog = BenefactorTrialCatalog.Load();
            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                var record = session.Progression.Record.Get(r.SkillId);
                var def = session.Progression.Catalog.Get(r.SkillId);
                float ratio = 0f;
                if (r.Owned && record != null && def != null)
                {
                    int cap = def.stage3At > 0 ? def.stage3At : 100;
                    ratio = Mathf.Clamp01((float)record.validUseCount / cap);
                }
                var trialInfo = trialCatalog.ForSkill(r.SkillId);
                snap.SkillCards.Add(new GameShellCanvas.SkillCardItem
                {
                    Name = r.Name + (r.Owned ? "" : Loc.Get("ui.locked_tag")),
                    Owned = r.Owned,
                    StageText = r.StageText,
                    ProgressText = r.ProgressText,
                    ProgressRatio = ratio,
                    FormText = r.FormText,
                    TrialText = trialInfo != null ? BenefactorTrialProgress.Status(session.Progression.Record, trialInfo) : ""
                });
            }
            snap.Buttons.Add(new GameShellCanvas.ButtonItem
            {
                Index = 0,
                Label = Loc.Get("ui.investigation_progress", Investigation.ProgressLabel),
                Selected = selected == 0
            });
            snap.Buttons.Add(new GameShellCanvas.ButtonItem
            {
                Index = 1,
                Label = Loc.Get("quick_skill.menu"),
                Selected = selected == 1
            });
            snap.Buttons.Add(new GameShellCanvas.ButtonItem
            {
                Index = 2,
                Label = Loc.Get("ui.back_to_pause"),
                Selected = selected == 2
            });
        }

        private void PopulateInvestigation(ref GameShellCanvas.ShellSnapshot snap)
        {
            var inv = Investigation;
            snap.BodyHeader = Loc.Get("investigation.scroll_hint", inv.ProgressLabel);
            string topicId = inv.Catalog.topics[investigationTopic].id;
            var sb = new StringBuilder();
            foreach (var clue in inv.Catalog.clues)
            {
                if (clue.topicId != topicId) continue;
                bool owned = inv.HasClue(clue.clueId);
                string prefix = owned ? Loc.Get("investigation.collected_prefix") : Loc.Get("investigation.uncollected_prefix");
                sb.AppendLine(prefix + (inv.CanRevealClue(clue.clueId) ? clue.Label : Loc.Get("ui.locked_tag")));
                if (owned)
                {
                    sb.AppendLine(InvestigationText.Get(clue.summaryKey, Loc.Get("investigation.summary_pending")));
                    sb.AppendLine(InvestigationText.Get(clue.BodyKey, InvestigationText.Get(clue.textKey, Loc.Get("investigation.body_pending"))));
                }
                sb.AppendLine();
            }
            snap.BodyText = sb.ToString().TrimEnd();
            snap.ScrollNormalized = Mathf.Clamp01(clueScroll.y / 600f);

            int idx = 0;
            for (int i = 0; i < inv.Catalog.topics.Length; i++, idx++)
            {
                var t = inv.Catalog.topics[i];
                snap.Buttons.Add(new GameShellCanvas.ButtonItem
                {
                    Index = idx,
                    Label = (i == investigationTopic ? Loc.Get("ui.selected_prefix") : "") + t.Label + "  " + inv.ClueCount(t.id) + "/" + inv.TopicTotal(t.id),
                    Selected = selected == idx
                });
            }
            for (int i = 0; i < inv.Catalog.stages.Length; i++, idx++)
            {
                var st = inv.Catalog.stages[i];
                string status = inv.HasRead(st.id) ? Loc.Get("investigation.read_again")
                    : inv.OffersOriginal(readingAtCore, st.id) ? Loc.Get("investigation.read_core")
                    : inv.CanRead(st) ? (readingAtCore ? Loc.Get("investigation.read_available") : Loc.Get("investigation.core_required"))
                    : Loc.Get("investigation.locked_requirements");
                snap.Buttons.Add(new GameShellCanvas.ButtonItem
                {
                    Index = idx,
                    Label = st.Label + " — " + status,
                    Selected = selected == idx
                });
            }
            snap.Buttons.Add(new GameShellCanvas.ButtonItem
            {
                Index = idx,
                Label = investigationReturn == ShellPage.Playing ? Loc.Get("ui.back_to_game") : Loc.Get("ui.back_to_skills"),
                Selected = selected == idx
            });
        }

        private void PopulateCoreReading(ref GameShellCanvas.ShellSnapshot snap)
        {
            var inv = Investigation;
            var stage = inv.Catalog.Stage(readingStageId);
            bool unlocked = ReadingAvailable(stage);
            snap.BodyHeader = stage.Label + (unlocked ? Loc.Get("investigation.entry_suffix", readingEntry + 1, stage.entries.Length) : Loc.Get("ui.locked_suffix"));
            var sb = new StringBuilder();
            if (!unlocked)
            {
                sb.AppendLine(inv.GateLabel(stage) + (readingAtCore ? "" : Loc.Get("investigation.new_record_hint")));
                snap.Buttons.Add(new GameShellCanvas.ButtonItem { Index = 0, Label = Loc.Get("ui.back_to_investigation"), Selected = selected == 0 });
            }
            else
            {
                var entry = stage.entries[readingEntry];
                sb.AppendLine("<b>" + InvestigationText.Get(entry.titleKey, Loc.Get("investigation.title_pending")) + "</b>");
                string body = InvestigationText.Get(entry.BodyKey, Loc.Get("investigation.body_pending"));
                bool still = entry.id == "record_01_failure";
                if (!still) sb.AppendLine(InvestigationText.Get(entry.summaryKey, Loc.Get("investigation.record_summary_pending")));
                sb.AppendLine();
                if (still)
                {
                    string[] frames = body.Split('\f');
                    sb.AppendLine(frames[Mathf.Min(stillIndex, frames.Length - 1)]);
                    if (stillIndex == 5)
                    {
                        sb.AppendLine();
                        sb.AppendLine(InvestigationText.Get(entry.summaryKey, Loc.Get("investigation.record_summary_pending")));
                    }
                }
                else
                {
                    sb.AppendLine(body.Replace("\f", "\n\n").Replace("\t", "  |  ").Replace("~~", ""));
                }
                string related = string.Join("\n", Array.ConvertAll(entry.relatedClues, id =>
                    (inv.HasClue(id) ? Loc.Get("investigation.collected_prefix") : Loc.Get("investigation.uncollected_prefix"))
                    + (inv.CanRevealClue(id) ? inv.Catalog.Clue(id).Label : Loc.Get("ui.locked_tag"))));
                sb.AppendLine();
                sb.AppendLine(Loc.Get("investigation.related_clues", related));

                snap.Buttons.Add(new GameShellCanvas.ButtonItem
                {
                    Index = 0,
                    Label = still && stillIndex > 0 ? Loc.Get("area.core_prev") : Loc.Get("investigation.previous_entry"),
                    Selected = selected == 0
                });
                snap.Buttons.Add(new GameShellCanvas.ButtonItem
                {
                    Index = 1,
                    Label = still && stillIndex < 5 ? Loc.Get("area.core_next")
                        : inv.HasRead(stage.id) ? Loc.Get("investigation.next_review") : Loc.Get("investigation.confirm_entry"),
                    Selected = selected == 1
                });
                snap.Buttons.Add(new GameShellCanvas.ButtonItem
                {
                    Index = 2,
                    Label = Loc.Get("investigation.close_keep_progress"),
                    Selected = selected == 2
                });
            }
            snap.BodyText = sb.ToString().TrimEnd();
            snap.ScrollNormalized = Mathf.Clamp01(readingScroll.y / 600f);
        }
    }
}
