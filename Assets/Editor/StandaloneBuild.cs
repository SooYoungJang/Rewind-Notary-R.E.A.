using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RewindNotary.Editor
{
    // Batch entry points only. Launch with the matching -buildTarget before project import.
    public static class StandaloneBuild
    {
        private const string RequiredUnityVersion = "6000.0.65f1";
        private const string LedgerHeader = "| 완료 UTC | 빌드 ID | 버전 | Unity | 대상 | BuildPipeline 결과 | 종료 코드 | STATIC-VALIDATION passed | STATIC SHA-256 | 결과물 경로 | 결과물 bytes | 비고 |";
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        [Serializable]
        private sealed class Validation
        {
            public int passed = -1;
            public string[] errors;
            public string[] checks;
        }

        [Serializable]
        private sealed class Result
        {
            public int schemaVersion = 1;
            public string buildId, version = "unknown", unityVersion, target;
            public string startedUtc, completedUtc, runDirectory, outputDirectory, playerPath;
            public string staticValidationSha256 = "unknown";
            public int staticValidationPassed = -1;
            public long artifactBytes = -1;
            public string buildResult = "NotStarted";
            public bool buildPipelineInvoked;
            public int exitCode = 1;
            public string scriptingBackend = "Mono2x", macArchitecture;
            public string[] scenes = new string[0];
            public string[] autotestArguments;
            public string notes = "";
        }

        public static void BuildWindows64() { Run(BuildTarget.StandaloneWindows64, "Windows64", "RewindNotary.exe"); }
        public static void BuildMacOS() { Run(BuildTarget.StandaloneOSX, "macOS", "RewindNotary.app"); }

        private static void Run(BuildTarget target, string targetName, string playerName)
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Use these entry points with -batchmode.");

            string project = Directory.GetParent(Application.dataPath).FullName;
            string ledger = Path.Combine(project, "BUILD-LOG.md");
            var result = new Result { target = targetName, unityVersion = Application.unityVersion, startedUtc = UtcNow() };
            result.autotestArguments = new[] { "-autotest", "-autotestProjectPath", project };
            string oldVersion = PlayerSettings.bundleVersion;
            var oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            bool settingsChanged = false, ownsOutput = false, canWriteResult = false;

            try
            {
                result.buildId = BuildId();
                result.runDirectory = Path.Combine(project, "Builds", targetName, result.buildId);
                result.outputDirectory = Path.Combine(result.runDirectory, "Player");
                result.playerPath = Path.Combine(result.outputDirectory, playerName);
                if (File.Exists(Path.Combine(result.runDirectory, "build-result.json")) || Directory.Exists(result.outputDirectory))
                    throw new IOException("Build ID already used; choose a new -rewindBuildId.");
                Directory.CreateDirectory(result.runDirectory);
                canWriteResult = true;
                string version = File.ReadAllText(Path.Combine(project, "VERSION"), Utf8).Trim();
                if (!Regex.IsMatch(version, @"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z"))
                    throw new InvalidDataException("VERSION must contain MAJOR.MINOR.PATCH.");
                result.version = version;
                if (Application.unityVersion != RequiredUnityVersion)
                    throw new InvalidOperationException("Required Unity: " + RequiredUnityVersion);
                if (!File.ReadAllText(Path.Combine(project, "ProjectSettings/ProjectVersion.txt"), Utf8)
                    .Contains("m_EditorVersion: " + RequiredUnityVersion))
                    throw new InvalidDataException("ProjectVersion.txt does not match required Unity.");
                if (!File.ReadAllText(ledger, Utf8).Contains(LedgerHeader))
                    throw new InvalidDataException("BUILD-LOG.md header is missing or changed.");

                byte[] validationBytes = File.ReadAllBytes(Path.Combine(project, "STATIC-VALIDATION.json"));
                File.WriteAllBytes(Path.Combine(result.runDirectory, "STATIC-VALIDATION.json"), validationBytes);
                using (var sha = SHA256.Create())
                    result.staticValidationSha256 = BitConverter.ToString(sha.ComputeHash(validationBytes)).Replace("-", "").ToLowerInvariant();
                var validation = JsonUtility.FromJson<Validation>(Utf8.GetString(validationBytes).TrimStart('\uFEFF'));
                if (validation == null || validation.passed <= 0 || validation.errors == null || validation.errors.Length != 0
                    || validation.checks == null || validation.checks.Length != validation.passed)
                    throw new InvalidDataException("STATIC-VALIDATION.json is missing a successful, consistent validation result.");
                result.staticValidationPassed = validation.passed;

                result.scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
                if (result.scenes.Length == 0 || result.scenes.Distinct().Count() != result.scenes.Length)
                    throw new InvalidDataException("Enable at least one unique scene in EditorBuildSettings.");
                foreach (string scene in result.scenes)
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null)
                        throw new FileNotFoundException("Enabled build scene is missing: " + scene);
                if (EditorUserBuildSettings.activeBuildTarget != target)
                    throw new InvalidOperationException("Launch with matching -buildTarget win64 or osxuniversal.");
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                    throw new InvalidOperationException("Install the target's Build Support module for this Unity version.");

                settingsChanged = true;
                PlayerSettings.bundleVersion = result.version;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                result.macArchitecture = EditorUserBuildSettings.GetPlatformSettings("Standalone", "OSXUniversal", "Architecture");
                Directory.CreateDirectory(result.outputDirectory);
                ownsOutput = true;
                var options = new BuildPlayerOptions
                {
                    scenes = result.scenes,
                    locationPathName = result.playerPath,
                    target = target,
                    targetGroup = BuildTargetGroup.Standalone,
                    subtarget = (int)StandaloneBuildSubtarget.Player,
                    options = BuildOptions.None
                };
                result.buildPipelineInvoked = true;
                result.buildResult = "Unknown";
                BuildReport report = BuildPipeline.BuildPlayer(options);
                result.buildResult = report.summary.result.ToString();
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("BuildPipeline result: " + result.buildResult + "; errors: " + report.summary.totalErrors);
                if (!File.Exists(result.playerPath) && !Directory.Exists(result.playerPath))
                    throw new IOException("BuildPipeline reported success but the player path is missing.");
                result.exitCode = 0;
            }
            catch (Exception exception)
            {
                Fail(result, exception, 1);
            }
            finally
            {
                try
                {
                    if (settingsChanged)
                    {
                        PlayerSettings.bundleVersion = oldVersion;
                        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, oldBackend);
                        AssetDatabase.SaveAssets();
                    }
                }
                catch (Exception exception) { Fail(result, exception, 2); }
                try
                {
                    if (ownsOutput)
                        result.artifactBytes = Directory.EnumerateFiles(result.outputDirectory, "*", SearchOption.AllDirectories)
                            .Sum(path => new FileInfo(path).Length);
                }
                catch (Exception exception) { Fail(result, exception, 2); }
                result.completedUtc = UtcNow();

                // Write JSON first; append exactly one row. A ledger error changes the exit code and JSON.
                try
                {
                    if (canWriteResult) WriteResult(result);
                    AppendLedger(ledger, result);
                }
                catch (Exception exception)
                {
                    Fail(result, exception, 2);
                    try { if (canWriteResult) WriteResult(result); }
                    catch (Exception writeException) { Debug.LogException(writeException); }
                }
                Debug.Log("REWIND_BUILD_RESULT " + JsonUtility.ToJson(result));
                EditorApplication.Exit(result.exitCode);
            }
        }

        private static string BuildId()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-rewindBuildId");
            string id = index < 0 ? DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")
                : index + 1 < args.Length ? args[index + 1] : "";
            if (!Regex.IsMatch(id, @"\A[A-Za-z0-9][A-Za-z0-9_-]{0,79}\z"))
                throw new ArgumentException("-rewindBuildId must be 1-80 ASCII letters, digits, underscores or hyphens.");
            return id;
        }

        private static string UtcNow() { return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture); }

        private static void Fail(Result result, Exception exception, int code)
        {
            result.exitCode = code;
            result.notes += (result.notes.Length == 0 ? "" : "; ") + exception.Message;
            Debug.LogException(exception);
        }

        private static void WriteResult(Result result)
        {
            File.WriteAllText(Path.Combine(result.runDirectory, "build-result.json"), JsonUtility.ToJson(result, true) + "\n", Utf8);
        }

        private static string Cell(string value)
        {
            return string.IsNullOrEmpty(value) ? "unknown" : value.Replace("&", "&amp;").Replace("|", "&#124;")
                .Replace("\r", " ").Replace("\n", " ").Replace("<", "&lt;").Replace(">", "&gt;").Replace("`", "&#96;");
        }

        private static void AppendLedger(string path, Result result)
        {
            if (!File.ReadAllText(path, Utf8).Contains(LedgerHeader))
                throw new InvalidDataException("BUILD-LOG.md header is missing or changed.");
            string[] cells = { result.completedUtc, result.buildId, result.version, result.unityVersion, result.target,
                result.buildResult, result.exitCode.ToString(CultureInfo.InvariantCulture),
                result.staticValidationPassed < 0 ? "unknown" : result.staticValidationPassed.ToString(CultureInfo.InvariantCulture),
                result.staticValidationSha256, result.playerPath,
                result.artifactBytes < 0 ? "unknown" : result.artifactBytes.ToString(CultureInfo.InvariantCulture),
                string.IsNullOrEmpty(result.notes) ? "automatic" : result.notes };
            File.AppendAllText(path, "\n| " + string.Join(" | ", cells.Select(Cell)) + " |\n", Utf8);
        }
    }
}
