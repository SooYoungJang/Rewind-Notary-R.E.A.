using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed partial class CombatMotor2D
    {
        private const float WaveSpeed = 14f, WaveRange = 7f, EchoDelay = .42f;
        private static readonly Vector2 WaveSize = new Vector2(.56f, 1.05f);
        private int starterFacing;
        private bool swingConnected;
        private sealed class WaveCast
        {
            internal UseContext Use;
            internal SpriteRenderer Visual;
            internal Coroutine Flight;
        }
        private readonly List<WaveCast> starterWaves = new List<WaveCast>();

        private void ShowStarterStrike(Bounds blade, bool hit)
        {
            StarterSlashVisual.Burst(activeStarter, blade.center, blade.size, starterFacing, hit);
            if (activeStarter == QuickSkills.Thrust)
                SkillStroke.Afterimage(Animation.Renderer, .20f);
        }

        private void ReleaseStarterWave()
        {
            var wave = new WaveCast { Use = activeUse };
            activeUse = chainUse = null;
            Vector2 origin = Body.position + Vector2.up * StarterAttacks[QuickSkills.BladeWave].centerY;
            wave.Visual = StarterSlashVisual.Create(QuickSkills.BladeWave, origin, new Vector2(2.15f, 2.35f), starterFacing);
            if (wave.Visual != null) wave.Visual.gameObject.name = "Rea travelling sword wave";
            StarterSlashVisual.Burst(QuickSkills.BladeWave, origin, new Vector2(1.4f, 1.6f), starterFacing, false);
            SkillStroke.Afterimage(Animation.Renderer, .19f);
            starterWaves.Add(wave);
            wave.Flight = StartCoroutine(FlyStarterWave(wave, origin, starterFacing, strikeDamage));
        }

        private IEnumerator FlyStarterWave(WaveCast wave, Vector2 position, int direction, int amount)
        {
            float remaining = WaveRange, nextWake = Time.time;
            while (remaining > 0f)
            {
                yield return null;
                if (!GameInput.GameplayEnabled || Time.deltaTime <= 0f) continue;
                float distance = Mathf.Min(remaining, Time.deltaTime * WaveSpeed);
                Vector2 heading = Vector2.right * direction;
                var terrain = Physics2D.BoxCast(position, WaveSize, 0f, heading, distance, 1 << LevelBuilder.GroundLayer);
                float travel = terrain.collider == null ? distance : Mathf.Max(0f, terrain.distance - .02f);
                Vector2 next = position + heading * travel;
                if (wave.Visual != null) wave.Visual.transform.position = next;
                if (Time.time >= nextWake)
                {
                    StarterSlashVisual.Wake(position, direction);
                    nextWake = Time.time + .062f;
                }
                var swept = new Bounds((position + next) * .5f, new Vector3(WaveSize.x + travel, WaveSize.y, 2f));
                if (ApplySwordBounds(swept, wave.Use, amount, .65f, 0, QuickSkills.BladeWave))
                {
                    StarterSlashVisual.Burst(QuickSkills.BladeWave, next, Vector2.one * 1.65f, direction, true);
                    yield return WaveEcho(wave, next, direction, amount);
                    break;
                }
                if (terrain.collider != null)
                {
                    StarterSlashVisual.Burst(QuickSkills.BladeWave, next, Vector2.one * 1.15f, direction, false);
                    break;
                }
                position = next;
                remaining -= distance;
            }
            starterWaves.Remove(wave);
            ClearStarterWave(wave);
        }

        private IEnumerator WaveEcho(WaveCast wave, Vector2 position, int direction, int amount)
        {
            if (wave.Visual != null)
                wave.Visual.color = new Color(.55f, .8f, 1f, .28f);
            yield return new WaitForSeconds(EchoDelay);
            bool hit = ApplySwordBounds(new Bounds(position, new Vector3(1.05f, 1.35f, 2f)),
                wave.Use, Mathf.Max(1, amount / 2), .35f, 0, QuickSkills.BladeWave, echo: true);
            StarterSlashVisual.Burst(QuickSkills.BladeWave, position, Vector2.one * 2.2f, -direction, hit);
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
