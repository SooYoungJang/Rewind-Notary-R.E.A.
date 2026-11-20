using UnityEngine;

namespace RewindNotary
{
    // Scene-owned: rebuilding Main also releases every source and static reference.
    public sealed class AudioManager : MonoBehaviour
    {
        public enum Sfx { Swing, Hit, Hurt, Jump, Land, BossWarning, BossAttack, BossDefeat, PlayerDown, Parry, Evolution }
        public enum Music { Field, Boss }
        // Values follow the OST track numbers (minus one).
        public enum Ost { Attic, Belvet, CasselPhase2, Title, Janhyang, CasselPhase1,
            Mikhail, Julius, Valeria, Gabriel, Irina, EndingNormal, EndingTrue,
            Forge, Clocktower, Opera, BellTower, Gallery }
        private static readonly string[] OstNames = {
            "track-01-mangak-darakbang-v1", "track-02-belvet-jaedangeori-v1",
            "track-03-cassel-phase2-v1", "track-04-title-theme-v1",
            "track-05-janhyang-boss-v1", "track-06-cassel-phase1-v1",
            "track-07-mikhail-boss-v1", "track-08-julius-boss-v1",
            "track-09-valeria-boss-v1", "track-10-gabriel-boss-v1",
            "track-11-irina-boss-v1", "track-12-ending-normal-v1",
            "track-13-ending-true-v1", "track-14-suro-forge-v1",
            "track-15-clocktower-v1", "track-16-glass-opera-v1",
            "track-17-bell-tower-v1", "track-18-canvas-gallery-v1"
        };
        public struct VoiceHandle { internal int Index, Generation; }
        public const int PoolSize = 12;
        public const float CrossfadeSeconds = 0.8f;
        public static AudioManager Instance { get; private set; }
        [Range(0f, 1f)] public float MasterVolume = 0.8f;
        [Range(0f, 1f)] public float SfxVolume = 0.7f;
        [Range(0f, 1f)] public float MusicVolume = 0.4f;
        private static readonly string[] Names = { "swing", "hit", "hurt", "jump", "land-thud",
            "boss-warning", "boss-attack", "boss-defeat", "player-down", "parry", "evolution" };
        private readonly AudioClip[] clips = new AudioClip[Names.Length];
        private readonly AudioSource[] voices = new AudioSource[PoolSize];
        private readonly int[] generations = new int[PoolSize];
        private readonly float[] started = new float[PoolSize];
        private readonly bool[] gameTimed = new bool[PoolSize];
        private readonly bool[] paused = new bool[PoolSize];
        private readonly bool[] protectedVoice = new bool[PoolSize];
        private AudioSource field, boss;
        private Music requested;
        private float bossBlend;
        // Lazy sources retain playback positions when a fade is reversed. No
        // source is recycled mid-fade, even under rapid A -> B -> C requests.
        private readonly AudioSource[] ostSources = new AudioSource[OstNames.Length];
        private readonly bool[] ostLoadAttempted = new bool[OstNames.Length];
        private readonly float[] ostWeights = new float[OstNames.Length];
        private readonly float[] ostFadeStart = new float[OstNames.Length];
        private int requestedOst = -1;
        private float legacyWeight = 1f, legacyFadeStart = 1f, ostFadeProgress = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            for (int i = 0; i < clips.Length; i++) clips[i] = Load("Audio/Sfx/" + Names[i]);
            for (int i = 0; i < voices.Length; i++) voices[i] = Source("SFX " + i, false);
            field = Source("Field music", true); field.clip = Load("Audio/Bgm/field");
            boss = Source("Boss music", true); boss.clip = Load("Audio/Bgm/boss");
            ApplyMusic();
        }
        private AudioClip Load(string path)
        {
            var clip = Resources.Load<AudioClip>(path);
            if (clip == null) Debug.LogWarning("Missing audio (gameplay continues): " + path, this);
            return clip;
        }
        private AudioSource Source(string label, bool loop)
        {
            var child = new GameObject(label); child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.spatialBlend = 0f;
            source.volume = 0f; source.dopplerLevel = 0f;
            return source;
        }
        public static VoiceHandle Play(Sfx sound)
        {
            if (Instance == null) return default;
            return Instance.PlayVoice(sound);
        }
        private VoiceHandle PlayVoice(Sfx sound)
        {
            var clip = clips[(int)sound];
            if (clip == null) return default;
            int slot = -1;
            for (int i = 0; i < voices.Length; i++)
                if (!voices[i].isPlaying && !paused[i]) { slot = i; break; }
            // Under saturation, steal the oldest ordinary sound; preserve cues/fanfare.
            if (slot < 0)
                for (int i = 0; i < voices.Length; i++)
                    if (!protectedVoice[i] && (slot < 0 || started[i] < started[slot])) slot = i;
            if (slot < 0) return default;
            var source = voices[slot]; source.Stop();
            paused[slot] = false;
            gameTimed[slot] = sound == Sfx.BossWarning;
            protectedVoice[slot] = gameTimed[slot] || sound == Sfx.BossDefeat || sound == Sfx.PlayerDown || sound == Sfx.Evolution;
            source.clip = clip;
            source.volume = MasterVolume * SfxVolume;
            source.pitch = gameTimed[slot] ? Mathf.Clamp(Time.timeScale, 0.01f, 3f) : 1f;
            source.priority = protectedVoice[slot] ? 32 : 128;
            started[slot] = Time.unscaledTime;
            generations[slot]++;
            source.Play();
            return new VoiceHandle { Index = slot, Generation = generations[slot] };
        }
        public static void Stop(VoiceHandle handle)
        {
            if (Instance == null || handle.Generation == 0) return;
            int i = handle.Index;
            if (Instance.generations[i] != handle.Generation) return;
            Instance.voices[i].Stop(); Instance.paused[i] = false;
        }
        public static void SetMusic(Music music)
        {
            if (Instance != null) Instance.requested = music;
        }
        // Opt-in: legacy callers keep their old behavior until a director calls
        // this. Missing/invalid tracks leave the current music untouched.
        // Repeated requests do not restart loops or completed ending one-shots.
        public static bool SetOst(Ost track, bool restart = false)
        {
            return Instance != null && Instance.RequestOst(track, restart);
        }
        private bool RequestOst(Ost track, bool restart)
        {
            int index = (int)track;
            if (index < 0 || index >= OstNames.Length) return false;
            if (requestedOst == index && !restart) return true;
            if (!ostLoadAttempted[index])
            {
                ostLoadAttempted[index] = true;
                var clip = Load("Audio/Bgm/" + OstNames[index]);
                if (clip != null)
                {
                    var source = Source(OstNames[index],
                        track != Ost.EndingNormal && track != Ost.EndingTrue);
                    source.clip = clip;
                    ostSources[index] = source;
                }
            }
            var next = ostSources[index];
            if (next == null) return false;
            // Set gain before Play so a new source cannot flash at full volume.
            next.volume = MasterVolume * MusicVolume * ostWeights[index];
            if (restart) next.Stop();
            if (!next.isPlaying) next.Play();
            BeginOstFade(index);
            return true;
        }
        public static void UseLegacyMusic()
        {
            if (Instance != null && Instance.requestedOst != -1)
                Instance.BeginOstFade(-1);
        }
        private void BeginOstFade(int target)
        {
            requestedOst = target;
            legacyFadeStart = legacyWeight;
            for (int i = 0; i < ostWeights.Length; i++) ostFadeStart[i] = ostWeights[i];
            ostFadeProgress = 0f;
        }
        private void UpdateOst()
        {
            ostFadeProgress = Mathf.Min(1f, ostFadeProgress + Time.unscaledDeltaTime / CrossfadeSeconds);
            legacyWeight = Mathf.Lerp(legacyFadeStart, requestedOst < 0 ? 1f : 0f, ostFadeProgress);
            for (int i = 0; i < ostSources.Length; i++)
            {
                ostWeights[i] = Mathf.Lerp(ostFadeStart[i], requestedOst == i ? 1f : 0f, ostFadeProgress);
                var source = ostSources[i];
                if (source == null) continue;
                source.volume = MasterVolume * MusicVolume * ostWeights[i];
                if (ostFadeProgress >= 1f && requestedOst != i && source.isPlaying) source.Stop();
                // Never auto-Play here: completed ending tracks must stay ended.
            }
        }
        public static void ResetPlayback(bool arena)
        {
            if (Instance == null) return;
            for (int i = 0; i < PoolSize; i++)
            {
                Instance.voices[i].Stop(); Instance.paused[i] = false;
                Instance.generations[i]++;
            }
            SetMusic(arena ? Music.Boss : Music.Field);
        }
        private void Update()
        {
            if (Instance != this) return;
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i].volume = MasterVolume * SfxVolume;
                if (!gameTimed[i]) continue;
                if (Time.timeScale <= 0f)
                {
                    if (voices[i].isPlaying) { voices[i].Pause(); paused[i] = true; }
                }
                else
                {
                    voices[i].pitch = Mathf.Clamp(Time.timeScale, 0.01f, 3f);
                    if (paused[i]) { voices[i].UnPause(); paused[i] = false; }
                }
            }
            UpdateOst();
            bossBlend = Mathf.MoveTowards(bossBlend, requested == Music.Boss ? 1f : 0f,
                Time.unscaledDeltaTime / CrossfadeSeconds);
            ApplyMusic();
        }
        private void ApplyMusic()
        {
            // Apply volume before playback; reversing a fade keeps the currently
            // playing sample positions and the current blend.
            ApplyTrack(field, (1f - bossBlend) * legacyWeight, requested == Music.Field && legacyWeight > 0f);
            ApplyTrack(boss, bossBlend * legacyWeight, requested == Music.Boss && legacyWeight > 0f);
        }
        private void ApplyTrack(AudioSource source, float weight, bool target)
        {
            source.volume = MasterVolume * MusicVolume * weight;
            if (source.clip != null && (weight > 0f || target))
            { if (!source.isPlaying) source.Play(); }
            else if (source.isPlaying) source.Stop();
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
