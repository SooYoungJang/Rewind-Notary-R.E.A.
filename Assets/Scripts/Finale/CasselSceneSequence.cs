using System;
using System.Collections.Generic;

namespace RewindNotary
{
    // The combat service owns R, B and settlement. This observer only acknowledges
    // authored lines and their inputs, and starts fresh on each combat attempt.
    internal sealed class CasselSceneSequence
    {
        private readonly SkillSession session;
        private readonly StoryRow[] rows;
        private readonly Queue<StoryRow> modals = new Queue<StoryRow>();
        private readonly HashSet<string> observed = new HashSet<string>();
        internal StoryRow Modal => modals.Count == 0 ? null : modals.Peek();
        internal StoryRow ConfirmMenu { get; }
        private CoreRecord Record => session.Progression.Record;

        internal CasselSceneSequence(SkillSession session)
        {
            this.session = session;
            rows = Array.FindAll(RepresentativeScript.Load().rows, r =>
                r.room == "court_04_tribunal" && !string.IsNullOrEmpty(r.presentation));
            ConfirmMenu = Array.Find(rows, r => r.presentation == "menu");
        }

        internal void Begin(bool defeated)
        {
            modals.Clear();
            observed.Clear();
            session.ClearStoryNotices();
            if (defeated) Signal("defeat");
            Signal("attempt");
        }

        private bool Put(string flag)
        {
            if (Record.storyFlags.Contains(flag)) return false;
            Record.storyFlags.Add(flag);
            return true;
        }

        internal void Signal(string input) => Observe(input, session.EnqueueStoryNotice);

        internal void Returned(string skill, int count)
        {
            // Keep both real-time lines together even when an earlier combat cue is on screen.
            var banner = new List<StoryRow>(2);
            Observe("return:" + skill + (count <= 2 ? ":early" : ":late"), banner.Add);
            Observe("r" + count, banner.Add);
            session.ShowReturnNotice(banner);
        }

        private void Observe(string input, Action<StoryRow> showNotice)
        {
            if (!observed.Add(input)) return;
            bool changed = false;
            foreach (var row in rows)
            {
                if (row.entryCondition[0] == input)
                {
                    if (row.presentation == "modal") modals.Enqueue(row);
                    else if (row.presentation == "notice")
                    {
                        showNotice(row);
                        changed |= Put(row.ReadFlag);
                    }
                }
                if (row.requiredPlayerAction[0] == input)
                {
                    changed |= Put(row.completionFlag);
                    if (row.presentation == "menu") changed |= Put(row.ReadFlag);
                }
            }
            if (changed) session.Save();
        }

        internal bool Confirm()
        {
            var row = Modal;
            if (row == null || !session.LastSaveSucceeded) return false;
            modals.Dequeue();
            Put(row.ReadFlag);
            if (row.DialogueOnly) Put(row.completionFlag);
            session.Save();
            if (row.stepId == "03_cassel_vessel") Signal("opening");
            return true;
        }
    }
}
