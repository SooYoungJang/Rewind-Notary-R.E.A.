using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed class PaintSurface2D : MonoBehaviour
    {
        public static readonly List<PaintSurface2D> All = new List<PaintSurface2D>();
        public bool Live { get; private set; }
        public bool Wall { get; private set; }
        public Bounds Bounds => floor.bounds;
        private BoxCollider2D floor;
        private CanvasCarveSkill owner;
        private CombatMotor2D player;
        private float until;
        private float returnFadeUntil;
        private bool supported, credit, retiring;
        private Sprite sprite;
        private SpriteRenderer visual;
        internal string StationId { get; private set; }
        private float stationX;
        internal void SetBrush(Sprite brush, string station, float center)
        {
            visual.sprite = brush;
            visual.size = Wall ? new Vector2(floor.size.y, floor.size.x) : floor.size;
            visual.transform.localRotation = Quaternion.Euler(0, 0, Wall ? 90 : 0);
            visual.color = Color.white;
            StationId = station;
            stationX = center;
        }
        public void Initialize(CanvasCarveSkill skill, CombatMotor2D actor, Vector2 size, bool wall, float seconds)
        {
            owner = skill; player = actor; Wall = wall; Live = credit = true; until = Time.time + seconds;
            gameObject.layer = LevelBuilder.GroundLayer;
            floor = gameObject.AddComponent<BoxCollider2D>(); floor.size = size; floor.autoTiling = false;
            if (!wall)
            {
                var effector = gameObject.AddComponent<PlatformEffector2D>(); effector.useOneWay = true;
                floor.usedByEffector = true;
            }
            sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2);
            var art = new GameObject("Brush"); art.transform.SetParent(transform, false);
            visual = art.AddComponent<SpriteRenderer>(); visual.sprite = sprite; visual.sortingOrder = 6;
            visual.drawMode = SpriteDrawMode.Sliced; visual.size = size; visual.color = Color.cyan;
        }
        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }
        private bool Supporting()
        {
            if (Wall || player == null || player.Health.Dead || floor == null) return false;
            Vector2 feet = player.transform.position;
            return feet.x >= Bounds.min.x - SkillGeometry.SupportTolerance && feet.x <= Bounds.max.x + SkillGeometry.SupportTolerance
                && Mathf.Abs(feet.y - Bounds.max.y) <= SkillGeometry.SupportTolerance;
        }
        private void Update()
        {
            if (!GameInput.GameplayEnabled) return;
            if (returnFadeUntil > 0)
            {
                float left = returnFadeUntil - Time.time;
                visual.color = new Color(0.2f, 0.8f, 1f, Mathf.Clamp01(left / 3f));
                if (left <= 0) { Remove(); }
                return;
            }

            if (retiring) { if (!Supporting()) Remove(); return; }
            if (!Live) return;
            if (Time.time >= until) { Retire(); return; }
            if (!supported && credit && Supporting() && player.Grounded && floor.IsTouching(player.GetComponent<Collider2D>()))
            {
                if (StationId != null && Mathf.Abs(player.Body.position.x - stationX) > .70f) return;
                supported = true;
                owner.ReportEffect((StationId ?? name) + ":support");
            }
        }
        public bool BlockProjectile(SkillProjectile2D shot)
        {
            if (!Live || !shot.Block()) return false;
            if (credit) owner.ReportEffect(name + ":projectile_block");
            return true;
        }
        public void BeginReturnFade()
        { Live = credit = false; retiring = false; returnFadeUntil = Time.time + 3f; }
        public void Retire()
        {
            if (returnFadeUntil > 0) return;
            Live = credit = false;
            retiring = Supporting();
            if (retiring) visual.color = new Color(0.2f, 0.8f, 1f, 0.4f);
            else Remove();
        }
        public void Remove() { Live = credit = false; gameObject.SetActive(false); Destroy(gameObject); }
        private void OnDestroy() { if (sprite != null) Destroy(sprite); }
    }
}
