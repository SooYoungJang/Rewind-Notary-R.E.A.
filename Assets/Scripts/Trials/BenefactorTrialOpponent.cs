using UnityEngine;

namespace RewindNotary
{
    // Authored protective demonstrations; never runs Janhyang AI or encounter victory rewards.
    public sealed class BenefactorTrialOpponent : MonoBehaviour
    {
        private CombatMotor2D motor, player;
        private StitchBinding binding;
        private SkillSession session;
        public BenefactorTrialDefinition Definition { get; private set; }
        private int pattern, phase, shotSerial;
        private float remaining, direction = -1, rest;
        private bool contacted;
        private TrialPattern Pattern => Definition.patterns[pattern];
        public string PatternLabel => rest > 0 ? Loc.Get("trial.resting") : Pattern.Label + (phase == 0 ? Loc.Get("trial.windup_suffix") : phase == 1 ? Loc.Get("trial.action_suffix") : Loc.Get("trial.opening_suffix"));
        public void Initialize(CombatMotor2D actor, SkillSession skills, BenefactorTrialDefinition definition)
        {
            motor = GetComponent<CombatMotor2D>(); player = actor; session = skills; Definition = definition;
            binding = GetComponent<StitchBinding>();
            motor.Health.Initialize(definition.health, definition.hitImmunity);
            motor.Health.Died += Rest;
            session.Progression.Trials.Verified += Rest;
            ResetAttempt();
        }
        private void Rest() { rest = Definition.restSeconds; motor.Move(0); motor.Animation.SetTelegraph(false); }
        public void ResetAttempt()
        {
            motor.ResetAt(new Vector2(Definition.spawnX, 0.15f)); motor.Face(-1);
            pattern = phase = 0; remaining = Pattern.windup; rest = 0; contacted = false;
            binding.Break(); motor.Animation.SetTelegraph(false);
        }
        private void FixedUpdate()
        {
            if (!GameInput.GameplayEnabled || player.Health.Dead) return;
            if (rest > 0)
            {
                rest -= Time.fixedDeltaTime;
                if (rest <= 0) ResetAttempt();
                return;
            }
            if (motor.Health.Dead) return;
            if (motor.transform.position.y < -7) { Rest(); return; }
            if (motor.Temporal.Frozen || motor.Stunned || binding.Bound)
            { motor.Animation.SetTelegraph(false); if (!motor.Temporal.Frozen) motor.Move(0); return; }
            motor.RefreshGround();
            float delta = player.Body.position.x - motor.Body.position.x;
            if (phase == 0 && remaining == Pattern.windup && Mathf.Abs(delta) > Pattern.reach + 0.5f)
            { Move(Mathf.Sign(delta) * Definition.approachSpeed); return; }
            if (phase == 0)
            {
                motor.Move(0); motor.Animation.SetTelegraph(true);
                if (remaining == Pattern.windup) { direction = delta < 0 ? -1 : 1; motor.Face(direction); }
            }
            else if (phase == 1)
            {
                motor.Animation.SetTelegraph(false);
                bool dash = Pattern.action == "pendulum_out" || Pattern.action == "pendulum_back";
                Move(dash ? direction * Pattern.speed : 0);
                motor.Animation.PlayAttack(Pattern.duration - remaining, 0, Pattern.duration);
                if (!contacted && Threat().Intersects(player.GetComponent<Collider2D>().bounds))
                { contacted = true; player.Health.Hit(Pattern.damage, motor.Body.position); }
            }
            else motor.Move(0);
            remaining -= Time.fixedDeltaTime;
            if (remaining > 0) return;
            if (phase == 0)
            {
                phase = 1; remaining = Pattern.duration; contacted = false;
                if (Pattern.action == "waltz") motor.Jump(Pattern.speed);
                if (Pattern.action == "volley") { Shoot(); contacted = true; }
                var area = Threat(); SkillStroke.Show(area.min, area.max, Color.cyan, Pattern.duration);
            }
            else if (phase == 1) { phase = 2; remaining = Pattern.recovery; }
            else { pattern = (pattern + 1) % Definition.patterns.Length; phase = 0; remaining = Pattern.windup; }
        }
        private Bounds Threat()
        {
            float height = Pattern.action == "paint_line" ? 0.5f : Pattern.action == "thread_cross" ? 3f : 2f;
            bool radial = Pattern.action == "bell" || Pattern.action == "waltz";
            return new Bounds(motor.transform.position + new Vector3(radial ? 0 : direction * Pattern.reach * 0.5f, height * 0.5f, 0),
                new Vector3(Pattern.reach * (radial ? 2 : 1), height, 2));
        }
        private void Move(float speed)
        {
            if ((motor.Body.position.x <= Definition.arenaMin && speed < 0) || (motor.Body.position.x >= Definition.arenaMax && speed > 0)) speed = 0;
            motor.Move(speed);
        }
        private void Shoot()
        {
            var go = new GameObject("trial_shot:" + Definition.id + ":" + (++shotSerial));
            go.transform.SetParent(transform.parent, false);
            bool fromRight = player.Body.position.x + Pattern.reach <= Definition.arenaMax;
            float x = player.Body.position.x + (fromRight ? Pattern.reach : -Pattern.reach);
            go.transform.position = new Vector3(x, player.Body.position.y + Pattern.projectileHeight, 0);
            var shot = go.AddComponent<SkillProjectile2D>();
            shot.Initialize(player, motor);
            shot.ConfigureTrialShot((fromRight ? Vector2.left : Vector2.right) * Pattern.projectileSpeed, Pattern.damage, Pattern.projectileLifetime);
        }
        private void OnDisable() { if (motor != null) motor.Animation.SetTelegraph(false); }
        private void OnDestroy()
        {
            if (motor != null) motor.Health.Died -= Rest;
            if (session != null) session.Progression.Trials.Verified -= Rest;
        }
    }
}
