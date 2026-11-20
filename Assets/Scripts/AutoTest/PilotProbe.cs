using System;
using UnityEngine;

namespace RewindNotary
{
    [Serializable] public sealed class PilotTraversal
    {
        public string room, fixedRoute = "skipped", wallRoute = "skipped";
        public int treads, fixedKicks, wallKicks;
        public long growthDelta;
        public int activations;
        public float fixedStartX, wallStartX, fixedFinishX, fixedTopY, finishX, topY, slideSpeed;
        public double fixedSeconds, wallSeconds;
        public bool airDashSpent, heldUntilLanding, landedCharged, dashBlockedKick, castBlockedKick;
        internal bool WitnessedBothRoutes => fixedKicks == 0 && treads == 2 && wallKicks == 1 &&
            Mathf.Abs(fixedStartX - wallStartX) <= .4f && Mathf.Abs(fixedFinishX - finishX) <= .4f &&
            Mathf.Abs(fixedTopY - topY) <= .12f && slideSpeed < 0 &&
            slideSpeed >= -PlayerController2D.WallSlideSpeed - .05f && airDashSpent && heldUntilLanding &&
            landedCharged && dashBlockedKick && castBlockedKick && growthDelta == 0 && activations == 0;
        internal bool Passed => fixedRoute == "pass" && wallRoute == "pass" && WitnessedBothRoutes;
    }

    [Serializable] public sealed class PilotResults
    {
        public PilotTraversal loom = new PilotTraversal { room = "velvet_02_loom" };
        public PilotTraversal gallery = new PilotTraversal { room = "velvet_03_gallery" };
        public string contactFloor, failure;
        public int contactWrites;
        public bool slotReadback, retryOnBalcony, arrivalOnShelf, lowerDoorRejected, knockbackObserved;
        public float hitVelocityX, hitVelocityY, hitGravity;
        public double entryToContactSeconds, comparisonSeconds;
        public string checkpointInterval = "unmeasured";
        internal bool Passed => loom.Passed && gallery.Passed && slotReadback && retryOnBalcony &&
            arrivalOnShelf && lowerDoorRejected && contactWrites == 1 && knockbackObserved;
    }

    // Drives the same physical start, treads, wall and finish twice in each live room.
    internal sealed class PilotProbe
    {
        private enum Phase { Start, Hit, Recover, ApproachTreads, Treads, FixedFinish, Drop, Return, ToWall, TestDash,
            CastChord, CheckCast, WaitDash, Land, Launch, SpendDash, Slide, Kick, WallFinish, LeaveCheckpoint,
            TestLowerDoor, VerifyLowerDoor, Retry, Exit }
        private Phase phase;
        private PilotTraversal route;
        private int kicksAtStart, tread, contactBefore;
        private double started, entry, comparisonStart;
        private bool jumpHeld, observeCharge;
        private float hitTick, castTick;
        private long growthBefore;
        private string roomId;
        private readonly Action<string> capture;
        internal PilotProbe(Action<string> screenshot) { capture = screenshot; }

        internal void ObserveUse()
        {
            if (route?.wallRoute == "running") route.activations++;
        }

        internal void ObserveRetry(PilotResults results)
        {
            if (route?.fixedRoute == "running" || route?.wallRoute == "running")
                Abort(results, "Attempt reset before reaching the balcony");
        }

        private static long Uses(CoreRecord record)
        {
            long total = 0;
            foreach (var skill in record.skills) total += skill.validUseCount;
            return total;
        }

        private static bool Walk(BotKeyboard keys, CombatMotor2D player, float x, float tolerance = .19f)
        {
            float dx = x - player.Body.position.x;
            if (Mathf.Abs(dx) <= tolerance) return true;
            keys.Hold(dx < 0 ? KeyCode.LeftArrow : KeyCode.RightArrow);
            return false;
        }

        internal void ObserveHit(CombatMotor2D player, PilotResults result)
        {
            if (phase != Phase.Hit || route != result.loom) return;
            var v = player.Body.linearVelocity;
            result.hitVelocityX = v.x;
            result.hitVelocityY = v.y;
            result.hitGravity = player.Body.gravityScale;
            hitTick = Time.fixedTime;
            phase = Phase.Recover;
        }

        internal void Abort(PilotResults results, string reason)
        {
            if (string.IsNullOrEmpty(results.failure)) results.failure = roomId + " " + phase + ": " + reason;
            if (route?.fixedRoute == "running") route.fixedRoute = "fail";
            if (route?.wallRoute == "running") route.wallRoute = "fail";
        }

        internal bool Drive(FirstDepartureBot bot, BotKeyboard keys, PilotResults result)
        {
            var area = bot.Area;
            if (area == null) return false;
            if (area.Room.id == "velvet_01_entry" && entry == 0) entry = AutoTestRuntime.Now;
            bool loom = area.Room.id == result.loom.room;
            if (!loom && area.Room.id != result.gallery.room) return false;
            if (!loom && !result.arrivalOnShelf)
            {
                result.arrivalOnShelf = area.StandingOn("arrival_shelf") &&
                    bot.Record.storyFlags.Contains("scene_floor:velvet:gallery_entry_balcony");
                return true;
            }
            if (!GameInput.CanPlay || area.ActiveRow != null || area.RouteTarget != null) return false;
            var player = area.Rea;
            if (player.Health.Dead) return false;
            var controller = player.GetComponent<PlayerController2D>();
            var first = area.Room.Surface("tread_1").rect;
            var post = area.Room.Surface("kick_post").rect;
            var balcony = area.Room.Surface("balcony").rect;
            float goal = loom ? area.Room.Floor("loom_balcony_floor").x : area.Room.Floor("gallery_balcony_floor").x;
            float takeoff = first.x - 2;
            float start = loom ? takeoff : area.Room.Placement("sword_latch_1").x + .8f;
            float feet = player.Body.position.y;
            var defense = player.Defense.State;
            if (roomId != area.Room.id)
            {
                roomId = area.Room.id;
                route = loom ? result.loom : result.gallery;
                phase = Phase.Start;
                contactBefore = area.ContactSaveCount;
                observeCharge = jumpHeld = false;
                tread = 0;
            }
            if (observeCharge && !player.Grounded && !defense.AirDashUsed)
                throw new InvalidOperationException("Wall contact refunded the air dash");
            switch (phase)
            {
                case Phase.Hit:
                    Walk(keys, player, area.Room.Placement("notary_guard_1").x);
                    break;
                case Phase.Recover:
                    if (Time.fixedTime <= hitTick) break;
                    var velocity = player.Body.linearVelocity;
                    result.knockbackObserved = Mathf.Abs(result.hitVelocityX) > .1f && result.hitVelocityY > 0 &&
                        Mathf.Sign(velocity.x) == Mathf.Sign(result.hitVelocityX) && Mathf.Abs(velocity.x) > .1f &&
                        Mathf.Approximately(result.hitGravity, 3f) && Mathf.Approximately(player.Body.gravityScale, 3f);
                    if (!result.knockbackObserved) throw new InvalidOperationException("Knockback lost on the physics tick after a hit");
                    phase = Phase.Return;
                    break;
                case Phase.Start:
                case Phase.Return:
                    if (Walk(keys, player, start) && player.Grounded && feet < .15f)
                    {
                        if (phase == Phase.Start) route.fixedStartX = player.Body.position.x;
                        else route.wallStartX = player.Body.position.x;
                        kicksAtStart = controller.WallJumps;
                        started = AutoTestRuntime.Now;
                        if (phase == Phase.Start) { phase = Phase.ApproachTreads; route.fixedRoute = "running"; }
                        else
                        {
                            growthBefore = Uses(bot.Record);
                            route.activations = 0;
                            phase = Phase.ToWall;
                            route.wallRoute = "running";
                        }
                    }
                    break;
                case Phase.ApproachTreads:
                    if (Walk(keys, player, takeoff) && player.Grounded && feet < .15f) phase = Phase.Treads;
                    break;
                case Phase.Treads:
                    var id = tread < 2 ? "tread_" + (tread + 1) : "balcony";
                    var rect = area.Room.Surface(id).rect;
                    // Step off the key for a frame between jumps so each press is a real edge.
                    if (area.StandingOn(id))
                    {
                        jumpHeld = false;
                        if (++tread == 3) phase = Phase.FixedFinish;
                        else route.treads++;
                        break;
                    }
                    if (player.Grounded || jumpHeld) { keys.Hold(KeyCode.Space); jumpHeld = true; }
                    Walk(keys, player, rect.x + Mathf.Min(1.4f, rect.width * .5f));
                    break;
                case Phase.FixedFinish:
                case Phase.WallFinish:
                    if (!Walk(keys, player, goal) || !area.StandingOn("balcony")) break;
                    if (phase == Phase.FixedFinish)
                    {
                        route.fixedFinishX = player.Body.position.x;
                        route.fixedTopY = feet;
                        route.fixedKicks = controller.WallJumps - kicksAtStart;
                        if (route.fixedKicks != 0) throw new InvalidOperationException("Kick on the fixed path");
                        if (!loom)
                        {
                            var floor = area.Room.Floor("gallery_balcony_floor");
                            if (area.ContactSaveCount <= contactBefore || area.LastContactFloor != floor.id) break;
                            var store = new SkillSaveStore(bot.Session.SaveDirectory, bot.Session.Progression.Catalog, bot.Session.ActiveSlot);
                            var saved = store.Load();
                            result.contactFloor = floor.id;
                            result.slotReadback = store.LoadStatus == SaveLoadStatus.Loaded && saved?.schemaVersion == CoreRecord.SchemaVersion &&
                                saved.slotId == bot.Record.slotId && saved.lastStageId == area.Room.stage &&
                                saved.storyFlags.Contains("scene_floor:velvet:" + floor.id);
                            if (!result.slotReadback) throw new InvalidOperationException("Missing contact save in isolated slot");
                            result.entryToContactSeconds = area.LastContactTime - entry - result.comparisonSeconds;
                            result.checkpointInterval = result.entryToContactSeconds < 180 ? "below-target" :
                                result.entryToContactSeconds > 300 ? "above-target" : "within-target";
                            phase = Phase.LeaveCheckpoint;
                        }
                        else
                        {
                            comparisonStart = AutoTestRuntime.Now;
                            phase = Phase.Drop;
                        }
                        route.fixedSeconds = AutoTestRuntime.Now - started;
                        route.fixedRoute = "pass";
                        capture(loom ? "loom-fixed" : "gallery-fixed");
                    }
                    else
                    {
                        route.finishX = player.Body.position.x;
                        route.topY = feet;
                        route.wallKicks = controller.WallJumps - kicksAtStart;
                        route.growthDelta = Uses(bot.Record) - growthBefore;
                        route.landedCharged = !defense.AirDashUsed;
                        route.wallSeconds = AutoTestRuntime.Now - started;
                        if (!loom)
                        {
                            result.contactWrites = area.ContactSaveCount - contactBefore;
                            if (result.contactWrites != 1)
                                throw new InvalidOperationException("Repeated contact wrote the same checkpoint");
                        }
                        if (!route.WitnessedBothRoutes) throw new InvalidOperationException("Incomplete traversal witnesses: " + roomId);
                        route.wallRoute = "pass";
                        if (loom) result.comparisonSeconds = AutoTestRuntime.Now - comparisonStart;
                        capture(loom ? "loom-wall" : "gallery-wall");
                        phase = Phase.Exit;
                    }
                    break;
                case Phase.LeaveCheckpoint:
                    Walk(keys, player, (balcony.x + balcony.width + post.x) * .5f);
                    if (player.Grounded && feet < .15f) phase = Phase.TestLowerDoor;
                    break;
                case Phase.TestLowerDoor:
                    var arenaDoor = area.ForwardDoor;
                    if (arenaDoor == null || arenaDoor.room != "velvet_04_arena")
                        throw new InvalidOperationException("Expected the unlocked upper arena door");
                    if (Walk(keys, player, arenaDoor.x) && area.OnReturnFloor && feet < .15f)
                    {
                        keys.Tap(GameInput.InteractKey);
                        if (GameInput.InvestigateDown) phase = Phase.VerifyLowerDoor;
                    }
                    break;
                case Phase.VerifyLowerDoor:
                    // Travel commits the destination floor on the input frame and restores the room next frame.
                    result.lowerDoorRejected = area.Room.id == result.gallery.room &&
                        bot.Record.storyFlags.Contains("scene_floor:velvet:gallery_balcony_floor");
                    if (!result.lowerDoorRejected) throw new InvalidOperationException("Lower-floor input opened the upper arena door");
                    capture("gallery-lower");
                    keys.Tap(KeyCode.Backspace);
                    phase = Phase.Retry;
                    break;
                case Phase.Retry:
                    if (area.StandingOn("balcony") && Mathf.Abs(player.Body.position.x - goal) < .15f &&
                        bot.Record.storyFlags.Contains("scene_floor:velvet:gallery_balcony_floor"))
                    { result.retryOnBalcony = true; phase = Phase.Drop; }
                    break;
                case Phase.Exit:
                    if (!loom || feet < .15f) return false;
                    goto case Phase.Drop;
                case Phase.Drop:
                    Walk(keys, player, (balcony.x + balcony.width + post.x) * .5f);
                    if (phase == Phase.Drop && player.Grounded && feet < .15f) phase = Phase.Return;
                    break;
                case Phase.ToWall:
                    keys.Hold(KeyCode.RightArrow);
                    if (post.x - player.Body.position.x <= .36f && player.Grounded)
                    {
                        if (route.dashBlockedKick) phase = Phase.Launch;
                        else { keys.Hold(KeyCode.Space); phase = Phase.TestDash; }
                    }
                    break;
                case Phase.TestDash:
                    keys.Hold(KeyCode.RightArrow);
                    if (!controller.Sliding) { keys.Hold(KeyCode.Space); break; }
                    phase = Phase.CastChord; // Release Space for a fresh press with the V chord.
                    break;
                case Phase.CastChord:
                    keys.Hold(KeyCode.RightArrow); keys.Tap(KeyCode.V); keys.Tap(KeyCode.Space);
                    if (!GameInput.CastDown || !GameInput.JumpDown)
                        throw new InvalidOperationException("Did not deliver both cast and jump edges");
                    castTick = Time.fixedTime;
                    phase = Phase.CheckCast;
                    break;
                case Phase.CheckCast:
                    keys.Hold(KeyCode.RightArrow);
                    if (Time.fixedTime <= castTick) break;
                    route.castBlockedKick = controller.WallJumps == kicksAtStart && !player.Grounded;
                    if (!route.castBlockedKick) throw new InvalidOperationException("V chord started a wall kick");
                    phase = Phase.WaitDash;
                    break;
                case Phase.WaitDash:
                    keys.Hold(KeyCode.RightArrow);
                    keys.Tap(KeyCode.Space); keys.Tap(GameInput.DashKey);
                    if (!GameInput.DashDown || !GameInput.JumpDown)
                        throw new InvalidOperationException("Did not deliver both dash and jump edges");
                    phase = Phase.Land;
                    break;
                case Phase.Land:
                    keys.Hold(KeyCode.RightArrow);
                    if (defense.Phase == DefensePhase.Dash) route.dashBlockedKick = true;
                    if (controller.WallJumps != kicksAtStart) throw new InvalidOperationException("Kick during dash");
                    if (player.Grounded && !defense.BlocksActions && Time.time >= defense.DashReadyAt)
                    {
                        if (!route.dashBlockedKick) throw new InvalidOperationException("Dash state was not observed");
                        phase = loom ? Phase.Hit : Phase.Return;
                    }
                    break;
                case Phase.Launch:
                    keys.Hold(KeyCode.RightArrow); keys.Hold(KeyCode.Space);
                    if (!player.Grounded && feet >= 1.6f)
                    { keys.Tap(KeyCode.LeftShift); phase = Phase.SpendDash; }
                    break;
                case Phase.SpendDash:
                    keys.Hold(KeyCode.RightArrow); keys.Hold(KeyCode.Space);
                    if (defense.Phase == DefensePhase.Dash && defense.AirDashUsed)
                    { route.airDashSpent = observeCharge = true; phase = Phase.Slide; }
                    break;
                case Phase.Slide:
                    keys.Hold(KeyCode.RightArrow);
                    if (!controller.Sliding || player.Body.linearVelocity.y > -.5f) { keys.Hold(KeyCode.Space); break; }
                    route.slideSpeed = player.Body.linearVelocity.y;
                    phase = Phase.Kick;
                    break;
                case Phase.Kick:
                    keys.Hold(KeyCode.Space);
                    if (controller.WallJumps == kicksAtStart) { keys.Hold(KeyCode.RightArrow); break; }
                    route.heldUntilLanding = defense.AirDashUsed;
                    phase = Phase.WallFinish;
                    break;
            }
            if (phase == Phase.WallFinish && !player.Grounded)
            {
                // Do not cut the kick short at the key-up boundary.
                keys.Hold(KeyCode.Space);
                Walk(keys, player, goal);
            }
            if (observeCharge && player.Grounded)
            {
                if (!area.StandingOn("balcony")) throw new InvalidOperationException("Wall path landed below the balcony");
                observeCharge = false;
            }
            return true;
        }
    }
}
