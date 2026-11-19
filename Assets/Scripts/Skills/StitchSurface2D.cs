using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    // Authored anchors only. This is a world state machine, never a free-air platform spawner.
    public sealed class StitchSurface2D : MonoBehaviour
    {
        public static readonly List<StitchSurface2D> All = new List<StitchSurface2D>();
        public string TargetId { get; private set; }
        public Bounds Bounds => new Bounds(transform.position + Vector3.up * 0.65f, new Vector3(span, 1.6f, 2));
        public bool Installed { get; private set; }
        public bool Available => !Installed && !releasedBridge && !retiring && returnFadeUntil == 0;
        private float span, until, bindSeconds;
        private float returnFadeUntil;
        private bool support, releasedBridge, retiring, supportReported;
        private int stage;
        private BoxCollider2D floor;
        private SpriteRenderer visual;
        private Sprite installedArt, looseArt;
        private CombatMotor2D player, enemy;
        private StitchBinding binding;
        private SkillProgressionService progression;
        private UseContext use;
        public void Initialize(string id, float width, bool cuttableSupport, CombatMotor2D actor, CombatMotor2D target)
        {
            TargetId = id; span = width; support = cuttableSupport; player = actor; enemy = target;
            binding = enemy.GetComponent<StitchBinding>();
            gameObject.layer = LevelBuilder.GroundLayer;
            floor = gameObject.AddComponent<BoxCollider2D>(); floor.size = new Vector2(span, 0.14f); floor.enabled = false;
            installedArt = RoomArt.Picture("stitch-platform-installed");
            looseArt = RoomArt.Picture("stitch-platform-uninstalled-v2");
            var threads = new GameObject("Silver stitch");
            threads.transform.SetParent(transform, false);
            visual = threads.AddComponent<SpriteRenderer>(); visual.sortingOrder = 4;
            RestoreVisual();
        }
        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }
        public bool Install(SkillProgressionService service, UseContext context, SkillStageDefinition profile)
        {
            if (!Available || span > profile.maxSpan) return false;
            progression = service; use = context; stage = context.Stage; bindSeconds = profile.bindSeconds;
            Installed = true; supportReported = false; until = Time.time + profile.lifetime;
            floor.enabled = stage >= 2;
            RestoreVisual();
            if (stage < 2)
            {
                // A cracked support becomes physically stable, even without HP damage.
                floor.enabled = true;
                progression.Apply(use, new CombatEffectResult(EffectTargetKind.TerrainMechanism, TargetId, true));
            }
            return true;
        }
        internal bool SupportingPlayer => Installed && player.Grounded && PlayerOnFloor() &&
            Mathf.Abs(player.Body.position.y - floor.bounds.max.y) <= .10f && floor.IsTouching(player.GetComponent<Collider2D>());
        private bool PlayerOnFloor()
        {
            if (player == null || player.Health.Dead) return false;
            Vector3 delta = player.transform.position - transform.position;
            return Mathf.Abs(delta.x) <= span * 0.5f + 0.4f && delta.y >= -0.35f && delta.y <= 0.4f;
        }
        private void Update()
        {
            if (!GameInput.GameplayEnabled) return;
            if (returnFadeUntil > 0)
            {
                float left = returnFadeUntil - Time.time;
                visual.color = new Color(0.2f, 0.8f, 1f, Mathf.Clamp01(left / 3f));
                if (left <= 0) { returnFadeUntil = 0; floor.enabled = false; RestoreVisual(); }
                return;
            }

            if (retiring && !PlayerOnFloor()) { retiring = false; floor.enabled = false; RestoreVisual(); }
            if (!Installed) return;
            if (Time.time >= until) { Retire(); return; }
            if (stage >= 2)
            {
                if (!supportReported && PlayerOnFloor() && player.Grounded
                    && floor.IsTouching(player.GetComponent<Collider2D>()))
                {
                    supportReported = true;
                    progression.Apply(use, new CombatEffectResult(EffectTargetKind.TerrainMechanism, TargetId + ":support", true));
                }
                if (!enemy.Health.Dead && Bounds.Intersects(enemy.GetComponent<Collider2D>().bounds) && binding.TryBind(bindSeconds))
                    progression.Apply(use, new CombatEffectResult(EffectTargetKind.Enemy, enemy.name + ":joint", true));
            }
        }
        public bool Cut(bool silverMastery)
        {
            if (!Installed) return false;
            // Stage 3 can release a connected support. Keep a standing player's floor in place.
            releasedBridge = support;
            Retire();
            if (releasedBridge) { floor.enabled = true; retiring = false; }
            SkillStroke.Show(transform.position + Vector3.left * span * 0.5f,
                transform.position + Vector3.right * span * 0.5f, silverMastery ? Color.cyan : Color.yellow, 0.25f);
            RestoreVisual();
            return true;
        }
        private void Retire()
        {
            Installed = false; progression?.EndUse(use); use = null;
            retiring = !releasedBridge && PlayerOnFloor();
            floor.enabled = releasedBridge || retiring;
            RestoreVisual();
        }
        public void BeginReturnFade()
        {
            if (!floor.enabled) return;
            progression?.EndUse(use); use = null;
            Installed = releasedBridge = retiring = false; returnFadeUntil = Time.time + 3f;
        }
        public void CancelCredit() { progression?.EndUse(use); }
        public void ResetAttempt()
        {
            returnFadeUntil = 0;
            Installed = releasedBridge = retiring = false; floor.enabled = false;
            progression?.EndUse(use); use = null; RestoreVisual();
        }
        private void RestoreVisual()
        {
            bool taut = Installed || retiring || releasedBridge;
            visual.sprite = taut ? installedArt : looseArt;
            visual.transform.localScale = Vector3.one * (span / visual.sprite.bounds.size.x);
            visual.color = releasedBridge ? new Color(0.8f, 0.65f, 0.3f) : Color.white;
        }
        private void OnDestroy() { progression?.EndUse(use); }
    }
}
