using System.Collections;
using UnityEngine;

namespace RewindNotary
{
    public sealed partial class CombatMotor2D
    {
        private const float WaveSpeed = 14f, WaveRange = 7f, EchoDelay = .42f;
        private static readonly Vector2 WaveSize = new Vector2(.56f, 1.05f);
        private int starterFacing;
        private bool swingConnected;
        private Coroutine starterWave;
        private UseContext waveUse;
        private SpriteRenderer waveVisual;

        private void ShowStarterStrike(Bounds blade)
        {
            var center = blade.center;
            StarterSlashVisual.Burst(activeStarter, center, blade.size, starterFacing);
            if (activeStarter == QuickSkills.Thrust)
                SkillStroke.Afterimage(Animation.Renderer, .16f);
        }

        private void ReleaseStarterWave()
        {
            CancelStarterWave();
            waveUse = activeUse;
            activeUse = chainUse = null;
            Vector2 origin = Body.position + Vector2.up * StarterAttacks[QuickSkills.BladeWave].centerY;
            waveVisual = StarterSlashVisual.Create(QuickSkills.BladeWave, origin, new Vector2(1.35f, 1.65f), starterFacing);
            starterWave = StartCoroutine(FlyStarterWave(origin, starterFacing, strikeDamage));
        }

        private IEnumerator FlyStarterWave(Vector2 position, int direction, int amount)
        {
            float remaining = WaveRange;
            while (remaining > 0f)
            {
                yield return null;
                if (!GameInput.GameplayEnabled || Time.deltaTime <= 0f) continue;
                float distance = Mathf.Min(remaining, Time.deltaTime * WaveSpeed);
                Vector2 heading = Vector2.right * direction;
                var terrain = Physics2D.BoxCast(position, WaveSize, 0f, heading, distance, 1 << LevelBuilder.GroundLayer);
                float travel = terrain.collider == null ? distance : Mathf.Max(0f, terrain.distance - .02f);
                Vector2 next = position + heading * travel;
                if (waveVisual != null) waveVisual.transform.position = next;
                var swept = new Bounds((position + next) * .5f, new Vector3(WaveSize.x + travel, WaveSize.y, 2f));
                if (ApplySwordBounds(swept, waveUse, amount, .65f, 0, QuickSkills.BladeWave))
                {
                    yield return WaveEcho(next, direction, amount);
                    break;
                }
                if (terrain.collider != null) break;
                position = next;
                remaining -= distance;
            }
            starterWave = null;
            ClearStarterWave();
        }

        private IEnumerator WaveEcho(Vector2 position, int direction, int amount)
        {
            if (waveVisual != null)
                waveVisual.color = new Color(.55f, .8f, 1f, .32f);
            yield return new WaitForSeconds(EchoDelay);
            StarterSlashVisual.Burst(QuickSkills.BladeWave, position, Vector2.one * 1.8f, direction);
            ApplySwordBounds(new Bounds(position, new Vector3(1.05f, 1.35f, 2f)),
                waveUse, Mathf.Max(1, amount / 2), .35f, 0, QuickSkills.BladeWave, echo: true);
        }

        private void CancelStarterWave()
        {
            if (starterWave != null) StopCoroutine(starterWave);
            starterWave = null;
            ClearStarterWave();
        }

        private void ClearStarterWave()
        {
            if (waveVisual != null) Destroy(waveVisual.gameObject);
            waveVisual = null;
            Skills?.Progression.EndUse(waveUse);
            waveUse = null;
        }
    }
}
