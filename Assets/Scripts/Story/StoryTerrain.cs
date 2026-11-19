using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed partial class RepresentativeArea
    {
        private readonly Dictionary<string, Collider2D> surfaces = new Dictionary<string, Collider2D>();
        private readonly List<(GameObject actor, StoryRect zone)> activationZones = new List<(GameObject, StoryRect)>();
        internal int ContactSaveCount { get; private set; }
        internal string LastContactFloor { get; private set; }
        internal double LastContactTime { get; private set; }
        private string touchingCheckpoint;
        private readonly Dictionary<string, Sprite> rosterArt = new Dictionary<string, Sprite>();

        private void BuildSurfaces()
        {
            surfaces.Clear();
            touchingCheckpoint = null;
            activationZones.Clear();
            if (Room.terrain == null) return;
            foreach (var surface in Room.terrain)
            {
                var r = surface.rect;
                var go = Block(surface.id, r.x + r.width / 2, r.y + r.height / 2, r.width, r.height,
                    surface.wallJump ? new Color(.94f,.70f,.38f) : Room.stage == "gallery" ? Color.white : Room.stage == "opera" ? new Color(.83f,.83f,.92f) : Room.stage == "bell" ? new Color(.80f,.71f,.52f) : Room.stage == "clock" ? new Color(.92f,.91f,.79f) : new Color(.85f,.54f,.52f), true);
                var box = go.GetComponent<BoxCollider2D>();
                var face = go.AddComponent<AuthoredSurface>();
                if (surface.rampRise != 0)
                {
                    // The authored rectangle starts at the low lip. Rotate the top edge about that lip.
                    var tangent = new Vector2(r.width, surface.rampRise);
                    var normal = Vector2.Perpendicular(tangent.normalized);
                    go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);
                    go.transform.position = new Vector2(r.x, r.y + r.height) + tangent / 2 - normal * (r.height / 2);
                    box.size = new Vector2(tangent.magnitude, r.height);
                    go.GetComponentInChildren<SpriteRenderer>().size = box.size;
                    face.RampNormal = normal;
                    fixedFloors.Remove(box);
                }
                face.Kickable = surface.wallJump && !surface.Moving && !surface.oneWay;
                surfaces.Add(surface.id, box);
                if (surface.Moving)
                {
                    fixedFloors.Remove(box);
                    go.AddComponent<StoryMovingSurface>().Initialize(surface);
                }
                if (surface.oneWay)
                {
                    box.usedByEffector = true;
                    var effector = go.AddComponent<PlatformEffector2D>();
                    effector.useOneWay = true;
                    effector.surfaceArc = 150f;
                }
            }
        }

        internal bool StandingOn(string surface)
        {
            if (!surfaces.TryGetValue(surface, out var box) || !box.enabled || box.attachedRigidbody != null) return false;
            var actor = player.GetComponent<Collider2D>();
            return player.Grounded && Mathf.Abs(actor.bounds.min.y - box.bounds.max.y) <= .12f && box.IsTouching(actor);
        }

        internal bool StandingOnRamp(StorySurface ramp)
        {
            var floor = surfaces[ramp.id];
            return floor.GetComponent<AuthoredSurface>().SupportsRamp(player, player.GetComponent<CapsuleCollider2D>(), floor);
        }

        private float FloorDistance(StoryMark floor) => Vector2.Distance(player.Body.position, new Vector2(floor.x, LandingY(floor.id)));

        private bool SaveContactFloor()
        {
            foreach (var floor in Room.floors)
            {
                if (!floor.contactSave || Distance(floor.x) > .8f || !StandingOn(floor.surface)) continue;
                if (touchingCheckpoint == floor.id) return true;
                touchingCheckpoint = floor.id;
                if (Has(FloorPrefix + floor.id)) return true;
                Checkpoint(Room, floor.id);
                session.Save();
                if (!session.LastSaveSucceeded) return false;
                LastContactFloor = floor.id;
                LastContactTime = Time.realtimeSinceStartupAsDouble;
                ContactSaveCount++;
                return true;
            }
            touchingCheckpoint = null;
            return true;
        }

        private void UpdateActivationZones()
        {
            foreach (var entry in activationZones)
            {
                bool active = entry.zone.Contains(player.Body.position);
                if (entry.actor.activeSelf != active) entry.actor.SetActive(active);
            }
        }
    }
}
