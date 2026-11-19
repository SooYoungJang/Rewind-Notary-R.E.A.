using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RewindNotary
{
    internal static class AutoTestRuntime
    {
        private static readonly bool requested = Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest") >= 0;
        private static string directory, sandbox;
        private static StreamWriter log;
        private static double sceneStart;
        private static int mainThread;
        private static ConcurrentQueue<(string message, string stack, LogType type)> backgroundLogs;
        internal static AutoTestReport Report { get; private set; }
        internal static double Now => Time.realtimeSinceStartupAsDouble;
        internal static string DirectoryPath => directory;
        internal static int SlotCount => requested && sandbox != null ? 4 : 3;

        // Never fall back to the real root if test setup throws before creating the sandbox.
        internal static string SaveRoot => requested
            ? sandbox ?? throw new InvalidOperationException("Autotest save isolation unavailable.")
            : Application.persistentDataPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Start()
        {
            if (!requested) return;
            try
            {
                Report = AutoTestReport.NewRun(Application.isEditor ? "editor" : "standalone");
#if UNITY_EDITOR
                directory = Environment.GetEnvironmentVariable("REWIND_AUTOTEST_RUN");
#endif
                if (string.IsNullOrEmpty(directory)) directory = AutoTestReport.ReserveDirectory(ProjectDirectory());
                log = new StreamWriter(Path.Combine(directory, "run.log"), true) { AutoFlush = true };
                mainThread = Thread.CurrentThread.ManagedThreadId;
                backgroundLogs = new ConcurrentQueue<(string, string, LogType)>();
                Application.logMessageReceived += OnLog;
                Application.logMessageReceivedThreaded += OnThreadLog;
                string real = Path.GetFullPath(Application.persistentDataPath);
                string temp = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "RewindNotary-" + Guid.NewGuid().ToString("N")));
                if (Inside(temp, real) || Inside(real, temp)) throw new IOException("Overlapping save roots.");
                Report.isolation.realRoot = real;
                Report.isolation.temporaryRoot = temp;
                Report.isolation.beforeSha256 = Fingerprint(real);
                if (Directory.Exists(temp)) throw new IOException("Temporary save root already exists.");
                Directory.CreateDirectory(temp);
                sandbox = temp;
                Application.runInBackground = true;
                SceneManager.sceneLoaded += SceneLoaded;
                sceneStart = Now;
            }
            catch (Exception e)
            {
                Report?.Error("setup", e.Message, e.StackTrace);
                Debug.LogException(e);
                Finish(false);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallDriver()
        {
            if (!requested || sandbox == null) return;
            var host = new GameObject("Autotest");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<AutoTestDriver>();
        }

        internal static string ProjectDirectory()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-autotestProjectPath");
            var dir = new DirectoryInfo(i >= 0 && i + 1 < args.Length ? args[i + 1] : Application.dataPath);
            for (; dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "ProjectSettings", "ProjectVersion.txt"))) return dir.FullName;
            throw new DirectoryNotFoundException("Supply -autotestProjectPath for a player outside the project.");
        }

        private static bool Inside(string path, string root) =>
            (path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar).StartsWith(
                root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        // Only the files owned by SkillSaveStore and SettingsStore. Unity writes its own Player.log.
        private static string Fingerprint(string root)
        {
            string slots = Path.Combine(root, "SkillSystem");
            var files = new List<string>();
            if (Directory.Exists(slots)) files.AddRange(Directory.GetFiles(slots, "*", SearchOption.AllDirectories));
            if (Directory.Exists(root)) files.AddRange(Directory.GetFiles(root, "settings.json*"));
            files.Sort(StringComparer.Ordinal);
            using (var sha = SHA256.Create())
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Directory.Exists(slots));
                foreach (string path in files)
                {
                    writer.Write(path.Substring(root.Length));
                    writer.Write(File.GetLastWriteTimeUtc(path).Ticks);
                    using (var input = File.OpenRead(path)) writer.Write(sha.ComputeHash(input));
                }
                writer.Flush();
                return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }

        internal static double LoadStart() => Report == null ? 0 : Now;
        internal static void LoadEnd(string kind, string id, double start)
        {
            Report?.loads.Add(new LoadSample { kind = kind, id = id, ms = (Now - start) * 1000 });
        }
        private static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            LoadEnd("scene", scene.name, sceneStart);
            if (scene.name != "Main") Report.Error("scene", "Expected Main, loaded " + scene.name);
            SceneManager.sceneLoaded -= SceneLoaded;
        }
        private static void OnThreadLog(string message, string stack, LogType type)
        {
            if (Thread.CurrentThread.ManagedThreadId != mainThread)
                backgroundLogs.Enqueue((message, stack, type));
        }
        internal static void DrainLogs()
        {
            int pending = backgroundLogs?.Count ?? 0;
            for (int i = 0; i < pending && backgroundLogs.TryDequeue(out var entry); i++)
                OnLog(entry.message, entry.stack, entry.type);
        }

        // One known editor callback, with its observed BCL, editor and stack-formatting frames.
        private const string SearchStartup = "UnityEditor.Search.SearchInit.IndexationOnStartup";
        private static readonly HashSet<string> SearchStartupFrames = new HashSet<string>(StringComparer.Ordinal)
        {
            SearchStartup,
            "System.Collections.Generic.List`1.get_Item",
            "System.Collections.Generic.List`1[T].get_Item(System.Int32 index)",
            "UnityEditor.Search.SearchDatabase+<EnumerateAll>d__78.MoveNext",
            "System.Linq.Enumerable.TryGetFirst",
            "System.Linq.Enumerable.TryGetFirst[TSource](System.Collections.Generic.IEnumerable`1[T] source, System.Boolean& found)",
            "System.Linq.Enumerable.First[TSource](System.Collections.Generic.IEnumerable`1[T] source)",
            "UnityEditor.Search.SearchDatabase.GetDefaultSearchDatabase",
            "UnityEditor.EditorApplication.Internal_CallDelayFunctions",
            "UnityEngine.StackTraceUtility:ExtractStringFromExceptionInternal(Object, String&, String&)"
        };

        // Accept the optional "at" prefix and Unity's opaque source location. A project path or
        // other unrecognized suffix must stay visible to the classifier and fail the match.
        private static readonly Regex ManagedFrame = new Regex(
            @"^(?:at[ \t]+)?([^\s()]+)(?:[ \t]*\(([^()]*)\))?(?:[ \t]+\(at <[0-9a-fA-F]{32}>:[0-9]+\))?$");

        internal static bool IsEditorSearchStartupNoise(string stack)
        {
            if (string.IsNullOrWhiteSpace(stack)) return false;
            bool sawStartup = false;
            foreach (string line in stack.Split('\n'))
            {
                string frame = line.Trim();
                if (frame.Length == 0) continue;
                Match match = ManagedFrame.Match(frame);
                if (!match.Success) return false;
                string signature = match.Groups[1].Value;
                string arguments = match.Groups[2].Value;
                if (arguments.Length != 0) signature += "(" + arguments + ")";
                if (!SearchStartupFrames.Contains(signature)) return false;
                if (signature == SearchStartup) sawStartup = true;
            }
            return sawStartup;
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            log.WriteLine(DateTime.UtcNow.ToString("O") + " [" + type + "] " + message);
            if (!string.IsNullOrEmpty(stack)) log.WriteLine(stack);
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
            {
                if (Application.isEditor && IsEditorSearchStartupNoise(stack))
                    Report.Warning(type.ToString(), message, stack);
                else
                    Report.Error(type.ToString(), message, stack);
            }
        }

        internal static string[] ReadPreviousSlotHashes()
        {
            var hashes = new string[3];
            using (var sha = SHA256.Create())
                for (int i = 0; i < hashes.Length; i++)
                {
                    var path = Path.Combine(sandbox, "SkillSystem", "slot-" + (i + 1) + ".json");
                    using (var file = File.OpenRead(path))
                        hashes[i] = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
                }
            return hashes;
        }

        private static bool SavedArrival(string path)
        {
            var saved = JsonUtility.FromJson<CoreRecord>(File.ReadAllText(path));
            var silver = saved?.Get(SkillRules.SilverStitch);
            return saved != null && saved.schemaVersion == CoreRecord.SchemaVersion && !saved.testData &&
                saved.slotId == "slot-1" && saved.lastStageId == "clock" && silver?.acquired == true &&
                silver.validUseCount >= 80 && saved.completedEncounters.Contains("story:mikhail_bout") &&
                saved.storyFlags.Contains("investigation_complete") && saved.storyFlags.Contains("luka_lift_complete");
        }

        internal static void Finish(bool completed)
        {
            int code = 1;
            var audit = Report.isolation;
            try
            {
                if (audit.beforeSha256 != null)
                {
                    audit.afterSha256 = Fingerprint(audit.realRoot);
                    audit.unchanged = audit.beforeSha256 == audit.afterSha256;
                }
                if (!audit.unchanged) Report.Error("isolation", "Real save fingerprint changed or unavailable.");
                if (sandbox != null)
                {
                    string slot = Path.Combine(sandbox, "SkillSystem", "slot-1.json");
                    audit.observedSlotWrite = File.Exists(slot);
                    if (completed)
                    {
                        audit.verifiedFinalSlot = audit.observedSlotWrite && SavedArrival(slot);
                        var third = Path.Combine(sandbox, "SkillSystem", "slot-3.json");
                        if (File.Exists(third))
                        {
                            var save = JsonUtility.FromJson<CoreRecord>(File.ReadAllText(third));
                            Report.clockDistrict.slotReadback = save != null && save.schemaVersion == CoreRecord.SchemaVersion &&
                                !save.testData && save.slotId == "slot-3" && save.lastStageId == "clock" &&
                                save.Get(BenefactorIds.Pendulum).stage >= 3 && save.Get(BenefactorIds.Pendulum).validUseCount >= 80 &&
                                save.Get(SkillRules.SilverStitch).acquired && save.storyFlags.Contains("clock_04_cleared") &&
                                save.storyFlags.Contains("investigation_complete") && save.storyFlags.Contains("scene_floor:clock:clock_rest_09");
                        }
                        var opera = Report.operaDistrict;
                        opera.previousSlotsAfter = ReadPreviousSlotHashes();
                        opera.previousSlotsUnchanged = opera.previousSlotsBefore?.Length == 3 &&
                            string.Join("/", opera.previousSlotsBefore) == string.Join("/", opera.previousSlotsAfter);
                        if (!opera.previousSlotsUnchanged) Report.Error("operaDistrict", "Earlier slot contents changed during slot 4 traversal");
                        var fourth = Path.Combine(sandbox, "SkillSystem", "slot-4.json");
                        if (File.Exists(fourth))
                        {
                            var save = JsonUtility.FromJson<CoreRecord>(File.ReadAllText(fourth));
                            Report.operaDistrict.slotReadback = save != null && save.schemaVersion == CoreRecord.SchemaVersion &&
                                !save.testData && save.slotId == "slot-4" && save.lastStageId == "opera" &&
                                save.Get(BenefactorIds.Waltz).stage == 3 && save.Get(BenefactorIds.Waltz).validUseCount >= 80 &&
                                save.Get(SkillRules.SilverStitch).acquired && save.completedEncounters.Contains("story:valeria_bout") &&
                                save.storyFlags.Contains("investigation_complete") && save.storyFlags.Contains("scene_floor:opera:opera_rest_09");
                        }
                        if (!Report.operaDistrict.slotReadback) Report.Error("operaDistrict", "Missing fourth-slot opera checkpoint on disk.");
                        if (!Report.clockDistrict.slotReadback) Report.Error("clockDistrict", "Missing third-slot clock checkpoint on disk.");
                        if (!audit.verifiedFinalSlot) Report.Error("save", "Missing final district checkpoint on disk.");
                    }
                }
            }
            catch (Exception e) { Report.Error("isolation", e.Message, e.StackTrace); }
            try
            {
                if (sandbox != null)
                {
                    Directory.Delete(sandbox, true);
                    audit.tempRemoved = true;
                }
            }
            catch (Exception e) { Report.Error("cleanup", e.Message, e.StackTrace); }
            try
            {
                Application.logMessageReceivedThreaded -= OnThreadLog;
                DrainLogs();
                if (completed && !Report.pilot.Passed) Report.Error("pilot", "Incomplete pilot route or contact save");
                if (completed && Report.velvetDistrict.run.status != "pass") Report.Error("velvetDistrict", "Incomplete district traversal");
                if (completed && Report.operaDistrict.run.status != "pass") Report.Error("operaDistrict", "Incomplete opera traversal");
                if (completed && Report.clockDistrict.run.status != "pass") Report.Error("clockDistrict", "Incomplete clock traversal");
                if (completed && !Report.feel.Passed) Report.Error("feel", "Required input measurements are incomplete.");
                if (completed && (Report.attackInputs.accepted < 1 || Report.attackInputs.accepted > Report.attackInputs.issued))
                    Report.Error("feel", "Inconsistent bot attack input counts.");
                code = completed && audit.unchanged && audit.verifiedFinalSlot && audit.tempRemoved && Report.errors.Count == 0 ? 0 : 1;
                Report.status = code == 0 ? "pass" : "fail";
                Report.exitCode = code;
                Report.endedUtc = DateTime.UtcNow.ToString("O");
                Debug.Log("AUTOTEST " + Report.status + " " + directory);
                Report.Write(directory);
            }
            catch (Exception e) { code = 1; Debug.LogException(e); }
            finally
            {
                Application.logMessageReceived -= OnLog;
                SceneManager.sceneLoaded -= SceneLoaded;
                sandbox = null;
                log?.Dispose();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.Exit(code);
#else
                Application.Quit(code);
#endif
            }
        }
    }
}
