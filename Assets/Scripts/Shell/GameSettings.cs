using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace RewindNotary
{
    [Serializable]
    public sealed class GameSettings
    {
        public int schemaVersion = 1;
        public float bgmVolume = 0.4f, sfxVolume = 0.7f;
        public bool screenShake = true;
        public int stageOverride;
        public string language;
        public int displayMode;
        public bool vsync = true;
        public string keyJump = "";
        public string keyAttack = "";
        public string keyCast = "";
        public string keyDash = "";
        public string keyParry = "";
        public string keyInteract = "";
        public string keyMap = "";

        public void Validate()
        {
            if (schemaVersion != 1 || float.IsNaN(bgmVolume) || float.IsNaN(sfxVolume)
                || bgmVolume < 0 || bgmVolume > 1 || sfxVolume < 0 || sfxVolume > 1
                || stageOverride < 0 || stageOverride > 3) throw new InvalidDataException(Loc.Get("settings.invalid_values"));
            language = Loc.Normalize(language);
            displayMode = Mathf.Clamp(displayMode, 0, 3);
        }

        public void ResetKeybinds()
        {
            keyJump = keyAttack = keyCast = keyDash = keyParry = keyInteract = keyMap = "";
        }
    }

    public sealed class SettingsStore
    {
        [Serializable] private sealed class Header { public int schemaVersion; }
        public GameSettings Value { get; private set; } = new GameSettings();
        private Func<string> error;
        public string Error => error?.Invoke();
        private readonly string path;
        private bool blocked;

        public SettingsStore(string directory)
        {
            path = Path.Combine(directory, "settings.json");
            Value.language = Loc.DefaultLanguage(Application.systemLanguage);
        }

        private GameSettings Read(string file)
        {
            string json = File.ReadAllText(file);
            var header = JsonUtility.FromJson<Header>(json);
            if (header != null && header.schemaVersion > 1) blocked = true;
            if (header == null || header.schemaVersion != 1) throw new InvalidDataException(Loc.Get("settings.unsupported_version"));
            var value = JsonUtility.FromJson<GameSettings>(json);
            if (value == null) throw new InvalidDataException(Loc.Get("settings.empty_file"));
            value.Validate();
            return value;
        }

        public void Load()
        {
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return;
            try { Value = Read(path); }
            catch (Exception)
            {
                if (blocked) { error = () => Loc.Get("settings.defaults_preserved", Loc.Get("settings.unsupported_version")); return; }
                try
                {
                    Value = Read(path + ".bak");
                    if (File.Exists(path)) File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.Ticks, false);
                    File.Copy(path + ".bak", path, true);
                    error = () => Loc.Get("settings.backup_restored");
                }
                catch (Exception) { blocked = true; error = () => Loc.Get("settings.recovery_failed"); }
            }
        }

        public bool Save()
        {
            if (blocked) return false;
            try
            {
                Value.Validate(); Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + ".tmp";
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(Value, true));
                    stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
                }
                Read(temp);
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
                error = null; return true;
            }
            catch (Exception e) { error = () => Loc.Get("settings.save_failed", e.Message); return false; }
        }

        public void Apply()
        {
            Value.language = Loc.Normalize(Value.language);
            Loc.SetLanguage(Value.language);
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.MusicVolume = Value.bgmVolume;
                AudioManager.Instance.SfxVolume = Value.sfxVolume;
            }
            ImpactCameraShake.Enabled = Value.screenShake;
            QualitySettings.vSyncCount = Value.vsync ? 1 : 0;
            GameInput.ApplyBindings(Value);

            if (!Application.isEditor && AutoTestRuntime.Report == null)
            {
                switch (Value.displayMode)
                {
                    case 0:
                        Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
                        break;
                    case 1:
                        Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
                        break;
                    case 2:
                        Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
                        break;
                    case 3:
                        Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
                        break;
                }
            }
        }

        public static string DisplayModeLabel(int mode)
        {
            switch (mode)
            {
                case 1: return "1280 × 720 (Windowed)";
                case 2: return "1600 × 900 (Windowed)";
                case 3: return "1920 × 1080 (Windowed)";
                default: return "Fullscreen Window";
            }
        }
    }
}
