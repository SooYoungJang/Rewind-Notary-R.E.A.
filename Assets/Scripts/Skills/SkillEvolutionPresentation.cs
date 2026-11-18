using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    // Observes committed progression events; never grants skills, increments uses or saves.
    public sealed class SkillEvolutionPresentation : MonoBehaviour
    {
        public const float BannerSeconds = 3.2f, FadeSeconds = 0.35f;
        private sealed class Banner
        {
            public Func<string> Title, Detail;
            public bool Preview, FeedbackPlayed;
            public Color Tint;
        }
        private readonly List<Banner> pending = new List<Banner>();
        private readonly HashSet<string> delivered = new HashSet<string>();
        private SkillSession session;
        private Transform actor, effectRoot;
        private Font font;
        private GUIStyle titleStyle, detailStyle;
        private Banner active;
        private SkillEvolutionBurst burst;
        private AudioManager.VoiceHandle voice;
        private int previewStage, feedbackFrame = -1;
        private float age;
        public void Initialize(SkillSession skills, Transform player, Transform root, Font uiFont)
        {
            Detach(); session = skills; actor = player; effectRoot = root; font = uiFont;
            previewStage = session.PreviewStage;
            session.Progression.Event += OnProgression;
        }
        private void OnProgression(ProgressionEvent result)
        {
            if (result.kind == "attempt_reset" || result.kind == "debug_seed") { Clear(); return; }
            if (result.kind == "debug_preview")
            {
                int next = session.PreviewStage;
                if (next == previewStage) return;
                previewStage = next;
                pending.RemoveAll(item => item.Preview);
                if (active != null && active.Preview) active = null;
                Enqueue(new Banner { Preview = true, Tint = new Color(0.65f, 0.8f, 1f),
                    Title = () => next == 0 ? Loc.Get("evolution.preview_off") : Loc.Get("evolution.preview_title", SkillStatusView.StageLabel(next)),
                    Detail = () => next == 0 ? Loc.Get("evolution.preview_off_detail")
                        : Loc.Get("evolution.preview_detail") });
                return;
            }
            if (!SkillStatusView.IsEvolution(result, session.Progression.Catalog)
                || !delivered.Add(result.useId)) return;
            var definition = session.Progression.Catalog.Get(result.skillId);
            Enqueue(new Banner { Title = () => Loc.Get("evolution.title", (result.testData ? Loc.Get("evolution.test_prefix") : ""), definition.DisplayName, SkillStatusView.StageLabel(result.stage)),
                Detail = () => definition.Profile(result.stage).Name + " — " + SkillStatusView.Describe(result.skillId, result.stage),
                Tint = result.stage == 3 ? new Color(1f, 0.78f, 0.35f) : new Color(0.35f, 1f, 0.9f) });
        }
        private void Enqueue(Banner banner)
        {
            // Natural boundaries play feedback immediately, even when a prior banner is still visible.
            if (!banner.Preview && GameInput.GameplayEnabled) Feedback(banner);
            pending.Add(banner);
            // Defer preview until Update so F5/F6 can discard a same-frame temporary override change.
            if (!banner.Preview && active == null && GameInput.GameplayEnabled) Next();
        }
        private void Feedback(Banner banner)
        {
            banner.FeedbackPlayed = true;
            // Several skills can cross in one frame: one short stop/voice/visual is enough.
            if (feedbackFrame == Time.frameCount) return;
            feedbackFrame = Time.frameCount;
            ImpactFeedback.SwordHit(); // Existing 0.05x / 65 ms, with the existing pause/restoration owner.
            AudioManager.Stop(voice); voice = AudioManager.Play(AudioManager.Sfx.Evolution);
            if (burst != null) Destroy(burst.gameObject);
            if (actor != null) burst = SkillEvolutionBurst.Show(effectRoot, actor.position + Vector3.up, banner.Tint);
        }
        private void Next()
        {
            if (pending.Count == 0) { active = null; return; }
            active = pending[0]; pending.RemoveAt(0); age = 0;
            if (!active.FeedbackPlayed) Feedback(active);
        }
        private void Update()
        {
            // Menu time does not consume a banner, or play a deferred override cue behind the menu.
            if (!GameInput.GameplayEnabled) return;
            if (active == null) { Next(); return; }
            age += Time.unscaledDeltaTime;
            if (age >= BannerSeconds) Next();
        }
        private void Clear()
        {
            pending.Clear(); active = null; age = 0;
            AudioManager.Stop(voice); voice = default;
            if (burst != null) Destroy(burst.gameObject);
            burst = null;
        }
        private void Detach()
        {
            if (session != null) session.Progression.Event -= OnProgression;
            Clear(); delivered.Clear(); session = null;
        }
        private void OnDisable() { Detach(); }
        private void OnDestroy() { Detach(); }
        private void OnGUI()
        {
            if (active == null || !GameInput.GameplayEnabled) return;
            var previousMatrix = GUI.matrix; var previousColor = GUI.color; int previousDepth = GUI.depth;
            GUI.depth = -10;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2,
                (Screen.height - 720 * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 23, fontStyle = FontStyle.Bold, wordWrap = true };
                detailStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 17, wordWrap = true };
                titleStyle.normal.textColor = detailStyle.normal.textColor = Color.white;
            }
            float opacity = Mathf.Clamp01((BannerSeconds - age) / FadeSeconds);
            GUI.color = new Color(0.025f, 0.055f, 0.09f, 0.94f * opacity);
            GUI.DrawTexture(new Rect(475, 140, 780, 122), Texture2D.whiteTexture);
            GUI.color = new Color(active.Tint.r, active.Tint.g, active.Tint.b, opacity);
            GUI.DrawTexture(new Rect(475, 140, 5, 122), Texture2D.whiteTexture);
            GUI.Label(new Rect(495, 150, 740, 58), active.Title(), titleStyle);
            GUI.color = new Color(1, 1, 1, opacity);
            GUI.Label(new Rect(495, 209, 740, 49), active.Detail(), detailStyle);
            // Draw/Label only: no focus, button, modal or input ownership.
            GUI.matrix = previousMatrix; GUI.color = previousColor; GUI.depth = previousDepth;
        }
    }
}
