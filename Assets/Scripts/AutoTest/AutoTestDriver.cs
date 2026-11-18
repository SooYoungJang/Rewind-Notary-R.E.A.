using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RewindNotary
{
    [DefaultExecutionOrder(-1000)]
    internal sealed class AutoTestDriver : MonoBehaviour
    {
        private readonly BotKeyboard keyboard = new BotKeyboard();
        private readonly List<double> frameMs = new List<double>();
        private FirstDepartureBot bot;
        private AutoTestReport report;
        private StepResult step;
        private SkillSession observed;
        private Health health;
        private BoutAttempt attempt;
        private int boutId, index, lastShotFrame = -1;
        private string lastShot, progress;
        private double started, entered, lastFrame, lastProgress, nextProbe, drainStarted;
        private Vector2 lastPosition;
        private bool finishing, allPassed, exited, bossWon;

        private void Awake()
        {
            report = AutoTestRuntime.Report;
            if (report == null) { enabled = false; return; }
            bot = new FirstDepartureBot(keyboard);
            GameInputDevice.Source = keyboard;
            started = AutoTestRuntime.Now;
        }

        private void Update()
        {
            if (exited) return;
            keyboard.NextFrame();
            try
            {
                AutoTestRuntime.DrainLogs();
                if (finishing) { Drain(); return; }
                bot.Locate();
                if (step == null) { Begin(); return; }
                double now = AutoTestRuntime.Now;
                if (now > lastFrame) frameMs.Add((now - lastFrame) * 1000);
                lastFrame = now;
                ObserveSession();
                ObserveBout();
                if (report.errors.Count != 0) { Stop("Unity or harness error"); return; }
                if (now - entered > FirstDepartureBot.Limits[index]) { Stop("step deadline"); return; }
                if (bot.Passed((PlaytestStep)index, bossWon))
                {
                    EndStep("pass", "");
                    if (++index == report.steps.Count) { StartDrain(true); return; }
                    Begin();
                    return;
                }
                if (now >= nextProbe)
                {
                    Probe(now);
                    if (now - lastProgress > 18) { Stop("no position or state progress for 18 seconds"); return; }
                }
                bot.Drive((PlaytestStep)index);
            }
            catch (Exception e)
            {
                report.Error("harness", e.Message, e.StackTrace);
                // Cleanup and report publication must still run when observation itself failed.
                if (finishing) { Exit(); return; }
                Stop("exception in scenario driver");
            }
        }

        private void Begin()
        {
            step = report.steps[index];
            step.status = "running";
            step.reason = "";
            progress = null;
            frameMs.Clear();
            entered = lastFrame = lastProgress = nextProbe = AutoTestRuntime.Now;
            Capture("start");
            Debug.Log("AUTOTEST begin " + step.id);
            report.Write(AutoTestRuntime.DirectoryPath);
        }

        private void Probe(double now)
        {
            nextProbe = now + .25;
            var area = bot.Area;
            var p = area?.Rea;
            var target = area?.RouteTarget;
            var position = p == null ? Vector2.zero : p.Body.position;
            string state = string.Join("|", bot.Shell?.Page, bot.Shell?.MenuIndex, area?.Room.id, area?.ActiveRow?.textKey,
                area?.PendingAction, bot.Record?.storyFlags.Count, bot.Record?.Get(SkillRules.SilverStitch)?.validUseCount,
                target?.TargetId, target?.State.Remaining, area?.BoutOpponent?.Health.Current);
            if (state != progress || (position - lastPosition).sqrMagnitude >= .25f)
            {
                lastProgress = now;
                progress = state;
                lastPosition = position;
            }
        }

        private void ObserveSession()
        {
            var next = bot.Session;
            if (ReferenceEquals(next, observed)) return;
            Detach();
            if (next == null) return;
            observed = next;
            string expected = Path.Combine(AutoTestRuntime.SaveRoot, "SkillSystem");
            report.isolation.observedSessionRoot = observed.SaveDirectory;
            if (!string.Equals(expected, observed.SaveDirectory, StringComparison.Ordinal))
                throw new IOException("Skill session did not use the isolated directory.");
            health = bot.Area.Rea.Health;
            health.Damaged += Damaged;
            health.Died += Died;
            observed.Progression.UseBegan += UseBegan;
            observed.Progression.Event += ProgressionEvent;
        }
        private void Detach()
        {
            if (observed != null)
            {
                observed.Progression.UseBegan -= UseBegan;
                observed.Progression.Event -= ProgressionEvent;
            }
            if (health != null) { health.Damaged -= Damaged; health.Died -= Died; }
            observed = null;
            health = null;
        }
        private void Damaged(Vector2 _) { step.hits++; }
        private void Died() { step.deaths++; }
        private void UseBegan(UseContext use) { step.Count(use.SkillId).activations++; }
        private void ProgressionEvent(ProgressionEvent e)
        {
            if (e.kind == "valid_use") step.Count(e.skillId).validUses++;
            if (e.kind == "attempt_reset")
            {
                // Falling uses the retry path directly, without a Health.Died event.
                if (!health.Dead && bot.Area.Rea.transform.position.y < -7f) step.deaths++;
                EndBout("retry");
            }
        }
        private void ObserveBout()
        {
            var bout = bot.Area?.Bout;
            if (attempt != null && (bout == null || bout.GetInstanceID() != boutId))
            {
                bool won = bot.Record?.storyFlags.Contains("mikhail_defeated") == true;
                bossWon |= won;
                EndBout(won ? "victory" : "interrupted");
            }
            if (attempt != null || bout == null) return;
            boutId = bout.GetInstanceID();
            attempt = new BoutAttempt { startSeconds = AutoTestRuntime.Now - started, outcome = "running" };
            report.bossAttempts.Add(attempt);
            report.bossAttemptCount = report.bossAttempts.Count;
            Capture("boss-start");
        }
        private void EndBout(string outcome)
        {
            if (attempt == null) return;
            attempt.seconds = AutoTestRuntime.Now - started - attempt.startSeconds;
            attempt.outcome = outcome;
            report.bossSeconds += attempt.seconds;
            attempt = null;
            Capture("boss-end-" + outcome);
        }

        private void EndStep(string status, string reason)
        {
            step.status = status;
            step.reason = reason;
            step.seconds = AutoTestRuntime.Now - entered;
            step.performance.Measure(frameMs);
            try { step.endState = bot.Snapshot(); }
            catch (Exception e) { report.Error("state-dump", e.Message, e.StackTrace); }
            Debug.Log("AUTOTEST " + status + " " + step.id + " " + reason);
        }
        private void Stop(string reason)
        {
            EndBout("aborted");
            Capture("failure");
            EndStep("fail", reason);
            StartDrain(false);
        }
        private void StartDrain(bool passed)
        {
            allPassed = passed;
            finishing = true;
            drainStarted = AutoTestRuntime.Now;
            Detach();
            report.Write(AutoTestRuntime.DirectoryPath);
        }

        private void Capture(string kind)
        {
            try
            {
                // Multiple labels can refer to the same rendered frame (victory + next step).
                if (lastShotFrame != Time.frameCount)
                {
                    lastShotFrame = Time.frameCount;
                    lastShot = "screenshots/" + report.checkpoints.Count.ToString("D3") + "-" + step.id + "-" + kind + ".png";
                    ScreenCapture.CaptureScreenshot(Path.Combine(AutoTestRuntime.DirectoryPath, lastShot));
                }
                report.checkpoints.Add(new Checkpoint { step = step.id, kind = kind, png = lastShot, seconds = AutoTestRuntime.Now - started });
            }
            catch (Exception e) { report.Error("screenshot", e.Message, e.StackTrace); }
        }

        private static bool CompletePng(string path)
        {
            if (!File.Exists(path)) return false;
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length < 45 || stream.ReadByte() != 137 || stream.ReadByte() != 80 ||
                        stream.ReadByte() != 78 || stream.ReadByte() != 71) return false;
                    stream.Seek(-8, SeekOrigin.End);
                    return stream.ReadByte() == 'I' && stream.ReadByte() == 'E' && stream.ReadByte() == 'N' && stream.ReadByte() == 'D';
                }
            }
            catch (IOException) { return false; }
        }
        private void Drain()
        {
            if (Time.frameCount <= lastShotFrame) return;
            foreach (var shot in report.checkpoints)
            {
                if (CompletePng(Path.Combine(AutoTestRuntime.DirectoryPath, shot.png))) continue;
                if (AutoTestRuntime.Now - drainStarted < 10) return;
                report.Error("screenshot", "Missing or incomplete PNG: " + shot.png);
            }
            Exit();
        }
        private void Exit()
        {
            exited = true;
            try
            {
                // Final autosaves and deferred Unity destruction cannot recreate the removed sandbox.
                if (bot.Shell != null) bot.Shell.enabled = false;
                if (bot.Game != null && bot.Game.ActiveSkills != null)
                {
                    try
                    {
                        if (!bot.Game.FlushProgress()) report.Error("save", "Final isolated checkpoint failed.");
                    }
                    finally { bot.Game.StopStage(); }
                }
            }
            catch (Exception e) { report.Error("shutdown", e.Message, e.StackTrace); }
            AutoTestRuntime.Finish(allPassed);
        }
    }
}
