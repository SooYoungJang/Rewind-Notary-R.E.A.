using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    // Enemy-only silhouettes of the player profiles. No SkillSession/UseContext,
    // progression callbacks, player displacement, global clock or player installations.
    public sealed class JanhyangMimic
    {
        private readonly CombatMotor2D motor, player;
        private readonly JanhyangDefinition data;
        private readonly List<GameObject> visuals = new List<GameObject>();
        private readonly HashSet<int> delivered = new HashSet<int>();
        private Material material;
        private SkillStageDefinition profile;
        private JanhyangTechnique power;
        private string skill;
        private Vector2 origin, previous;
        private int facing, stage, movementSegment;
        private float elapsed, duration;
        private bool active, contact, landedBurst;
        public bool Active => active;
        public bool DeliveringContact { get; private set; }
        public JanhyangMimic(CombatMotor2D owner, CombatMotor2D target, JanhyangDefinition definition)
        { motor = owner; player = target; data = definition; }

        public void Telegraph(string id, SkillStageDefinition shape, Vector2 aim)
        {
            ClearVisuals();
            Vector2 from = motor.Body.position + Vector2.up;
            float reach = id == SkillRules.BasicSword ? 1.75f + shape.waveReach : shape.reach;
            int direction = aim.x < from.x ? -1 : 1;
            Line(from, from + Vector2.right * direction * reach, 0.045f);
        }
        public void Begin(string id, SkillStageDefinition shape)
        {
            Cancel();
            skill = id; profile = shape; stage = shape.stage; power = data.Find(id);
            origin = previous = motor.Body.position; facing = motor.Facing;
            elapsed = 0; movementSegment = -1; contact = landedBurst = false; active = true;
            duration = id == SkillRules.BasicSword ? SwordDuration()
                : id == BenefactorIds.Pendulum ? shape.duration * 2 + 0.55f
                : id == BenefactorIds.Waltz ? shape.lifetime
                : id == BenefactorIds.Canvas ? 0.7f + (stage == 3 ? shape.echoDelay : 0)
                : 0.65f;
            if (skill == BenefactorIds.Waltz) motor.Jump(shape.jumpSpeed);
        }
        private float SwordDuration()
        {
            float value = 0;
            for (int i = 0; i < profile.comboLimit; i++) value += data.sword[i].duration;
            return value;
        }
        public bool Tick(float dt)
        {
            if (!active) return false;
            elapsed += dt;
            switch (skill)
            {
                case SkillRules.BasicSword: Sword(); break;
                case SkillRules.SilverStitch: Stitch(); break;
                case BenefactorIds.Pendulum: Pendulum(dt); break;
                case BenefactorIds.Waltz: Waltz(); break;
                case BenefactorIds.Forge: Forge(); break;
                case BenefactorIds.Canvas: Canvas(); break;
            }
            // Health.Hit may synchronously trigger the player's forge counter and cancel us.
            if (!active) return false;
            if (elapsed >= duration) { active = false; StopMotion(); return true; }
            return false;
        }
        public void Animate()
        {
            if (!active) return;
            if (skill == SkillRules.BasicSword)
            {
                float local = elapsed;
                for (int i = 0; i < profile.comboLimit; i++)
                {
                    var beat = data.sword[i];
                    if (local < beat.duration) { motor.Animation.PlayAttack(local, beat.strikeTime, beat.duration); return; }
                    local -= beat.duration;
                }
            }
            else motor.Animation.PlayAttack(elapsed % 0.4f, 0.16f, 0.4f);
        }
        private void Sword()
        {
            float start = 0;
            for (int i = 0; i < profile.comboLimit; i++)
            {
                if (!active) return;
                var beat = data.sword[i];
                if (elapsed >= start && elapsed < start + beat.duration)
                    Move(elapsed - start < profile.stepSeconds ? facing * profile.stepSpeed : 0);
                float wave = i == profile.comboLimit - 1 ? profile.waveReach : 0;
                Bounds area = new Bounds(motor.transform.position + new Vector3(facing * (beat.reach + wave * 0.5f), 1, 0),
                    new Vector3(beat.width + wave, beat.height, 2));
                if (Pulse(i, start + beat.strikeTime, area, i == profile.comboLimit - 1) && profile.afterimageSeconds > 0) Ghost();
                start += beat.duration;
            }
        }
        private void Stitch()
        {
            Move(0);
            Bounds area = Forward(profile.reach);
            if (Pulse(0, 0.16f, area, false))
            {
                Line(area.min, area.max, 0.06f);
                if (stage >= 2) Line(new Vector2(area.min.x, area.max.y), new Vector2(area.max.x, area.min.y), 0.06f);
            }
            if (stage == 3) Pulse(1, 0.5f, area, true);
        }
        private void Pendulum(float dt)
        {
            float returnAt = profile.duration + 0.35f;
            Vector2 at = motor.Body.position;
            // Sample the PREVIOUS physics traversal first, including the final dash step.
            if (movementSegment >= 0)
            {
                Bounds path = new Bounds((previous + at) * 0.5f + Vector2.up,
                    new Vector3(Mathf.Abs(previous.x - at.x) + 1, Mathf.Abs(previous.y - at.y) + 2, 2));
                // One contact attempt per traversal, including a defended contact.
                if (!delivered.Contains(movementSegment) && path.Intersects(player.GetComponent<Collider2D>().bounds))
                { delivered.Add(movementSegment); Hit(path, movementSegment == 1); }
                if (!active) return;
                Line(previous + Vector2.up, at + Vector2.up, 0.065f);
            }
            previous = at;
            float start = elapsed - dt;
            bool outbound = start < profile.duration;
            bool returning = stage >= 2 && start >= returnAt && start < returnAt + profile.duration;
            movementSegment = outbound ? 0 : returning ? 1 : -1;
            float left = outbound ? profile.duration - start : returning ? returnAt + profile.duration - start : 0;
            Move(movementSegment < 0 ? 0 : (returning ? -facing : facing) * profile.reach / profile.duration * Mathf.Min(1, left / dt), dt);
            if (stage == 3)
            {
                Bounds trace = new Bounds(origin + new Vector2(facing * profile.reach * 0.5f, 1),
                    new Vector3(profile.reach, 2, 2));
                Pulse(2, returnAt + profile.duration + 0.1f, trace, true);
            }
        }
        private void Waltz()
        {
            Move(0);
            Bounds spin = SkillGeometry.Area(motor.Body.position + Vector2.up, profile.reach);
            if (Pulse(0, profile.duration * 0.5f, spin, false)) Ring(motor.Body.position + Vector2.up, profile.reach);
            if (!active) return;
            if (stage >= 2 && contact && elapsed >= profile.duration && delivered.Add(4))
                motor.Jump(profile.jumpSpeed);
            if (stage >= 2) Pulse(1, profile.duration * 1.5f, spin, false);
            if (!active) return;
            if (stage == 3 && elapsed > profile.duration * 2)
                motor.Body.linearVelocity = new Vector2(0, -profile.plungeSpeed);
            if (elapsed > profile.duration && motor.Grounded)
            {
                if (stage == 3 && !landedBurst)
                { landedBurst = true; Pulse(2, 0, SkillGeometry.Area(motor.Body.position, profile.burstRadius), true); }
                duration = elapsed;
            }
        }
        private void Forge()
        {
            Move(0);
            Bounds area = stage == 3 ? SkillGeometry.Area(motor.Body.position + Vector2.up, profile.burstRadius) : Forward(profile.reach);
            if (Pulse(0, stage == 3 ? profile.counterWindow : 0.16f, area, stage == 3) && stage >= 2)
            {
                for (int i = 0; i < profile.markLimit; i++)
                    Line(motor.Body.position + new Vector2(i * SkillGeometry.MarkSpacing, 2.2f),
                        motor.Body.position + new Vector2(i * SkillGeometry.MarkSpacing, 2.5f), 0.06f);
                if (stage == 3) Ring(motor.Body.position + Vector2.up, profile.burstRadius);
            }
        }
        private void Canvas()
        {
            Move(0);
            Bounds floor = new Bounds(origin + new Vector2(facing * profile.reach * 0.5f, SkillGeometry.PaintGroundOffset),
                new Vector3(profile.reach, SkillGeometry.PaintThickness, 2));
            Bounds wall = new Bounds(origin + new Vector2(facing * profile.reach, profile.reach * 0.5f),
                new Vector3(SkillGeometry.PaintThickness, profile.reach, 2));
            Pulse(0, 0.16f, floor, false);
            if (stage >= 2) Pulse(1, 0.4f, wall, true);
            if (stage == 3)
            {
                Pulse(2, 0.16f + profile.echoDelay, floor, false);
                Pulse(3, 0.4f + profile.echoDelay, wall, true);
            }
        }
        private Bounds Forward(float reach) => new Bounds(motor.transform.position + new Vector3(facing * reach * 0.5f, 1, 0),
            new Vector3(reach, 2, 2));
        private bool Pulse(int index, float at, Bounds area, bool finisher)
        {
            if (!active || elapsed < at || !delivered.Add(index)) return false;
            Line(area.min, area.max, finisher ? 0.1f : 0.065f);
            // An offset reverse trail is visual only; never an extra contact or skill use.
            Line((Vector2)area.max + Vector2.up * 0.15f, (Vector2)area.min + Vector2.up * 0.15f, 0.025f);
            Hit(area, finisher);
            return active; // Death/counter callbacks may have disposed this pulse synchronously.
        }
        private void Hit(Bounds area, bool finisher)
        {
            if (active && !player.Health.Dead && area.Intersects(player.GetComponent<Collider2D>().bounds))
            {
                DeliveringContact = true;
                try { contact |= player.Health.Hit(finisher ? power.finisherDamage : power.damage, motor.transform.position, power.knockback); }
                finally { DeliveringContact = false; }
            }
        }
        private void Move(float speed, float dt = -1f)
        {
            if (!active) return;
            if (dt <= 0) dt = Time.fixedDeltaTime;
            float x = motor.Body.position.x;
            if (speed > 0) speed = Mathf.Min(speed, Mathf.Max(0, (data.arenaMax - x) / dt));
            if (speed < 0) speed = Mathf.Max(speed, Mathf.Min(0, (data.arenaMin - x) / dt));
            var capsule = motor.GetComponent<CapsuleCollider2D>();
            if (Mathf.Abs(speed) > 0.01f)
            {
                var hit = Physics2D.CapsuleCast(motor.Body.position + capsule.offset, capsule.size - Vector2.one * SkillGeometry.Skin,
                    CapsuleDirection2D.Vertical, 0, Vector2.right * Mathf.Sign(speed), Mathf.Abs(speed) * dt,
                    1 << LevelBuilder.GroundLayer);
                if (hit.collider != null) speed = 0;
            }
            motor.Move(speed);
            // Return strokes keep the original facing so attack source and silhouette remain legible.
            motor.Face(facing);
        }
        private void StopMotion()
        {
            if (motor != null && motor.Body != null)
                motor.Body.linearVelocity = new Vector2(0, motor.Body.linearVelocity.y);
        }
        private void Line(Vector2 from, Vector2 to, float width)
        {
            // At most a small finite number of lines per pattern, including dash samples.
            if (visuals.Count >= 64) return;
            if (material == null) material = new Material(Shader.Find("Sprites/Default"));
            var go = new GameObject("Janhyang violet echo");
            if (SkillStroke.WorldRoot != null) go.transform.SetParent(SkillStroke.WorldRoot, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = true; line.positionCount = 2;
            line.SetPosition(0, from); line.SetPosition(1, to);
            line.startWidth = line.endWidth = width;
            line.startColor = data.tint; line.endColor = new Color(data.tint.r, data.tint.g, data.tint.b, 0.2f);
            line.sortingOrder = 15; visuals.Add(go);
        }
        private void Ring(Vector2 center, float radius)
        {
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8, b = (i + 1) * Mathf.PI / 8;
                Line(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, 0.045f);
            }
        }
        private void Ghost()
        {
            if (!active || visuals.Count >= 64) return;
            var source = motor.Animation.GetComponent<SpriteRenderer>();
            var go = new GameObject("Janhyang reversed sword silhouette");
            if (SkillStroke.WorldRoot != null) go.transform.SetParent(SkillStroke.WorldRoot, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = source.sprite; renderer.flipX = source.flipX;
            renderer.color = new Color(data.tint.r, data.tint.g, data.tint.b, 0.3f); renderer.sortingOrder = 9;
            go.transform.SetPositionAndRotation(source.transform.position - Vector3.right * facing * 0.2f, source.transform.rotation);
            go.transform.localScale = source.transform.lossyScale; visuals.Add(go);
        }
        private void ClearVisuals() { foreach (var go in visuals) if (go != null) Object.Destroy(go); visuals.Clear(); }
        public void Suspend()
        {
            // Knockback/local freeze/binding movement is never a pendulum attack traversal.
            previous = motor.Body.position; movementSegment = -1;
        }
        public void Cancel() { active = false; delivered.Clear(); ClearVisuals(); }
        public void Dispose() { Cancel(); if (material != null) Object.Destroy(material); }
    }
}
