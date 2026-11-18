using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed class SkillProjectile2D : MonoBehaviour
    {
        public static readonly List<SkillProjectile2D> All = new List<SkillProjectile2D>();
        public bool Hostile { get; private set; } = true;
        public Bounds Bounds => SkillGeometry.Area(transform.position, SkillGeometry.ProjectileRadius);
        private CombatMotor2D player, enemy;
        private TemporalActor2D temporal;
        private Vector2 velocity;
        private float remaining = SkillGeometry.ProjectileLifetime;
        private bool spent;
        private int damage = SkillGeometry.ProjectileDamage;
        private float flightSpeed = SkillGeometry.ProjectileSpeed;
        public void ConfigureTrialShot(Vector2 flight, int hitDamage, float lifetime)
        { velocity = flight; flightSpeed = flight.magnitude; damage = hitDamage; remaining = lifetime; }
        private Sprite sprite;
        private SpriteRenderer visual;
        public void Initialize(CombatMotor2D actor, CombatMotor2D target)
        {
            player = actor; enemy = target;
            velocity = Vector2.left * SkillGeometry.ProjectileSpeed;
            temporal = gameObject.AddComponent<TemporalActor2D>();
            sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2);
            visual = gameObject.AddComponent<SpriteRenderer>(); visual.sprite = sprite; visual.sortingOrder = 8;
            visual.color = Color.magenta; transform.localScale = Vector3.one * SkillGeometry.ProjectileRadius * 2;
        }
        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }
        public bool Deflect(bool reflect)
        {
            if (spent || !Hostile) return false;
            if (!reflect) { Consume(); return true; }
            Hostile = false; temporal.Release();
            velocity = ((Vector2)(enemy.transform.position + Vector3.up - transform.position)).normalized * flightSpeed;
            visual.color = Color.cyan; return true;
        }
        public bool Block() { if (spent || !Hostile) return false; Consume(); return true; }
        private void Consume() { spent = true; gameObject.SetActive(false); Destroy(gameObject); }
        private void Update()
        {
            if (!GameInput.GameplayEnabled || spent || temporal.Frozen) return;
            remaining -= Time.deltaTime;
            if (remaining <= 0) { Consume(); return; }
            Vector2 start = transform.position;
            Vector2 delta = velocity * Time.deltaTime;
            Vector2 end = start + delta;
            Bounds swept = new Bounds((start + end) * 0.5f,
                new Vector3(Mathf.Abs(delta.x) + SkillGeometry.ProjectileRadius * 2, Mathf.Abs(delta.y) + SkillGeometry.ProjectileRadius * 2, 2));
            // Resolve authored paint barriers before player contact, including long render frames.
            foreach (var paint in PaintSurface2D.All)
                if (Hostile && paint.Live && swept.Intersects(paint.Bounds) && paint.BlockProjectile(this)) return;
            if (Physics2D.Linecast(start, end, 1 << LevelBuilder.GroundLayer).collider != null) { Consume(); return; }
            transform.position = end;
            var target = Hostile ? player : enemy;
            if (!target.Health.Dead && swept.Intersects(target.GetComponent<Collider2D>().bounds))
            { target.Health.Hit(Hostile ? damage : FinaleCombat.Scale(player.Skills, damage), start); Consume(); }
        }
        private void OnDestroy() { if (sprite != null) Destroy(sprite); }
        public static void ClearAll()
        { foreach (var shot in All.ToArray()) if (shot != null) shot.Consume(); }
    }
}
