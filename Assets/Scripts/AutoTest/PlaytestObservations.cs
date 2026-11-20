using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed partial class GameShell
    {
        internal int MenuIndex => selected;
        internal int OpeningCaption => Page == ShellPage.StoryDialogue ? openingScene?.CaptionIndex ?? -1 : -1;
        internal bool EmptyNewSlot => purpose == SlotPurpose.NewGame && selected < records.Length &&
            records[selected] == null && slotErrors[selected] == null;
        internal CoreRecord MenuRecord => selectedRecord;
        internal string MenuMessage => message;
    }

    public sealed partial class RepresentativeArea
    {
        internal CombatMotor2D Rea => player;
        internal GameObject ProbeLayout => layout;
        private Collider2D ProbeCollider(StorySurface s) =>
            surfaces.TryGetValue(s.id, out var box) ? box : fixedFloors.Find(f => f.name == s.id);
        internal void VerifyGalleryTile(StorySurface s)
        {
            var box = (BoxCollider2D)surfaces[s.id];
            var tile = box.GetComponentInChildren<SpriteRenderer>();
            if (tile == null || tile.transform.parent != box.transform || box.autoTiling ||
                (box.transform.lossyScale - Vector3.one).sqrMagnitude > .000001f ||
                (tile.size - box.size).sqrMagnitude > .000025f ||
                (tile.enabled && (((Vector2)(tile.bounds.size - box.bounds.size)).sqrMagnitude > .000025f ||
                    ((Vector2)(tile.bounds.center - box.bounds.center)).sqrMagnitude > .000025f)))
                throw new System.InvalidOperationException(Room.id + "/" + s.id + " gallery tile and box diverged");
        }
        internal Bounds ProbeSurfaceBounds(StorySurface s) => ProbeCollider(s).bounds;
        internal bool StandingOnGeneratedFloor(StorySurface s)
        {
            if (surfaces.ContainsKey(s.id)) return false;
            var box = ProbeCollider(s);
            var actor = player.GetComponent<Collider2D>();
            return player.Grounded && box.isActiveAndEnabled && box.IsTouching(actor) &&
                Mathf.Abs(actor.bounds.min.y - box.bounds.max.y) <= .12f;
        }

        internal string ClockSupportDescription()
        {
            if (player == null || !player.Grounded) return "-";
            var actor = player.GetComponent<Collider2D>();
            foreach (var box in surfaces.Values)
            {
                var b = box.bounds;
                if (box.IsTouching(actor) && Mathf.Abs(actor.bounds.min.y - b.max.y) <= .12f)
                {
                    var shape = (BoxCollider2D)box;
                    var rect = Room.Surface(box.name).rect;
                    return $"{box.name}[{b.min.x:F2},{b.max.x:F2}]@{b.max.y:F2}" +
                        $" authoredWidth={rect.width:F2} boxLocalWidth={shape.size.x:F2}" +
                        $" worldScaleX={box.transform.lossyScale.x:F2} autoTiling={shape.autoTiling}";
                }
            }
            return "-";
        }

        internal StorySurface[] ProbeTerrain()
        {
            var faces = new List<StorySurface>(Room.terrain ?? System.Array.Empty<StorySurface>());
            faces.RemoveAll(s => s.RequiredCuts > 0 && ProbeCollider(s).isTrigger);
            foreach (var box in fixedFloors)
            {
                if (!box.isActiveAndEnabled || box.attachedRigidbody != null || surfaces.ContainsKey(box.name)) continue;
                var b = box.bounds;
                // Low story planks overlay the walking lane and Walk handles their lip.
                if (faces.Exists(s => FixedSurfaceRoute.IsFloor(s) && b.min.x >= s.rect.x &&
                    b.max.x <= s.rect.x + s.rect.width && b.max.y >= s.rect.y + s.rect.height &&
                    b.max.y - (s.rect.y + s.rect.height) < .5f)) continue;
                faces.Add(new StorySurface { id = box.name, wallJump = box.name == "east_wall" || box.name == "west_wall",
                    rect = new StoryRect { x = b.min.x, y = b.min.y, width = b.size.x, height = b.size.y } });
            }
            return faces.ToArray();
        }
        internal bool OnOperaLift(out Bounds deck)
        {
            deck = default;
            if (raisedLift == null) return false;
            var lift = raisedLift.GetComponent<Collider2D>();
            if (!lift.IsTouching(player.GetComponent<Collider2D>())) return false;
            deck = lift.bounds;
            return Mathf.Abs(player.Body.position.y - deck.max.y) <= .12f;
        }

        internal bool RidingSurface(StorySurface surface) => surface.Moving &&
            surfaces[surface.id].IsTouching(player.GetComponent<Collider2D>());
        internal bool BellPlatePending(Vector2 feet)
        {
            foreach (var p in bellPlates)
                if (p.face.bellLatch > 0 && p.box.isTrigger && p.box.bounds.min.x <= feet.x && feet.x <= p.box.bounds.max.x &&
                    Mathf.Abs(p.box.bounds.max.y - feet.y) < .1f) return true;
            return false;
        }

        internal bool GalleryBridgeOpening()
        {
            foreach (var plate in bellPlates)
            {
                if (plate.face.latchCuts <= 0 || !plate.box.isTrigger) continue;
                int cuts = 0;
                foreach (var latch in latches) if (latch.State.Cleared) cuts++;
                if (cuts >= plate.face.latchCuts || Has("gallery_03_cleared")) return true;
            }
            return false;
        }

        internal WaveTarget2D BellTargetOnTurn(int turn)
        {
            float top = Room.SurfaceTop("lower_" + turn);
            return roomTargets.Find(t => !t.State.Cleared && Mathf.Abs(t.transform.position.y - top) < .2f);
        }
        internal WaveTarget2D GalleryTurnTarget(string surface)
        {
            var r = Room.Surface(surface).rect;
            float top = r.y + r.height;
            return roomTargets.Find(t => !t.State.Cleared && Mathf.Abs(t.transform.position.y - top) < .2f &&
                t.transform.position.x >= r.x && t.transform.position.x <= r.x + r.width);
        }
        internal WaveTarget2D ClockTargetOnTier => roomTargets.Find(t => !t.State.Cleared &&
            Mathf.Abs(t.transform.position.y - player.Body.position.y) < .2f);
        internal WaveTarget2D UnclearedClockTarget => roomTargets.Find(t => !t.State.Cleared);
        internal WaveTarget2D DistrictWaveTarget => Room == null ? null :
            (Room.stage == "clock" || Room.stage == "opera" || Room.stage == "bell" || Room.stage == "gallery") ? UnclearedClockTarget : RouteTarget;
        // Inactive targets still belong to the wave, including queued velvet entry enemies.
        internal int WaveTargetCount => roomTargets.Count + entryWave.Count;
        internal int ClearedWaveTargets
        {
            get
            {
                int cleared = 0;
                foreach (var t in roomTargets) if (t.State.Cleared) cleared++;
                foreach (var t in entryWave) if (t.State.Cleared) cleared++;
                return cleared;
            }
        }
        internal CombatMotor2D BoutOpponent => opponent.gameObject.activeSelf && FightNeeded() ? opponent : null;
        internal BenefactorTrialOpponent Bout => mikhailBout;
        internal string PendingAction => ReadyForAction ? ActionToken : null;
        internal bool OnReturnFloor => Safe && OnFixedFloor();
        internal StoryDoor ForwardDoor
        {
            get
            {
                string next = NextDoorId();
                foreach (var door in Room.exits)
                    if (door.room == next && Gate(door)) return door;
                return null;
            }
        }

        internal WaveTarget2D RouteTarget
        {
            get
            {
                if (NeedsEntryWave && entryMember < entryWave.Count) return entryWave[entryMember];
                if (HasBridge && !Has(BridgeFlag)) return latches.Find(t => !t.State.Cleared);
                if (GatheringSilver) return roomTargets.Find(t => !t.State.Cleared && t.State.NeedsSetup);
                return null;
            }
        }
    }
}
