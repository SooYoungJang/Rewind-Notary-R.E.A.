using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using UnityEngine;

namespace RewindNotary
{
    [Serializable] public sealed class AutoTestReport
    {
        public int schemaVersion = 2, exitCode = 1;
        public string status = "running", startedUtc = DateTime.UtcNow.ToString("O"), endedUtc, mode, unityVersion;
        public List<StepResult> steps = new List<StepResult>();
        public List<RunError> errors = new List<RunError>();
        public List<RunError> warnings = new List<RunError>();
        public List<Checkpoint> checkpoints = new List<Checkpoint>();
        public List<LoadSample> loads = new List<LoadSample>();
        public List<BoutAttempt> bossAttempts = new List<BoutAttempt>();
        public int bossAttemptCount;
        public double bossSeconds;
        public SaveAudit isolation = new SaveAudit();
        public FeelResults feel = new FeelResults();
        public PilotResults pilot = new PilotResults();
        public VelvetDistrictResult velvetDistrict = new VelvetDistrictResult();
        public ClockDistrictResult clockDistrict = new ClockDistrictResult();
        public AttackInputCounts attackInputs = new AttackInputCounts();

        public static AutoTestReport NewRun(string mode)
        {
            var report = new AutoTestReport { mode = mode, unityVersion = Application.unityVersion };
            foreach (PlaytestStep id in Enum.GetValues(typeof(PlaytestStep)))
                report.steps.Add(new StepResult { id = id.ToString(), expected = FirstDepartureBot.Expectations[(int)id] });
            return report;
        }

        public static string ReserveDirectory(string project)
        {
            string root = Path.Combine(project, "TestResults");
            Directory.CreateDirectory(root);
            var now = DateTime.UtcNow;
            for (int i = 0; i < 60; i++)
            {
                string dir = Path.Combine(root, "autotest-" + now.AddSeconds(i).ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
                if (Directory.Exists(dir)) continue;
                Directory.CreateDirectory(dir);
                try { using (new FileStream(Path.Combine(dir, "run.log"), FileMode.CreateNew, FileAccess.Write)) {} }
                catch (IOException) when (File.Exists(Path.Combine(dir, "run.log"))) { continue; }
                Directory.CreateDirectory(Path.Combine(dir, "screenshots"));
                return dir;
            }
            throw new IOException("Unable to reserve a unique autotest result directory.");
        }

        public void Write(string directory)
        {
            const int maxRetries = 5;
            const int retryDelayMs = 50;
            attackInputs.unaccepted = attackInputs.issued - attackInputs.accepted;
            string path = Path.Combine(directory, "report.json");
            string temp = path + ".tmp";
            string json = JsonUtility.ToJson(this, true) + "\n";
            bool tempWritten = false;
            for (int retry = 0; ; retry++)
            {
                try
                {
                    // A locked destination can be retried with the already completed temp file.
                    if (!tempWritten)
                    {
                        File.WriteAllText(temp, json);
                        tempWritten = true;
                    }
                    if (File.Exists(path)) File.Replace(temp, path, null);
                    else File.Move(temp, path);
                    return;
                }
                catch (IOException) when (retry < maxRetries)
                {
                    Thread.Sleep(retryDelayMs);
                }
            }
        }

        public void Error(string type, string message, string stack = "") => errors.Add(LogEntry(type, message, stack));
        internal void Warning(string type, string message, string stack) => warnings.Add(LogEntry(type, message, stack));

        private static RunError LogEntry(string type, string message, string stack) =>
            new RunError { utc = DateTime.UtcNow.ToString("O"), type = type, message = message, stack = stack };
    }

    [Serializable] public sealed class FeelResults
    {
        public StopMeasurement groundStop = new StopMeasurement(), airStop = new StopMeasurement();
        public AttackMeasurement stationaryAttack = new AttackMeasurement();
        public string failure;
        internal bool Passed => groundStop.status == "pass" && airStop.status == "pass" && stationaryAttack.status == "pass";
    }

    [Serializable] public sealed class StopMeasurement
    {
        public string status = "skipped";
        public float releaseSpeed, speed, seconds;
        public int physicsTicks;
    }

    [Serializable] public sealed class AttackMeasurement
    {
        public string status = "skipped";
        public int framesSinceTap = -1, attackStartedCount;
        public bool attacking;
    }

    [Serializable] public sealed class AttackInputCounts { public int issued, accepted, unaccepted; }

    [Serializable] public sealed class StepResult
    {
        public string id, expected, status = "skipped", reason = "prerequisite incomplete";
        public double seconds;
        public int deaths, hits;
        public List<SkillCount> skills = new List<SkillCount>();
        public FrameStats performance = new FrameStats();
        public WorldState endState;

        internal SkillCount Count(string id)
        {
            var count = skills.Find(s => s.id == id);
            if (count != null) return count;
            count = new SkillCount { id = id };
            skills.Add(count);
            return count;
        }
    }

    [Serializable] public sealed class WorldState
    {
        public string page, message, slot, stage, room, row, action, target, saveError;
        public int menuIndex, hp, bossHp, targetSegments, silverUses;
        public float x, y;
        public bool grounded, safeFloor, silverOwned;
        public List<string> flags, encounters;
    }

    [Serializable] public sealed class FrameStats
    {
        public int frames;
        public double meanMs, meanFps, slowest1PercentMeanMs, onePercentLowFps;

        internal void Measure(List<double> frameMs)
        {
            frames = frameMs.Count;
            if (frames == 0) return;
            frameMs.Sort();
            int tail = (int)Math.Ceiling(frames * .01);
            double total = 0, slow = 0;
            for (int i = 0; i < frames; i++)
            {
                total += frameMs[i];
                if (i >= frames - tail) slow += frameMs[i];
            }
            meanMs = total / frames;
            slowest1PercentMeanMs = slow / tail;
            meanFps = 1000 / meanMs;
            onePercentLowFps = 1000 / slowest1PercentMeanMs;
        }
    }

    [Serializable] public sealed class SkillCount { public string id; public int activations, validUses; }
    [Serializable] public sealed class RunError { public string utc, type, message, stack; }
    [Serializable] public sealed class Checkpoint { public string step, kind, png; public double seconds; }
    [Serializable] public sealed class LoadSample { public string kind, id; public double ms; }
    [Serializable] public sealed class BoutAttempt { public double startSeconds, seconds; public string outcome; }
    [Serializable] public sealed class SaveAudit
    {
        public string realRoot, temporaryRoot, observedSessionRoot, beforeSha256, afterSha256;
        public bool observedSlotWrite, unchanged, tempRemoved, verifiedFinalSlot;
    }
}
