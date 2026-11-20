using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed partial class CombatMotor2D
    {
        private const float WaveSpeed = 17f, WaveLifetime = 3f;
        private static readonly Vector2 LightWaveSize = new Vector2(.64f, 1.1f);
        private static readonly Vector2 HeavyWaveSize = new Vector2(.76f, 1.6f);
        private int starterFacing;
        private bool swingConnected;

        private sealed class WaveCast
        {
            internal readonly HashSet<int> Visited = new HashSet<int>();
            internal UseContext Use;
            internal Vector2 Position;
            internal SpriteRenderer Visual;
            internal Coroutine Flight;
        }
        private readonly List<WaveCast> starterWaves = new List<WaveCast>();

        private void ShowStarterStrike(Bounds blade, bool hit)
        {
            StarterSlashVisual.Burst(activeStarter, blade.center, blade.size, starterFacing, hit);
            if (activeStarter == QuickSkills.Thrust)
                SkillStroke.Afterimage(Animation.Renderer, .20f);
            else if (activeStarter == QuickSkills.RetreatCut)
            {
                Vector3 center = blade.center;
                foreach (int slope in new[] { -1, 1 })
                {
                    Vector3 line = new Vector3(starterFacing * 1.2f, slope * .57f, 0f);
                    SkillStroke.Show(center - line, center + line, new Color(.70f, .79f, 1f, .75f), .18f);
                }
                SkillStroke.Afterimage(Animation.Renderer, .18f);
            }
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
            if (!defending && !Attacking)
            {
                ResetCombo();
                activeStarter = QuickSkills.RetreatCut;
                starterFacing = direction;
                Face(direction);
                aerialSwing = false;
                attackStep = AttackDirectionY = 0;
                var attack = StarterAttacks[QuickSkills.RetreatCut];
                StartSwing(attack.swing, attack.art, false);
                chargeReleasePose = true;
                strikeDelivered = true;
            }
            int amount = FinaleCombat.Scale(Skills, 2 * damage);
            LaunchWave(use, direction, level, level == 2 ? Mathf.CeilToInt(amount * 1.25f) : amount);
        }

        private void LaunchWave(UseContext use, int direction, int level, int amount)
        {
            Vector2 origin = Body.position + Vector2.up * (level == 2 ? 1.24f : 1.08f);
            var wave = new WaveCast
            {
                Use = use,
                Position = origin,
                Visual = StarterSlashVisual.ChargedBlade(level, origin, direction)
            };
            StarterSlashVisual.Burst(QuickSkills.RetreatCut, origin, Vector2.one * (level == 2 ? 1.5f : 1.35f), direction, false);
            SkillStroke.Afterimage(Animation.Renderer, .19f);
            starterWaves.Add(wave);
            wave.Flight = StartCoroutine(FlyWave(wave, origin, direction, level, amount, ViewEdge(Camera.main, origin, direction)));
        }

        private IEnumerator FlyWave(WaveCast wave, Vector2 origin, int direction, int level, int amount, float distance)
        {
            Vector2 size = level == 2 ? HeavyWaveSize : LightWaveSize;
            float age = 0f;
            while (age < WaveLifetime)
            {
                yield return null;
                if (!GameInput.GameplayEnabled || Time.deltaTime <= 0f) continue;
                age += Time.deltaTime;
                if (AdvanceWave(wave, size, level, amount, direction,
                    distance - (wave.Position.x - origin.x) * direction)) break;
            }
            starterWaves.Remove(wave);
            ClearStarterWave(wave);
        }

        private bool AdvanceWave(WaveCast wave, Vector2 size, int level, int amount, int direction, float remaining)
        {
            if (remaining <= 0f) return true;
            float distance = Mathf.Min(remaining, Time.deltaTime * WaveSpeed);
            Vector2 heading = Vector2.right * direction;
            var terrain = Physics2D.BoxCast(wave.Position, size, 0f, heading, distance, 1 << LevelBuilder.GroundLayer);
            float travel = terrain.collider == null ? distance : Mathf.Max(0f, terrain.distance - .02f);
            Vector2 next = wave.Position + heading * travel;
            if (wave.Visual != null) wave.Visual.transform.position = next;
            var swept = new Bounds((wave.Position + next) * .5f, new Vector3(size.x + travel, size.y, 2f));
            if (ApplySwordBounds(swept, wave.Use, amount, .16f, 0, QuickSkills.RetreatCut, pulse: 1, visited: wave.Visited))
                StarterSlashVisual.Burst(QuickSkills.RetreatCut, next, Vector2.one * (level == 2 ? 1.5f : 1.35f), direction, true);
            else if (terrain.collider != null)
                StarterSlashVisual.Burst(QuickSkills.RetreatCut, next, Vector2.one * 1.05f, direction, false);
            wave.Position = next;
            return terrain.collider != null || remaining <= travel + .001f;
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
            if (wave.Visual != null) Destroy(wave.Visual.gameObject);
            Skills?.Progression.EndUse(wave.Use);
        }
    }
}
