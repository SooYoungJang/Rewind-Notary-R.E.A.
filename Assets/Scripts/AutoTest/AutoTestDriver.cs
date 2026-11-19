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
        private PlayerController2D controller;
        private FeelProbe feel;
        private PilotProbe pilot;
        private VelvetDistrictProbe district;
        private ClockDistrictProbe clock;
        private OperaDistrictProbe opera;
        private int capturedOperaContacts;
        private bool DrivingDistrict => district != null || clock != null || opera != null;
        private BoutAttempt attempt;
        private BenefactorTrialOpponent observedBout;
        private int index, lastShotFrame = -1;
        private string lastShot, progress;
        private double started, entered, lastFrame, lastProgress, nextProbe, drainStarted;
        private const double DistrictLogInterval = 30, DistrictIdleLimit = 120;
        private double nextDistrictLog, lastDistrictProgress;
        private int lastContacts, lastCleared;
        private string lastWaveRoom;
        private Vector2 lastPosition;
        private bool finishing, allPassed, exited, bossWon;

        private void Awake()
        {
            report = AutoTestRuntime.Report;
            if (report == null) { enabled = false; return; }
            bot = new FirstDepartureBot(keyboard, Stop);
            feel = gameObject.AddComponent<FeelProbe>();
            pilot = new PilotProbe(CapturePilot);
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
                if (now >= nextProbe)
                {
                    Probe(now);
                    if (now - lastProgress > 18) { Stop("no position or state progress for 18 seconds"); return; }
                    if (DrivingDistrict && !CheckDistrictProgress(now))
                    { Stop("no district contact, kill or target progress for 120 seconds"); return; }
                }
                if (clock != null)
                {
                    if (report.errors.Count != 0) { Stop("Unity or harness error"); return; }
                    if (now - entered > 1500) { Stop("Clock district deadline"); return; }
                    if (clock.Drive(bot, keyboard, now))
                    {
                        EndStep("pass", "");
                        Detach();
                        clock = null;
                        bot = new FirstDepartureBot(keyboard, Stop);
                        opera = new OperaDistrictProbe(report.operaDistrict, PublishOpera);
                        step = report.operaDistrict.run;
                        StartMeasurement();
                    }
                    return;
                }
                if (opera != null)
                {
                    if (report.errors.Count != 0) { Stop("Unity or harness error"); return; }
                    if (now - entered > 1500) { Stop("Opera district deadline"); return; }
                    if (opera.Drive(bot, keyboard, now)) { EndStep("pass", ""); StartDrain(true); }
                    return;
                }
                if (district != null)
                {
                    if (report.errors.Count != 0) { Stop("Unity or harness error"); return; }
                    if (now - entered > 1500)
                    { Stop("District probe exceeded progress or run deadline"); return; }
                    if (district.Drive(bot, keyboard, now))
                    {
                        EndStep("pass", "");
                        Detach();
                        district = null;
                        bot = new FirstDepartureBot(keyboard, Stop);
                        clock = new ClockDistrictProbe(report.clockDistrict, () => report.Write(AutoTestRuntime.DirectoryPath));
                        step = report.clockDistrict.run;
                        StartMeasurement();
                    }
                    return;
                }
                ObserveBout();
                if (report.errors.Count != 0) { Stop("Unity or harness error"); return; }
                if (!string.IsNullOrEmpty(report.feel.failure)) { Stop(report.feel.failure); return; }
                if (!string.IsNullOrEmpty(report.pilot.failure)) { Stop(report.pilot.failure); return; }
                if (now - entered > FirstDepartureBot.Limits[index]) { Stop("step deadline"); return; }
                if (bot.Passed((PlaytestStep)index, bossWon))
                {
                    if ((PlaytestStep)index == PlaytestStep.Attic && !feel.Passed)
                    { Stop("Attic finished before the input checks passed"); return; }
                    if ((PlaytestStep)index == PlaytestStep.Velvet && !report.pilot.Passed)
                    { Stop("Velvet finished before both pilot traversals passed"); return; }
                    EndStep("pass", "");
                    if (++index == report.steps.Count) { BeginDistrict(); return; }
                    Begin();
                    return;
                }
                bool drivingPilot = (PlaytestStep)index == PlaytestStep.Velvet && pilot.Drive(bot, keyboard, report.pilot);
                if (!drivingPilot && ((PlaytestStep)index != PlaytestStep.Attic || !feel.Drive(bot, keyboard, report.feel)))
                    bot.Drive((PlaytestStep)index);
                if (keyboard.GetKeyDown(KeyCode.J)) report.attackInputs.issued++;
            }
            catch (Exception e)
            {
                report.Error("harness", e.Message, e.StackTrace);
                // Cleanup and report publication must still run when observation itself failed.
                if (finishing) { Exit(); return; }
                Stop("exception in scenario driver: " + e.Message);
            }
        }

        private void PublishOpera()
        {
            var contacts = report.operaDistrict.contacts;
            if (capturedOperaContacts < contacts.Count)
            {
                Capture(contacts[contacts.Count - 1].room);
                capturedOperaContacts = contacts.Count;
            }
            report.Write(AutoTestRuntime.DirectoryPath);
        }

        private void BeginDistrict()
        {
            Detach();
            bot = new FirstDepartureBot(keyboard, Stop);
            district = new VelvetDistrictProbe(report.velvetDistrict, () => report.Write(AutoTestRuntime.DirectoryPath));
            step = report.velvetDistrict.run;
            StartMeasurement();
        }

        private void Begin()
        {
            step = report.steps[index];
            StartMeasurement();
        }
        private void StartMeasurement()
        {
            step.status = "running";
            step.reason = "";
            progress = null;
            frameMs.Clear();
            entered = lastFrame = lastProgress = nextProbe = AutoTestRuntime.Now;
            lastDistrictProgress = entered;
            nextDistrictLog = entered + DistrictLogInterval;
            lastContacts = lastCleared = 0;
            lastWaveRoom = null;
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
                target?.TargetId, target?.State.Remaining, area?.BoutOpponent?.Health.Current,
                clock != null ? bot.Record?.Get(BenefactorIds.Pendulum)?.validUseCount : null,
                opera != null ? bot.Record?.Get(BenefactorIds.Waltz)?.validUseCount : null,
                (clock != null || opera != null) ? area?.UnclearedClockTarget?.State.Remaining : null);
            if (state != progress || (position - lastPosition).sqrMagnitude >= .25f)
            {
                lastProgress = now;
                progress = state;
                lastPosition = position;
            }
        }

        private int DistrictContacts => opera != null ? report.operaDistrict.contacts.Count : clock != null ? report.clockDistrict.contacts.Count : report.velvetDistrict.contacts.Count;

        private bool CheckDistrictProgress(double now)
        {
            var area = bot.Area;
            string room = area?.Room?.id;
            int cleared = area?.ClearedWaveTargets ?? 0;
            int contacts = DistrictContacts;
            if (contacts > lastContacts || (room == lastWaveRoom && cleared > lastCleared)) lastDistrictProgress = now;
            lastWaveRoom = room;
            lastCleared = cleared;
            lastContacts = contacts;
            if (now - lastDistrictProgress >= DistrictIdleLimit) return false;
            if (now >= nextDistrictLog)
            {
                Debug.Log("AUTOTEST district " + DistrictState(now));
                nextDistrictLog = now + DistrictLogInterval;
            }
            return true;
        }

        private string DistrictState(double now)
        {
            var area = bot.Area;
            var player = area?.Rea;
            var target = area?.DistrictWaveTarget;
            var foe = area?.BoutOpponent;
            var feet = player == null ? "-" : player.Body.position.ToString("F2");
            int remaining = area == null ? 0 : area.WaveTargetCount - area.ClearedWaveTargets;
            string support = (clock != null || opera != null) && area != null ? " support=" + area.ClockSupportDescription() : "";
            return $"{step.id} elapsed={now - entered:F1}s idle={now - lastDistrictProgress:F1}s room={area?.Room?.id ?? "-"} " +
                $"feet={feet} grounded={player?.Grounded} row={area?.ActiveRow?.textKey ?? "-"} action={area?.PendingAction ?? "-"} " +
                $"remainingTargets={remaining} waveTarget={target?.TargetId ?? "-"} segments={target?.State.Remaining ?? 0} contacts={DistrictContacts}" + support +
                $" hp={player?.Health.Current}/{player?.Health.Maximum} bossHp={foe?.Health.Current}/{foe?.Health.Maximum} bossFeet={foe?.Body.position.ToString("F2") ?? "-"}";
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
            controller = health.GetComponent<PlayerController2D>();
            if (!DrivingDistrict) controller.AttackInputAccepted += AttackAccepted;
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
            if (controller != null) controller.AttackInputAccepted -= AttackAccepted;
            controller = null;
            observed = null;
            health = null;
        }
        private void Damaged(Vector2 _)
        {
            step.hits++;
            pilot.ObserveHit(bot.Area.Rea, report.pilot);
            if (DrivingDistrict) Debug.Log("AUTOTEST district hit " + DistrictState(AutoTestRuntime.Now));
        }
        private void AttackAccepted() { report.attackInputs.accepted++; }
        private void Died() { step.deaths++; }
        private void UseBegan(UseContext use)
        {
            step.Count(use.SkillId).activations++;
            if (clock != null) bot.ObserveClockCast(use);
            pilot.ObserveUse();
        }
        private void ProgressionEvent(ProgressionEvent e)
        {
            bot.ObserveSilver(e);
            district?.ObserveUse(e, bot.Area.Room.id);
            clock?.ObserveUse(e, bot.Area.Room.id);
            opera?.ObserveUse(e, bot.Area.Room.id);
            if (opera != null && e.kind == "attempt_reset")
                report.Error("operaDistrict", "Attempt reset during continuous opera traversal");
            if (clock != null && e.kind == "attempt_reset")
                report.Error("clockDistrict", "Attempt reset during continuous clock traversal");
            if (e.kind == "valid_use")
            {
                step.Count(e.skillId).validUses++;
                // The progression service emits this only for an applied effect on a named target.
                if (DrivingDistrict) lastDistrictProgress = AutoTestRuntime.Now;
            }
            if (district != null && e.kind == "attempt_reset")
                report.Error("velvetDistrict", "Attempt reset during continuous checkpoint traversal");
            if (e.kind == "attempt_reset")
            {
                if ((PlaytestStep)index == PlaytestStep.Velvet) pilot.ObserveRetry(report.pilot);
                // Falling uses the retry path directly, without a Health.Died event.
                if (!health.Dead && bot.Area.Rea.transform.position.y < bot.Area.Room.FallY) step.deaths++;
                EndBout("retry");
            }
        }
        private void ObserveBout()
        {
            var bout = bot.Area?.Bout;
            if (attempt != null && (bout == null || bout != observedBout))
            {
                bool won = bot.Record?.storyFlags.Contains("mikhail_defeated") == true;
                bossWon |= won;
                EndBout(won ? "victory" : "interrupted");
            }
            if (attempt != null || bout == null) return;
            observedBout = bout;
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
            observedBout = null;
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
            if (DrivingDistrict)
            {
                try { reason += " | " + DistrictState(AutoTestRuntime.Now); }
                catch (Exception e) { report.Error("district-state", e.Message, e.StackTrace); }
            }
            feel.Abort(reason);
            if ((PlaytestStep)index == PlaytestStep.Velvet)
                pilot.Abort(report.pilot, reason);
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

        private void CapturePilot(string kind)
        {
            Capture(kind);
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
