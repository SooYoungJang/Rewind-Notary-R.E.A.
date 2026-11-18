using System;
using System.Diagnostics;
using System.IO;
using RewindNotary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

// -quit belongs to the invoking editor. Returning from executeMethod before the
// Play loop finishes can terminate it before even the first Update.
public static class AutoTestRunner
{
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use -batchmode for AutoTestRunner.Run.");
        string project = Directory.GetParent(Application.dataPath).FullName;
        string output = AutoTestReport.ReserveDirectory(project);
        string scratch = Path.Combine(Path.GetTempPath(), "RewindNotary-editor-" + Guid.NewGuid().ToString("N"));
        string clone = Path.Combine(scratch, "Project");
        string nativeLog = Path.Combine(scratch, "editor.log");
        var result = AutoTestReport.NewRun("editor");
        int code = 1;
        try
        {
            result.Write(output);
            foreach (string folder in new[] { "Assets", "Packages", "ProjectSettings" })
                CopyTree(Path.Combine(project, folder), Path.Combine(clone, folder));
            // Separate project lock and a rendered Game view for IMGUI checkpoints.
            var start = new ProcessStartInfo(EditorApplication.applicationPath) {
                UseShellExecute = false, WorkingDirectory = clone,
                Arguments = "-projectPath \"" + clone + "\" -logFile \"" + nativeLog +
                    "\" -executeMethod AutoTestRunner.Play -autotest"
            };
            start.EnvironmentVariables["REWIND_AUTOTEST_RUN"] = output;
            using (var worker = Process.Start(start))
            {
                if (!worker.WaitForExit(45 * 60 * 1000))
                {
                    worker.Kill();
                    worker.WaitForExit(10000);
                    throw new TimeoutException("Editor import or Play Mode worker exceeded 45 minutes.");
                }
                result = JsonUtility.FromJson<AutoTestReport>(File.ReadAllText(Path.Combine(output, "report.json")))
                    ?? throw new InvalidDataException("Worker report is empty.");
                if (result.status == "running" || (worker.ExitCode != 0 && result.exitCode == 0))
                    throw new InvalidOperationException("Worker exited before successful finalization (code " + worker.ExitCode + ").");
                code = worker.ExitCode == 0 && result.exitCode == 0 && result.status == "pass" ? 0 : 1;
            }
        }
        catch (Exception e)
        {
            // Preserve any already-published step evidence, including on a native crash.
            string path = Path.Combine(output, "report.json");
            if (File.Exists(path))
            {
                try { result = JsonUtility.FromJson<AutoTestReport>(File.ReadAllText(path)) ?? result; }
                catch (Exception readError) { Debug.LogException(readError); }
            }
            result.Error("editor", e.Message, e.StackTrace);
            foreach (var step in result.steps)
                if (step.status == "running") { step.status = "fail"; step.reason = e.Message; }
            if (result.velvetDistrict.run.status == "running")
            { result.velvetDistrict.run.status = "fail"; result.velvetDistrict.run.reason = e.Message; }
            if (result.clockDistrict.run.status == "running")
            { result.clockDistrict.run.status = "fail"; result.clockDistrict.run.reason = e.Message; }
            result.status = "fail";
            result.exitCode = 1;
            result.endedUtc = DateTime.UtcNow.ToString("O");
            result.Write(output);
            File.AppendAllText(Path.Combine(output, "run.log"), e + Environment.NewLine);
            Debug.LogException(e);
        }
        finally
        {
            try { AppendEditorLog(nativeLog, output); }
            catch (Exception e)
            {
                code = 1;
                result.Error("editor-log", e.Message, e.StackTrace);
                result.exitCode = 1;
                result.status = "fail";
                result.Write(output);
            }
            try { if (Directory.Exists(scratch)) Directory.Delete(scratch, true); }
            catch (IOException e) { Debug.LogWarning("Worker cleanup: " + e.Message); }
            Debug.Log("AUTOTEST_RESULT " + output + " exit=" + code);
            EditorApplication.Exit(code);
        }
    }

    private static void AppendEditorLog(string path, string output)
    {
        if (!File.Exists(path)) return;
        using (var dest = new FileStream(Path.Combine(output, "run.log"), FileMode.Append))
        using (var src = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            src.CopyTo(dest);
    }

    private static void CopyTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (string dir in Directory.GetDirectories(source))
            CopyTree(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }

    public static void Play()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest") < 0)
            throw new InvalidOperationException("Worker requires -autotest.");
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        EditorApplication.EnterPlaymode();
    }
}
