using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed class CanvasCarveSkill : BenefactorSkill
    {
        public override string SkillId => BenefactorIds.Canvas;
        private readonly List<PaintSurface2D> strokes = new List<PaintSurface2D>();
        private readonly Queue<Echo> echoes = new Queue<Echo>();
        private float echoEnd;
        private int drawn;
        internal float StrokeWidth => profile.reach;
        internal event System.Action<PaintSurface2D> StrokeDrawn;
        private struct Echo { public float at; public Bounds blade; public int damage; public AttackEcho2D.Pose pose; }
        public override void Initialize(SkillSession skills, CombatMotor2D target)
        { base.Initialize(skills, target); motor.BasicStrikeDelivered += QueueEcho; }
        public override bool Activate() => Activate(GameInput.PaintWallHeld);
        public bool Activate(bool paintWall)
        {
            if (!CanAct || Time.time < readyAt) return false;
            bool fresh = !Active;
            if (fresh && !Begin()) return false;
            if (fresh) { drawn = 0; strokes.Clear(); echoEnd = 0; }
            if (drawn >= profile.installationLimit || PaintSurface2D.All.Count >= profile.installationLimit)
            { if (fresh) Finish(); return false; }
            PaintSurface2D first = strokes.Count > 0 && strokes[0] != null && strokes[0].Live ? strokes[0] : null;
            bool wall = use.Stage >= 2 && (first != null ? !first.Wall : paintWall);
            Vector2 size = wall ? new Vector2(SkillGeometry.PaintThickness, profile.reach) : new Vector2(profile.reach, SkillGeometry.PaintThickness);
            Vector2 at = motor.Body.position + (wall
                ? new Vector2(motor.Facing * SkillGeometry.WallOffset, profile.reach * 0.5f + SkillGeometry.PaintGroundOffset)
                : new Vector2(motor.Facing * (profile.reach * 0.5f + SkillGeometry.PaintLead), motor.Grounded ? SkillGeometry.PaintGroundOffset : -SkillGeometry.PaintBelowFeet));
            if (first != null)
                at = wall ? (Vector2)first.transform.position + Vector2.up * (profile.reach * 0.5f - SkillGeometry.PaintThickness * 0.5f)
                    : (Vector2)first.transform.position + Vector2.right * (profile.reach * 0.5f - SkillGeometry.PaintThickness * 0.5f);
            if (!PlacementClear(at, size, first, wall)) { if (fresh) Finish(); return false; }
            var go = new GameObject("paint:" + use.Id + ":" + drawn); go.transform.SetParent(SkillStroke.WorldRoot, false); go.transform.position = at;
            var paint = go.AddComponent<PaintSurface2D>(); paint.Initialize(this, motor, size, wall, profile.lifetime);
            StrokeDrawn?.Invoke(paint);
            strokes.Add(paint); drawn++; readyAt = Time.time + profile.cooldown;
            if (fresh && use.Stage == 3) echoEnd = Time.time + profile.echoSeconds;
            Damage(new Bounds(at, new Vector3(size.x, size.y, 2)), profile.damage);
            if (first != null && first.Wall != paint.Wall && first.Bounds.Intersects(paint.Bounds))
            {
                // Crossing visuals alone are not a valid use: only actual damage/block/support is.
                Damage(SkillGeometry.Area(first.transform.position, profile.burstRadius), profile.burstDamage);
                Stroke(first.Bounds.min, paint.Bounds.max, Color.cyan);
                first.Retire(); paint.Retire();
            }
            return true;
        }
        private bool PlacementClear(Vector2 at, Vector2 size, PaintSurface2D first, bool wall)
        {
            foreach (var hit in Physics2D.OverlapBoxAll(at, size, 0, 1 << LevelBuilder.GroundLayer))
                if (first == null || hit.gameObject != first.gameObject) return false;
            if (wall)
            {
                Bounds box = new Bounds(at, new Vector3(size.x, size.y, 2));
                if (box.Intersects(motor.GetComponent<Collider2D>().bounds) || box.Intersects(enemy.GetComponent<Collider2D>().bounds)) return false;
            }
            return true;
        }
        public void ReportEffect(string id) { if (Active) Credit(id, true); }
        private void QueueEcho(Bounds blade, int damage)
        {
            if (!Active || use.Stage != 3 || Time.time >= echoEnd) return;
            echoes.Enqueue(new Echo { at = Time.time + profile.echoDelay, blade = blade,
                pose = AttackEcho2D.Pose.Capture(motor.Animation.GetComponent<SpriteRenderer>()),
                damage = Mathf.Max(1, Mathf.CeilToInt(damage * SkillGeometry.EchoDamageScale)) });
        }
        private void Update()
        {
            if (!GameInput.GameplayEnabled || !Active) return;
            while (echoes.Count > 0 && Time.time >= echoes.Peek().at)
            {
                var echo = echoes.Dequeue(); AttackEcho2D.Show(echo.pose);
                // Never call Attack/BeginUse/BasicStrikeDelivered: copies cannot copy themselves or credit sword.
                Damage(echo.blade, echo.damage, false); Stroke(echo.blade.min, echo.blade.max, Color.cyan);
            }
            bool live = false;
            foreach (var paint in strokes) if (paint != null && paint.Live) live = true;
            if (!live && Time.time >= echoEnd && echoes.Count == 0) Finish();
        }
        public override void Interrupt()
        {
            echoes.Clear(); echoEnd = 0;
            foreach (var paint in strokes) if (paint != null) paint.Retire();
            base.Interrupt();
        }
        public override void ResetAttempt()
        {
            base.ResetAttempt();
            foreach (var paint in PaintSurface2D.All.ToArray()) if (paint != null) paint.Remove();
            strokes.Clear(); drawn = 0;
        }
        private void OnDestroy() { if (motor != null) motor.BasicStrikeDelivered -= QueueEcho; }
    }
}
