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
        private Sprite weave, brass;
        private Sprite BrassSprite()
        {
            if (brass == null)
            {
                var plate = Resources.Load<Texture2D>("GameArt/backgrounds/brass-plate");
                brass = Sprite.Create(plate, new Rect(0, 0, plate.width, plate.height), Vector2.one * .5f,
                    plate.width / 1.6f, 0, SpriteMeshType.FullRect);
            }
            return brass;
        }
        private readonly Dictionary<string, Sprite> rosterArt = new Dictionary<string, Sprite>();

        private Sprite RosterPicture(string id)
        {
            if (rosterArt.TryGetValue(id, out var sprite)) return sprite;
            if (id == "clock_switch")
            {
                var dial = Resources.Load<Texture2D>("GameArt/sprites/clock-switch");
                sprite = Sprite.Create(dial, new Rect(0, 0, dial.width, dial.height), Vector2.one * .5f, dial.height);
                rosterArt.Add(id, sprite);
                return sprite;
            }
            var atlas = Resources.Load<Texture2D>("GameArt/sprites/velvet-roster");
            // The four poses have different widths. Keep each silhouette inside its own UV window.
            float left, right;
            switch (id)
            {
                case "sword_latch": left = 0; right = .291f; break;
                case "notary_guard": left = .291f; right = .495f; break;
                case "shell_sentry": left = .51f; right = .735f; break;
                case "reflection_statue": left = .76f; right = 1; break;
                default: return square;
            }
            var r = new Rect(left * atlas.width, 0, (right - left) * atlas.width, atlas.height);
            sprite = Sprite.Create(atlas, r, Vector2.one * .5f, atlas.height, 0, SpriteMeshType.FullRect);
            rosterArt.Add(id, sprite);
            return sprite;
        }

        private Sprite WeaveSprite()
        {
            if (weave != null) return weave;
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool hem = y == 30 || (y == 28 && x % 8 < 5);
                    int diamond = Mathf.Abs(x % 16 - 8) + Mathf.Abs(y % 16 - 8);
                    var color = hem ? new Color(1f,.85f,.56f) : diamond == 7 ? new Color(.94f,.70f,.62f) :
                        (x + y) % 4 < 2 ? new Color(.76f,.57f,.66f) : new Color(.52f,.35f,.45f);
                    texture.SetPixel(x, y, color);
                }
            texture.Apply(false, true);
            weave = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size, 0, SpriteMeshType.FullRect);
            return weave;
        }

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
                    surface.wallJump ? new Color(.94f,.70f,.38f) : Room.stage == "opera" ? new Color(.83f,.83f,.92f) : Room.stage == "clock" ? new Color(.92f,.91f,.79f) : new Color(.85f,.54f,.52f), true);
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
