using UnityEngine;

namespace RewindNotary
{
    public sealed partial class WaveTarget2D
    {
        private enum AttackPhase { Idle, Windup, Charge }
        private enum Move { Cleave, TwinCut, MirrorCut, MirrorRay, Stomp, Rush }

        private AttackPhase attackPhase;
        private Move attack;
        private float nextAttack, strikeAt, recoverUntil, lastDodge;
        private float chargeLane, chargeRemaining;
        private int attackIndex;
        private bool followUp, chargeContact;

        internal float StrikeIn => attackPhase == AttackPhase.Windup ? strikeAt - Time.time : float.PositiveInfinity;
        internal bool Charging => attackPhase == AttackPhase.Charge;
        internal float LastStrikeAt { get; private set; } = float.NegativeInfinity;

        internal bool TryGetAttackCue(out Bounds bounds, out Color tint)
        {
            bool pending = attackPhase == AttackPhase.Windup;
            bounds = pending ? AttackBounds() : default;
            tint = AttackTint();
            return pending;
        }

        private Move ChooseAttack(float distance, bool alternate)
        {
            if (movementArchetype == "shell_sentry")
                return (alternate || distance > definition.attackReach * .8f) ? Move.Rush : Move.Stomp;
            if (movementArchetype == "reflection_statue")
                return (alternate || distance > definition.attackReach) ? Move.MirrorRay : Move.MirrorCut;
            return alternate ? Move.TwinCut : Move.Cleave;
        }

        private void TickAttack(float now)
        {
            if (attackPhase == AttackPhase.Charge) return;
            Vector2 playerFeet = player.Body.position;
            if (attackPhase == AttackPhase.Idle)
            {
                if (now < nextAttack) return;
                Vector2 offset = playerFeet - (Vector2)transform.position;
                float distance = Mathf.Abs(offset.x);
                if (distance > definition.attackReach * 1.6f || Mathf.Abs(offset.y) > 1.35f || TerrainBetween(playerFeet))
                    return;
                Facing = offset.x >= 0f ? 1 : -1;
                attack = ChooseAttack(distance, (attackIndex & 1) != 0);
                if (attack == Move.Rush)
                {
                    chargeLane = footing.WalkableDistance(transform.position, Facing, definition.attackReach * 1.4f);
                    if (chargeLane < .6f) attack = Move.Stomp;
                }
                if (!AttackBounds().Intersects(player.GetComponent<Collider2D>().bounds)) return;
                attackIndex++;
                followUp = false;
                BeginWindup(now, definition.telegraphSeconds + (attack == Move.Rush || attack == Move.MirrorRay ? .16f : 0f));
                return;
            }

            Bounds area = AttackBounds();
            if (strikeAt - now <= DefenseTuning.DashInvulnerableSeconds && PlayerIn(area)
                && player.Defense.State.Evading(now)) lastDodge = now;
            if (now < strikeAt) return;

            LastStrikeAt = now;
            if (attack == Move.Rush)
            {
                attackPhase = AttackPhase.Charge;
                chargeRemaining = chargeLane;
                chargeContact = false;
                SkillStroke.Afterimage(visual, .20f);
                AudioManager.Play(AudioManager.Sfx.BossAttack);
                return;
            }

            PlayStrike(area);
            bool defended = ResolveStrike(area, now, true);
            if (attack == Move.TwinCut && !followUp && !defended && !player.Health.Dead)
            {
                followUp = true;
                BeginWindup(now, .38f);
                return;
            }
            FinishAttack(now);
        }

        private void BeginWindup(float now, float duration)
        {
            attackPhase = AttackPhase.Windup;
            strikeAt = now + duration;
            lastDodge = float.NegativeInfinity;
        }

        private void StepCharge(Vector2 feet, float now, float dt)
        {
            float step = Mathf.Min(chargeRemaining, 8.6f * dt);
            if (step <= .001f || !footing.TryStep(feet, feet.x + Facing * step, out var next))
            {
                FinishAttack(now);
                return;
            }
            transform.position = next;
            chargeRemaining -= step;
            if (!chargeContact)
            {
                var contact = new Bounds(next + Vector2.up * .55f, new Vector3(1.08f, 1.1f, 2f));
                contact.Encapsulate(new Vector3(feet.x - Facing * .5f, feet.y + .55f, 0f));
                if (PlayerIn(contact))
                {
                    chargeContact = true;
                    if (ResolveStrike(contact, now, false)) { FinishAttack(now); return; }
                }
            }
            if (chargeRemaining <= .001f) FinishAttack(now);
        }

        private void FinishAttack(float now)
        {
            attackPhase = AttackPhase.Idle;
            followUp = false;
            recoverUntil = now + (attack == Move.Rush || attack == Move.Stomp ? .52f : .35f);
            nextAttack = now + definition.attackInterval;
        }

        private bool PlayerIn(Bounds area) =>
            area.Intersects(player.GetComponent<Collider2D>().bounds) && !TerrainBetween(player.Body.position);

        private bool ResolveStrike(Bounds area, float now, bool countRecentDodge)
        {
            bool contact = PlayerIn(area);
            bool evading = contact && player.Defense.State.Evading(now);
            bool hurt = contact && player.Health.Hit(definition.attackDamage, transform.position);
            bool dodged = !hurt && (evading || (countRecentDodge && !contact &&
                now - lastDodge <= DefenseTuning.DashInvulnerableSeconds));
            if (dodged && State.OpenDefense(now))
                player.Skills?.Progression.EmitSimple("wave_opening", "", TargetId + ":dash");
            return dodged;
        }

        private Bounds AttackBounds()
        {
            float reach = definition.attackReach, back = .34f, height = 1.75f, bottom = 0f;
            switch (attack)
            {
                case Move.Cleave: height = 1.35f; break;
                case Move.TwinCut:
                    reach *= followUp ? 1.13f : .88f;
                    height = followUp ? 2.15f : 1.7f;
                    break;
                case Move.MirrorCut: height = 2.05f; break;
                case Move.MirrorRay:
                    reach *= 1.52f;
                    back = .20f; bottom = .5f; height = .68f;
                    var wall = Physics2D.Raycast(transform.position + Vector3.up * (bottom + height * .5f),
                        Vector2.right * Facing, reach, 1 << LevelBuilder.GroundLayer);
                    if (wall.collider != null) reach = wall.distance;
                    break;
                case Move.Stomp: back = reach *= .74f; height = .72f; break;
                case Move.Rush: reach = chargeLane + .52f; back = .52f; height = 1.10f; break;
            }
            return new Bounds(transform.position + new Vector3(Facing * (reach - back) * .5f, bottom + height * .5f, 0f),
                new Vector3(reach + back, height, 2f));
        }

        private Color AttackTint()
        {
            switch (attack)
            {
                case Move.TwinCut: return new Color(1f, .43f, .33f);
                case Move.MirrorCut: case Move.MirrorRay: return new Color(.69f, .78f, 1f);
                default: return new Color(1f, .68f, .30f);
            }
        }

        private void PlayStrike(Bounds area)
        {
            Color tint = AttackTint();
            if (attack == Move.MirrorRay || attack == Move.Stomp)
            {
                float y = attack == Move.Stomp ? area.min.y + .18f : area.center.y;
                SkillStroke.Show(new Vector3(area.min.x, y), new Vector3(area.max.x, y), tint, .22f);
                if (attack == Move.Stomp) AtmospherePostEffect.PulseImpact(.22f);
            }
            else
            {
                float angle = (attack == Move.TwinCut && followUp ? -44f : attack == Move.Cleave ? -18f : 30f) * Facing;
                ActorVisualPolish2D.SpawnRotatedSlash("fx-basic-sword-mastery-additive", area.center, Facing,
                    Mathf.Clamp(area.size.x, 1.2f, 2.65f), .21f, angle, tint);
            }
            AudioManager.Play(attack == Move.Stomp ? AudioManager.Sfx.BossAttack : AudioManager.Sfx.Swing);
        }
    }
}
