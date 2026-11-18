using System;
using UnityEngine;

namespace RewindNotary
{
    // Content integration supplies approved localized text. Missing content remains neutral UI chrome.
    public static class InvestigationText
    {
        public static Func<string, string> Resolve;
        public static string Get(string key, string placeholder)
        {
            string value = Resolve?.Invoke(key);
            return !string.IsNullOrEmpty(value) ? value : Loc.Contains(key) ? Loc.Get(key) : placeholder;
        }
    }

    public sealed partial class GameShell
    {
        private ShellPage investigationReturn;
        private int investigationTopic, readingEntry, stillIndex;
        private bool readingAtCore;
        private string readingStageId;
        private Vector2 clueScroll, readingScroll;
        private InvestigationService Investigation => game.ActiveSkills?.Investigation;
        private int InvestigationRows => Page == ShellPage.Investigation ? Investigation.Catalog.topics.Length + Investigation.Catalog.stages.Length + 1
            : ReadingAvailable(Investigation.Catalog.Stage(readingStageId)) ? 3 : 1;
        // Bound to the hub terminal interaction when the player returns from the districts.
        public void OpenStoryCore()
        {
            if (Playing && Investigation?.Record.lastStageId == "hub") OpenInvestigation(null, true);
        }
        public void OpenInvestigation(string clueId = null, bool atCore = false)
        {
            if (Investigation == null) return;
            readingAtCore = atCore && Playing;
            investigationReturn = Playing ? ShellPage.Playing : ShellPage.Skills;
            if (Playing) Pause();
            var clue = Investigation.Catalog.Clue(clueId);
            if (clue != null) investigationTopic = Array.FindIndex(Investigation.Catalog.topics, t => t.id == clue.topicId);
            clueScroll = Vector2.zero; Go(ShellPage.Investigation);
        }
        private bool ReadingAvailable(CoreReadingStage stage) => Investigation.CanRead(stage) && (readingAtCore || Investigation.HasRead(stage.id));
        private void CloseInvestigation()
        {
            if (Page == ShellPage.CoreReading) { Go(ShellPage.Investigation); return; }
            if (investigationReturn == ShellPage.Playing) Resume(); else Go(investigationReturn);
        }
        private void ActivateInvestigation(int index)
        {
            var investigation = Investigation;
            if (Page == ShellPage.Investigation)
            {
                int topics = investigation.Catalog.topics.Length;
                if (index < topics) { investigationTopic = index; clueScroll = Vector2.zero; return; }
                if (index == topics + investigation.Catalog.stages.Length) { CloseInvestigation(); return; }
                var stage = investigation.Catalog.stages[index - topics];
                investigation.PrepareStoryComparison(readingAtCore, stage.id);
                if (!game.ActiveSkills.LastSaveSucceeded && readingAtCore && stage.id == "contract_comparison") return;
                readingStageId = stage.id; stillIndex = 0;
                readingEntry = investigation.HasRead(stage.id) ? 0 : investigation.NextEntry(stage);
                if (readingAtCore && !investigation.HasRead(stage.id) && stage.entries[readingEntry].id == "record_01_failure")
                    for (int n = 1; n <= 5; n++) { if (investigation.Record.storyFlags.Contains("failure_still:" + n)) stillIndex = n; }
                readingScroll = Vector2.zero; Go(ShellPage.CoreReading); RequestReadingText();
                return;
            }
            var current = investigation.Catalog.Stage(readingStageId);
            if (!ReadingAvailable(current) || index == 2) { CloseInvestigation(); return; }
            if (!game.ActiveSkills.LastSaveSucceeded) return;
            if (current.entries[readingEntry].id == "record_01_failure")
            {
                if (index == 0 && stillIndex > 0) { stillIndex--; readingScroll = Vector2.zero; return; }
                if (index == 1 && stillIndex < 5)
                {
                    stillIndex++; readingScroll = Vector2.zero;
                    string flag = "failure_still:" + stillIndex;
                    if (readingAtCore && !investigation.HasRead(current.id) && !investigation.Record.storyFlags.Contains(flag))
                    { investigation.Record.storyFlags.Add(flag); game.ActiveSkills.Save(); }
                    return;
                }
            }
            if (index == 0) readingEntry = Mathf.Max(0, readingEntry - 1);
            else
            {
                // Opening a page never consumes it. Explicit confirmation persists one entry at a time.
                if (!investigation.HasRead(current.id) && !investigation.State.confirmedEntryIds.Contains(current.entries[readingEntry].id))
                    if (!investigation.TryConfirmEntry(current.id, readingEntry, readingAtCore)) return;
                if (!game.ActiveSkills.LastSaveSucceeded) return;
                if (readingEntry == current.entries.Length - 1) { Go(ShellPage.Investigation); return; }
                readingEntry++;
            }
            stillIndex = 0; readingScroll = Vector2.zero; RequestReadingText();
        }
        private void RequestReadingText()
        {
            var stage = Investigation.Catalog.Stage(readingStageId);
            if (!ReadingAvailable(stage)) return;
            var entry = stage.entries[readingEntry];
            Investigation.RequestText(entry.titleKey); Investigation.RequestText(entry.summaryKey); Investigation.RequestText(entry.BodyKey);
        }
        private void DrawInvestigation()
        {
            var investigation = Investigation;
            var small = new GUIStyle(textStyle) { fontSize = 16 };
            if (Page == ShellPage.Investigation)
            {
                GUI.Label(new Rect(60, 96, 1160, 32), Loc.Get("investigation.scroll_hint", investigation.ProgressLabel), textStyle);
                for (int i = 0; i < investigation.Catalog.topics.Length; i++)
                {
                    var topic = investigation.Catalog.topics[i];
                    Row(i, (i == investigationTopic ? Loc.Get("ui.selected_prefix") : "") + topic.Label + " " + investigation.ClueCount(topic.id) + "/" + investigation.TopicTotal(topic.id), new Rect(60, 140 + 48 * i, 550, 44));
                    if (Page != ShellPage.Investigation) return;
                }
                string topicId = investigation.Catalog.topics[investigationTopic].id;
                float height = ClueBlocks(investigation, topicId, small, false);
                clueScroll = GUI.BeginScrollView(new Rect(60, 290, 550, 290), clueScroll, new Rect(0, 0, 525, height));
                ClueBlocks(investigation, topicId, small, true);
                GUI.EndScrollView();
                for (int i = 0; i < investigation.Catalog.stages.Length; i++)
                {
                    var stage = investigation.Catalog.stages[i];
                    string status = investigation.HasRead(stage.id) ? Loc.Get("investigation.read_again") : investigation.OffersOriginal(readingAtCore, stage.id)
                        ? Loc.Get("investigation.read_core") : investigation.CanRead(stage) ? (readingAtCore ? Loc.Get("investigation.read_available") : Loc.Get("investigation.core_required")) : Loc.Get("investigation.locked_requirements");
                    Row(investigation.Catalog.topics.Length + i, stage.Label + "\n" + status, new Rect(650, 140 + 56 * i, 570, 52));
                    if (Page != ShellPage.Investigation) return;
                }
                Row(InvestigationRows - 1, investigationReturn == ShellPage.Playing ? Loc.Get("ui.back_to_game") : Loc.Get("ui.back_to_skills"), new Rect(200, 620, 880, 46));
            }
            else
            {
                var stage = investigation.Catalog.Stage(readingStageId);
                bool unlocked = ReadingAvailable(stage);
                GUI.Label(new Rect(80, 140, 1120, 36), stage.Label + (unlocked ? Loc.Get("investigation.entry_suffix", (readingEntry + 1), stage.entries.Length) : Loc.Get("ui.locked_suffix")), textStyle);
                float height = ReadingBlocks(stage, unlocked, false);
                readingScroll = GUI.BeginScrollView(new Rect(80, 188, 1120, 396), readingScroll, new Rect(0, 0, 1090, height));
                ReadingBlocks(stage, unlocked, true);
                GUI.EndScrollView();
                if (!unlocked) Row(0, Loc.Get("ui.back_to_investigation"), new Rect(200, 620, 880, 46));
                else
                {
                    bool still = stage.entries[readingEntry].id == "record_01_failure";
                    Row(0, still && stillIndex > 0 ? Loc.Get("area.core_prev") : Loc.Get("investigation.previous_entry"), new Rect(80, 620, 300, 46));
                    if (Page != ShellPage.CoreReading) return;
                    Row(1, still && stillIndex < 5 ? Loc.Get("area.core_next") : investigation.HasRead(stage.id) ? Loc.Get("investigation.next_review") : Loc.Get("investigation.confirm_entry"), new Rect(400, 620, 480, 46));
                    if (Page != ShellPage.CoreReading) return;
                    Row(2, Loc.Get("investigation.close_keep_progress"), new Rect(900, 620, 300, 46));
                }
            }
        }
    }
}
