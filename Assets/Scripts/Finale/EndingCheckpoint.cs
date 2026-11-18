using System;
using UnityEngine;

namespace RewindNotary
{
    // The menu and bank use a logical lift ID independently of the physical room checkpoint.
    internal static class EndingCheckpoint
    {
        internal const string Lift = "court_lift", Basic = "ending_basic", True = "ending_true";
        private const string BankA = "cassel_previous:a:", BankB = "cassel_previous:b:";

        internal static string Marker(CoreRecord record) => record.storyFlags.Find(f =>
            f.StartsWith(BankA, StringComparison.Ordinal) || f.StartsWith(BankB, StringComparison.Ordinal));
        internal static bool IsBankA(string marker) => marker.StartsWith(BankA, StringComparison.Ordinal);

        internal static CoreRecord BeforeBattle(CoreRecord record)
        {
            var previous = JsonUtility.FromJson<CoreRecord>(JsonUtility.ToJson(record));
            previous.lastStageId = Lift;
            previous.finalBattleAttempt = new FinalBattleAttempt();
            return previous;
        }
        internal static void Remember(CoreRecord record, FinalBattleAttempt attempt)
        {
            var ending = FinaleEndingEvaluator.Evaluate(attempt);
            if (ending == FinaleEnding.None) return;
            string flag = ending == FinaleEnding.Basic ? Basic : True;
            if (!record.storyFlags.Contains(flag)) record.storyFlags.Add(flag);
        }
        internal static void Publish(CoreRecord record, CoreRecord previous)
        {
            string old = Marker(record);
            string marker = (old != null && IsBankA(old) ? BankB : BankA) + record.finalBattleAttempt.attemptId;
            previous.lastFinalBattleAttempt = record.finalBattleAttempt;
            previous.storyFlags.Remove(old);
            previous.storyFlags.Add(marker);
            record.storyFlags.Remove(old);
            record.storyFlags.Add(marker);
            record.lastStageId = Lift;
        }
        internal static void SetFloor(CoreRecord record, string stage, string floor)
        {
            record.lastStageId = stage;
            string prefix = "scene_floor:" + stage + ":";
            record.storyFlags.RemoveAll(f => f.StartsWith(prefix, StringComparison.Ordinal));
            record.storyFlags.Add(prefix + floor);
        }

        internal static void StartScenes(CoreRecord record)
        {
            var ending = FinaleEndingEvaluator.Evaluate(record.finalBattleAttempt);
            if (ending == FinaleEnding.None) return;
            string branch = "endingScene:" + ending;
            foreach (var row in RepresentativeScript.Load().rows)
                if (Array.IndexOf(row.entryCondition, branch) >= 0)
                    record.storyFlags.Remove(row.Receipt);
            record.storyFlags.RemoveAll(f => f.StartsWith("endingScene:", StringComparison.Ordinal) || f == "resume_at_lift");
            record.storyFlags.Add(branch);
            SetFloor(record, "janhyang", "court_rooftop_floor");
        }

        internal static void Resume(CoreRecord current, CoreRecord previous)
        {
            // The live slot owns lasting progression, including later training and spent charges.
            current.lastStageId = previous.lastStageId;
            if (FinaleEndingEvaluator.Evaluate(current.finalBattleAttempt) != FinaleEnding.None)
                current.lastFinalBattleAttempt = current.finalBattleAttempt;
            else if (FinaleEndingEvaluator.Evaluate(current.lastFinalBattleAttempt) == FinaleEnding.None)
                current.lastFinalBattleAttempt = previous.lastFinalBattleAttempt;
            current.finalBattleAttempt = previous.finalBattleAttempt;
            current.storyFlags.RemoveAll(f => f == "resume_lift_01_read" || f == "resume_lift_02_read" ||
                f == "resume_route_chosen" || f == "resume_at_lift" || f.StartsWith("endingScene:", StringComparison.Ordinal));
            current.storyFlags.Add("resume_at_lift");
        }
    }
}
