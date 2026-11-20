using System.Collections;
using UnityEngine;

namespace RewindNotary
{
    // The driver emits keys before gameplay Update. Sample the resulting body after each
    // physics simulation and observe the swing after all input readers have run.
    internal sealed class FeelProbe : MonoBehaviour
    {
        private enum Phase { Waiting, GroundRun, GroundStop, Tap, Attack, AirRun, Jump, AirStop, MovingRun, MovingAttack,
            ReverseRun, ReverseAttack, HoldReady, HoldAttack, Done, Failed }
        private Phase phase;
        private CombatMotor2D player;
        private FeelResults result;
        private int runTicks, tapFrame;
        private float reverseStarted;
        private bool reversedMotion, reversedStrike;
        private float holdStarted;
        private int heldSwings;
        private Sprite firstHeldPose;
        internal bool Passed => phase == Phase.Done;

        internal bool Drive(FirstDepartureBot bot, BotKeyboard keys, FeelResults measurements)
        {
            if (Passed) return false;
            if (phase == Phase.Waiting)
            {
                // This row waits for interaction on a flat, enemy-free floor. Let the route bot
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
                    else keys.Hold(KeyCode.RightArrow);
                    break;
                case Phase.Tap:
                    if (!player.Grounded || player.Attacking || Mathf.Abs(speed) >= .1f)
                    { Abort("Single attack tap requires a stationary idle player"); break; }
                    keys.Tap(GameInput.AttackKey);
                    tapFrame = Time.frameCount;
                    player.AttackStarted += OnAttackStarted;
                    phase = Phase.Attack;
                    break;
                case Phase.AirRun:
                    if (player.Attacking) break;
                    keys.Hold(KeyCode.RightArrow);
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
                        keys.Hold(KeyCode.RightArrow);
                        keys.Hold(KeyCode.Space);
                    }
                    break;
                case Phase.MovingRun:
                    if (!player.Grounded || player.Attacking) break;
                    keys.Hold(KeyCode.RightArrow);
                    if (!AtRunSpeed(speed)) break;
                    result.movingAttack.startSpeed = speed;
                    tapFrame = Time.frameCount;
                    keys.Tap(GameInput.AttackKey);
                    phase = Phase.MovingAttack;
                    break;
                case Phase.MovingAttack:
                    if (player.Attacking || Time.frameCount <= tapFrame + 1) keys.Hold(KeyCode.RightArrow);
                    else if (result.movingAttack.physicsTicks == 0) Abort("Running attack tap produced no attack physics ticks");
                    else
                    {
                        result.movingAttack.status = "pass";
                        result.reverseAttack = "running";
                        phase = Phase.ReverseRun;
                    }
                    break;
                case Phase.ReverseRun:
                    keys.Hold(KeyCode.RightArrow);
                    if (!player.Grounded || player.Attacking || player.ComboStep != 0 || !AtRunSpeed(speed)) break;
                    keys.Tap(GameInput.AttackKey);
                    player.BasicStrikeDelivered += OnReverseStrike;
                    reverseStarted = Time.time;
                    phase = Phase.ReverseAttack;
                    break;
                case Phase.ReverseAttack:
                    keys.Hold(KeyCode.LeftArrow);
                    if (player.ComboStep == 1 && Time.time - reverseStarted >= .10f) keys.Tap(GameInput.AttackKey);
                    if (player.ComboStep == 2 && reversedMotion && reversedStrike && FacingLeft)
                    {
                        player.BasicStrikeDelivered -= OnReverseStrike;
                        result.reverseAttack = "pass";
                        result.heldCombo = "running";
                        phase = Phase.HoldReady;
                    }
                    else if (Time.time - reverseStarted > 1f)
                        Abort("Moving attack did not reverse movement, strike and queued combo together");
                    break;
                case Phase.HoldReady:
                    if (!player.Grounded || player.Attacking || player.ComboStep != 0) break;
                    holdStarted = Time.time;
                    player.AttackStarted += OnHeldAttackStarted;
                    phase = Phase.HoldAttack;
                    keys.Hold(GameInput.AttackKey);
                    break;
                case Phase.HoldAttack:
                    if (Time.time - holdStarted > 3f)
                    {
                        Abort("Tapping attack did not complete the four-hit starting combo");
                        break;
                    }
                    if (heldSwings < 4) keys.Tap(GameInput.AttackKey);
                    else if (!player.Attacking)
                    {
                        player.AttackStarted -= OnHeldAttackStarted;
                        result.heldCombo = "pass";
                        phase = Phase.Done;
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
                else if (phase == Phase.MovingAttack && player.Attacking) SampleMovingAttack();
                else if (phase == Phase.ReverseAttack && player.Attacking && player.ComboStep == 1 &&
                         player.Grounded && player.Body.linearVelocity.x < -.1f && FacingLeft) reversedMotion = true;
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
                phase = ground ? Phase.Tap : Phase.MovingRun;
                if (ground) result.stationaryAttack.status = "running";
                else result.movingAttack.status = "running";
            }
            else if (ground ? sample.physicsTicks >= 3 : sample.seconds >= .35f)
                Abort("Horizontal speed exceeded the " + phase + " deadline");
        }

        private void SampleMovingAttack()
        {
            var sample = result.movingAttack;
            float speed = player.Body.linearVelocity.x;
            sample.minSpeed = sample.physicsTicks == 0 ? speed : Mathf.Min(sample.minSpeed, speed);
            sample.physicsTicks++;
            if (!player.Grounded || !player.GetComponent<Collider2D>().IsTouchingLayers(1 << LevelBuilder.GroundLayer))
                Abort("Lost ground contact during moving attack");
            else if (speed < PlayerController2D.MoveSpeed * .8f)
                Abort("Moving attack fell below 80% of run speed");
        }

        private void OnAttackStarted() { result.stationaryAttack.attackStartedCount++; }

        private void OnHeldAttackStarted()
        {
            int expectedStep = heldSwings + 1;
            var sprite = player.Animation.GetComponent<SpriteRenderer>().sprite;
            if (heldSwings == 0) firstHeldPose = sprite;
            heldSwings++;
            if (heldSwings > 4 || player.ComboStep != expectedStep || (sprite == firstHeldPose) != (expectedStep == 1))
                Abort("Tapping attack played the wrong swing or reused the first pose");
        }

        private bool FacingLeft => player.Facing < 0 && player.Animation.GetComponent<SpriteRenderer>().flipX;

        private void OnReverseStrike(Bounds hit, int damage)
        {
            if (player.ComboStep == 1)
                reversedStrike = FacingLeft && hit.center.x < player.transform.position.x;
        }

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
            else if (sample.framesSinceTap >= 2) Abort("Stationary attack tap did not start one swing within two frames");
        }

        internal void Abort(string reason)
        {
            if (result == null || Passed || phase == Phase.Failed) return;
            result.failure = reason;
            if (result.groundStop.status == "running") result.groundStop.status = "fail";
            if (result.stationaryAttack.status == "running") result.stationaryAttack.status = "fail";
            if (result.airStop.status == "running") result.airStop.status = "fail";
            if (result.movingAttack.status == "running") result.movingAttack.status = "fail";
            if (result.reverseAttack == "running") result.reverseAttack = "fail";
            if (result.heldCombo == "running") result.heldCombo = "fail";
            if (player != null)
            {
                player.AttackStarted -= OnAttackStarted;
                player.BasicStrikeDelivered -= OnReverseStrike;
                player.AttackStarted -= OnHeldAttackStarted;
            }
            phase = Phase.Failed;
        }
    }
}
