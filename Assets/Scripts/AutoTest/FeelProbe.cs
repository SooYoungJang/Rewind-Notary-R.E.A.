using System.Collections;
using UnityEngine;

namespace RewindNotary
{
    // The driver emits keys before gameplay Update. Sample the resulting body after each
    // physics simulation and observe the swing after all input readers have run.
    internal sealed class FeelProbe : MonoBehaviour
    {
        private enum Phase { Waiting, GroundRun, GroundStop, Tap, Attack, AirRun, Jump, AirStop, Done, Failed }
        private Phase phase;
        private CombatMotor2D player;
        private FeelResults result;
        private int runTicks, tapFrame;
        internal bool Passed => phase == Phase.Done;

        internal bool Drive(FirstDepartureBot bot, BotKeyboard keys, FeelResults measurements)
        {
            if (Passed) return false;
            if (phase == Phase.Waiting)
            {
                // This row waits for F on a flat, enemy-free floor. Let the route bot
                // handle the preceding dialogue, then take exclusive ownership of the keys.
                if (bot.Area?.PendingAction != "use:mint_candy_pickup") return false;
                player = bot.Area.Rea;
                result = measurements;
                result.groundStop.status = "running";
                phase = Phase.GroundRun;
            }
            if (!GameInput.CanPlay || player == null || player.Health.Dead)
            {
                Abort("Player unavailable during " + phase);
                return true;
            }

            float speed = player.Body.linearVelocity.x;
            switch (phase)
            {
                case Phase.GroundRun:
                    if (runTicks >= 4 && player.Grounded && AtRunSpeed(speed))
                    {
                        result.groundStop.releaseSpeed = speed;
                        phase = Phase.GroundStop;
                    }
                    else keys.Hold(KeyCode.D);
                    break;
                case Phase.Tap:
                    if (!player.Grounded || player.Attacking || Mathf.Abs(speed) >= .1f)
                    { Abort("Single J tap requires a stationary idle player"); break; }
                    keys.Tap(KeyCode.J);
                    tapFrame = Time.frameCount;
                    player.AttackStarted += OnAttackStarted;
                    phase = Phase.Attack;
                    break;
                case Phase.AirRun:
                    if (player.Attacking) break;
                    keys.Hold(KeyCode.D);
                    if (player.Grounded && AtRunSpeed(speed))
                    {
                        keys.Hold(KeyCode.Space);
                        phase = Phase.Jump;
                    }
                    break;
                case Phase.Jump:
                    if (!player.Grounded && !player.Attacking && player.Body.linearVelocity.y > 0f && AtRunSpeed(speed))
                    {
                        result.airStop.releaseSpeed = speed;
                        phase = Phase.AirStop;
                    }
                    else
                    {
                        keys.Hold(KeyCode.D);
                        keys.Hold(KeyCode.Space);
                    }
                    break;
            }
            // NextFrame starts with all keys released, including during both stop windows.
            return true;
        }

        private static bool AtRunSpeed(float vx) => vx >= PlayerController2D.MoveSpeed - .05f;

        private IEnumerator Start()
        {
            var afterPhysics = new WaitForFixedUpdate();
            while (true)
            {
                yield return afterPhysics;
                if (phase == Phase.GroundRun && player != null)
                    runTicks = player.Grounded && AtRunSpeed(player.Body.linearVelocity.x) ? runTicks + 1 : 0;
                else if (phase == Phase.GroundStop || phase == Phase.AirStop) SampleStop();
            }
        }

        private void SampleStop()
        {
            bool ground = phase == Phase.GroundStop;
            var sample = ground ? result.groundStop : result.airStop;
            sample.physicsTicks++;
            sample.seconds += Time.fixedDeltaTime;
            sample.speed = Mathf.Abs(player.Body.linearVelocity.x);
            bool touchedFloor = player.GetComponent<Collider2D>().IsTouchingLayers(1 << LevelBuilder.GroundLayer);
            if (player.Grounded != ground || touchedFloor != ground || player.Attacking)
            { Abort("Ground or attack state changed during " + phase); return; }

            bool inTime = ground ? sample.physicsTicks <= 3 : sample.seconds <= .35f;
            bool stopped = ground ? sample.speed < .1f : sample.speed <= .5f;
            if (inTime && stopped)
            {
                sample.status = "pass";
                phase = ground ? Phase.Tap : Phase.Done;
                if (ground) result.stationaryAttack.status = "running";
            }
            else if (ground ? sample.physicsTicks >= 3 : sample.seconds >= .35f)
                Abort("Horizontal speed exceeded the " + phase + " deadline");
        }

        private void OnAttackStarted() { result.stationaryAttack.attackStartedCount++; }

        private void LateUpdate()
        {
            if (phase != Phase.Attack) return;
            var sample = result.stationaryAttack;
            sample.framesSinceTap = Time.frameCount - tapFrame;
            sample.attacking = player.Attacking;
            if (sample.framesSinceTap <= 2 && sample.attacking && sample.attackStartedCount == 1)
            {
                player.AttackStarted -= OnAttackStarted;
                sample.status = "pass";
                result.airStop.status = "running";
                phase = Phase.AirRun;
            }
            else if (sample.framesSinceTap >= 2) Abort("Stationary J did not start one swing within two frames");
        }

        internal void Abort(string reason)
        {
            if (result == null || Passed || phase == Phase.Failed) return;
            result.failure = reason;
            if (result.groundStop.status == "running") result.groundStop.status = "fail";
            if (result.stationaryAttack.status == "running") result.stationaryAttack.status = "fail";
            if (result.airStop.status == "running") result.airStop.status = "fail";
            if (player != null) player.AttackStarted -= OnAttackStarted;
            phase = Phase.Failed;
        }
    }
}
