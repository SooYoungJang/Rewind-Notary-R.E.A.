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
        public void Validate()
        {
            if (schemaVersion != 1 || float.IsNaN(bgmVolume) || float.IsNaN(sfxVolume)
                || bgmVolume < 0 || bgmVolume > 1 || sfxVolume < 0 || sfxVolume > 1
                || stageOverride < 0 || stageOverride > 3) throw new InvalidDataException("설정 값 또는 버전이 올바르지 않습니다.");
        }
    }
    public sealed class SettingsStore
    {
        [Serializable] private sealed class Header { public int schemaVersion; }
        public GameSettings Value { get; private set; } = new GameSettings();
        public string Error { get; private set; }
        private readonly string path;
        private bool blocked;
        public SettingsStore(string directory) { path = Path.Combine(directory, "settings.json"); }
        private GameSettings Read(string file)
        {
            string json = File.ReadAllText(file);
            var header = JsonUtility.FromJson<Header>(json);
            if (header != null && header.schemaVersion > 1) blocked = true;
            if (header == null || header.schemaVersion != 1) throw new InvalidDataException("지원하지 않는 설정 버전입니다.");
            var value = JsonUtility.FromJson<GameSettings>(json);
            if (value == null) throw new InvalidDataException("설정 파일이 비어 있습니다.");
            value.Validate(); return value;
        }
        public void Load()
        {
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return;
            try { Value = Read(path); }
            catch (Exception first)
            {
                if (blocked) { Error = first.Message + " 원본을 보존하고 기본 설정을 사용합니다."; return; }
                try
                {
                    Value = Read(path + ".bak");
                    if (File.Exists(path)) File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.Ticks, false);
                    File.Copy(path + ".bak", path, true);
                    Error = "설정 백업을 복구했습니다.";
                }
                catch (Exception) { blocked = true; Error = "설정을 복구하지 못했습니다. 원본을 보존하고 기본 설정을 사용합니다."; }
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
                Error = null; return true;
            }
            catch (Exception e) { Error = "설정 저장 실패: " + e.Message; return false; }
        }
        public void Apply()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.MusicVolume = Value.bgmVolume;
                AudioManager.Instance.SfxVolume = Value.sfxVolume;
            }
            ImpactCameraShake.Enabled = Value.screenShake;
        }
    }
}
