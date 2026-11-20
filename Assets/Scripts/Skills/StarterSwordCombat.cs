using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed partial class CombatMotor2D
    {
        private const float WaveSpeed = 17f, CrestInterval = .13f, WaveLifetime = 3f;
        private static readonly Vector2 LightWaveSize = new Vector2(.64f, 1.1f);
        private static readonly Vector2 HeavyWaveSize = new Vector2(.84f, 2.03f);
        private int starterFacing;
        private bool swingConnected;

        private sealed class WaveCrest
        {
            internal readonly HashSet<int> Visited = new HashSet<int>();
            internal Vector2 Position;
            internal SpriteRenderer Visual;
            internal bool Started, Done;
            internal float NextWake;
        }

        private sealed class WaveCast
        {
            internal UseContext Use;
            internal WaveCrest[] Crests;
            internal Coroutine Flight;
        }
        private readonly List<WaveCast> starterWaves = new List<WaveCast>();

        private void ShowStarterStrike(Bounds blade, bool hit)
        {
            StarterSlashVisual.Burst(activeStarter, blade.center, blade.size, starterFacing, hit);
            if (activeStarter == QuickSkills.Thrust)
                SkillStroke.Afterimage(Animation.Renderer, .20f);
        }

        internal void EndSwingRecoveryForCharge()
        {
            if (Attacking && activeStarter < 0 && strikeDelivered) CancelAttack();
        }

        internal void FireChargedWave(int level, float axis)
        {
            if (Health.Dead || Stunned || Skills == null || Temporal?.Frozen == true ||
                BenefactorSkills?.BlocksActions == true) return;
            bool defending = Defense?.BlocksActions == true;
            if (defending && Defense.State.Phase != DefensePhase.Dash) return;

            var use = Skills.Progression.BeginUse(SkillRules.BasicSword, Skills.PreviewStage);
            if (use == null) return;
            int direction = Mathf.Abs(axis) <= .01f ? Facing : (int)Mathf.Sign(axis);
            int amount;
            if (!defending && !Attacking)
            {
                ResetCombo();
                activeStarter = QuickSkills.BladeWave;
                starterFacing = direction;
                Face(direction);
                aerialSwing = false;
                attackStep = AttackDirectionY = 0;
                var attack = StarterAttacks[QuickSkills.BladeWave];
                StartSwing(attack.swing, attack.art, false);
                strikeDelivered = true;
                amount = strikeDamage;
            }
            else
            {
                float counter = Defense != null ? Defense.State.ConsumeCounter(Time.time) : 1f;
                amount = FinaleCombat.Scale(Skills, Mathf.CeilToInt(2 * damage * counter));
            }
            LaunchWave(use, direction, level, level == 2 ? Mathf.CeilToInt(amount * 1.5f) : amount);
        }

        private void ReleaseStarterWave()
        {
            var use = activeUse;
            activeUse = chainUse = null;
            if (use != null) LaunchWave(use, starterFacing, 1, strikeDamage);
        }

        private void LaunchWave(UseContext use, int direction, int level, int amount)
        {
            var wave = new WaveCast { Use = use, Crests = new WaveCrest[level == 2 ? 3 : 1] };
            Vector2 origin = Body.position + Vector2.up * (level == 2 ? 1.24f : 1.08f);
            for (int i = 0; i < wave.Crests.Length; i++) wave.Crests[i] = new WaveCrest { Position = origin };
            StarterSlashVisual.Burst(QuickSkills.BladeWave, origin, Vector2.one * (level == 2 ? 2.35f : 1.45f), direction, false);
            SkillStroke.Afterimage(Animation.Renderer, .19f);
            starterWaves.Add(wave);
            wave.Flight = StartCoroutine(FlyWave(wave, origin, direction, level, amount));
        }

        private IEnumerator FlyWave(WaveCast wave, Vector2 origin, int direction, int level, int amount)
        {
            var camera = Camera.main;
            float distance = ViewEdge(camera, origin, direction);
            Vector2 size = level == 2 ? HeavyWaveSize : LightWaveSize;
            float age = 0f;
            int left = wave.Crests.Length;
            while (left > 0 && age < WaveLifetime)
            {
                yield return null;
                if (!GameInput.GameplayEnabled || Time.deltaTime <= 0f) continue;
                age += Time.deltaTime;
                distance = Mathf.Max(distance, ViewEdge(camera, origin, direction));
                for (int i = 0; i < wave.Crests.Length; i++)
                {
                    var crest = wave.Crests[i];
                    if (crest.Done || age < i * CrestInterval) continue;
                    if (!crest.Started)
                    {
                        crest.Started = true;
                        crest.Visual = StarterSlashVisual.ChargedBlade(level, origin, direction, i);
                    }
                    if (!AdvanceCrest(wave.Use, crest, size, level, amount, i + 1, direction,
                        distance - (crest.Position.x - origin.x) * direction)) continue;
                    crest.Done = true;
                    left--;
                    if (crest.Visual != null) Destroy(crest.Visual.gameObject);
                }
            }
            starterWaves.Remove(wave);
            ClearStarterWave(wave);
        }

        private bool AdvanceCrest(UseContext use, WaveCrest crest, Vector2 size, int level, int amount,
            int pulse, int direction, float remaining)
        {
            if (remaining <= 0f) return true;
            float distance = Mathf.Min(remaining, Time.deltaTime * WaveSpeed);
            Vector2 heading = Vector2.right * direction;
            var terrain = Physics2D.BoxCast(crest.Position, size, 0f, heading, distance, 1 << LevelBuilder.GroundLayer);
            float travel = terrain.collider == null ? distance : Mathf.Max(0f, terrain.distance - .02f);
            Vector2 next = crest.Position + heading * travel;
            if (crest.Visual != null) crest.Visual.transform.position = next;
            if (Time.time >= crest.NextWake)
            {
                StarterSlashVisual.ChargedWake(level, crest.Position, direction);
                crest.NextWake = Time.time + .085f;
            }

            var swept = new Bounds((crest.Position + next) * .5f, new Vector3(size.x + travel, size.y, 2f));
            if (ApplySwordBounds(swept, use, amount, .16f, 0, QuickSkills.BladeWave, pulse, crest.Visited))
                StarterSlashVisual.Burst(QuickSkills.BladeWave, next, Vector2.one * (level == 2 ? 1.9f : 1.35f), direction, true);
            else if (terrain.collider != null && pulse == 1)
                StarterSlashVisual.Burst(QuickSkills.BladeWave, next, Vector2.one * 1.05f, direction, false);
            crest.Position = next;
            return terrain.collider != null;
        }

        private static float ViewEdge(Camera camera, Vector2 origin, int direction)
        {
            if (camera == null) return 14f;
            float depth = camera.WorldToViewportPoint(origin).z;
            float edge = camera.ViewportToWorldPoint(new Vector3(direction > 0 ? 1f : 0f, .5f, depth)).x;
            return Mathf.Max(1.4f, (edge - origin.x) * direction + .95f);
        }

        private void CancelStarterWaves()
        {
            foreach (var wave in starterWaves)
            {
                StopCoroutine(wave.Flight);
                ClearStarterWave(wave);
            }
            starterWaves.Clear();
        }

        private void ClearStarterWave(WaveCast wave)
        {
            foreach (var crest in wave.Crests)
                if (crest.Visual != null) Destroy(crest.Visual.gameObject);
            Skills?.Progression.EndUse(wave.Use);
        }
    }
}
