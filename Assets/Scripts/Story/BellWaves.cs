using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed partial class RepresentativeArea
    {
        private readonly List<BelfryWave> bellWaves = new List<BelfryWave>();
        private Sprite waveInk;

        private sealed class BellPlate
        {
            internal StorySurface face;
            internal BoxCollider2D box;
            internal SpriteRenderer picture, ring;
        }
        private readonly List<BellPlate> bellPlates = new List<BellPlate>();
        private float plateStart;
        internal int BellPlateRevision { get; private set; }

        private void UpdateBellPlates()
        {
            if (bellPlates.Count == 0) return;
            int cuts = 0;
            foreach (var latch in latches) if (latch.State.Cleared) cuts++;
            float beat = Mathf.Repeat(Time.fixedTime - plateStart, 4f);
            foreach (var p in bellPlates)
            {
                bool bolted = p.face.bellBeat ? cuts >= 2 || Has("bell_03_cleared") :
                    cuts >= p.face.RequiredCuts || Has(p.face.latchCuts > 0 ? "gallery_03_cleared" : "bell_02_cleared");
                bool open = bolted || p.face.bellBeat && (beat >= .8f && beat < 3.2f || StandingOn(p.face.id) && !p.box.isTrigger);
                if (p.box.isTrigger == open)
                {
                    p.box.isTrigger = !open;
                    if (p.face.RequiredCuts > 0) BellPlateRevision++;
                }
                p.picture.enabled = open;
                p.ring.enabled = open || p.face.bellBeat && beat < .8f;
                float spread = p.face.bellBeat && !bolted ? Mathf.Min(1, beat / .8f) : 1;
                p.ring.transform.localScale = new Vector3(.2f + spread * .8f, .26f, 1);
            }
        }

        private void BuildBellWaves()
        {
            bellWaves.Clear();
            bellPlates.Clear();
            plateStart = Time.fixedTime;
            if ((Room.stage == "bell" || Room.stage == "gallery") && Room.terrain != null)
                foreach (var s in Room.terrain)
                {
                    if (s.RequiredCuts == 0 && !s.bellBeat) continue;
                    var box = (BoxCollider2D)surfaces[s.id];
                    var ring = Place(s.id + "_rings", s.rect.x + s.rect.width / 2, Room.SurfaceTop(s.id) + .12f).AddComponent<SpriteRenderer>();
                    ring.sprite = s.latchCuts > 0 ? OperaSprite("gallery-drybrush", 2.8f) : OperaSprite("bell-dust", 5f);
                    ring.sortingOrder = 3;
                    var picture = box.GetComponentInChildren<SpriteRenderer>();
                    // A changing sound bridge can catch a jump but cannot own a return checkpoint.
                    fixedFloors.Remove(box);
                    box.isTrigger = true;
                    picture.enabled = ring.enabled = false;
                    bellPlates.Add(new BellPlate { face = s, box = box, picture = picture, ring = ring });
                }
            if (Room.bellWaves == null || InFieldTrial) return;
            if (waveInk == null)
                waveInk = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * .5f, 2);
            foreach (var definition in Room.bellWaves)
                bellWaves.Add(new BelfryWave(this, definition));
        }

        internal bool BellWaveActive => bellWaves.Count != 0 && bellWaves[0].Active;
        internal float FirstWaveSafeAt => bellWaves.Count == 0 ? -1 : bellWaves[0].SafeAt;
        internal float FirstWavePassedAt => bellWaves.Count == 0 ? -1 : bellWaves[0].PassedAt;

        private sealed class BelfryWave
        {
            private readonly BellWaveDefinition data;
            private readonly SpriteRenderer weight, front;
            private readonly GameObject outline;
            private readonly float left, right, floor;
            private float elapsed = -1f, rest;
            private bool struck, tookRefuge;
            internal bool Active => elapsed >= 0;
            internal float SafeAt { get; private set; } = -1;
            internal float PassedAt { get; private set; } = -1;

            internal BelfryWave(RepresentativeArea area, BellWaveDefinition definition)
            {
                data = definition;
                var deck = area.Room.Surface(data.surface).rect;
                left = deck.x;
                right = deck.x + deck.width;
                floor = area.Room.SurfaceTop(data.surface);
                weight = area.Place("clapper_" + data.surface, data.atX, floor + 3.4f).AddComponent<SpriteRenderer>();
                weight.sprite = area.OperaSprite("bell-weight", .9f);
                weight.sortingOrder = 3;
                outline = area.Place("wave_outline_" + data.surface, 0, 0);
                var amber = new Color(1f, .82f, .40f);
                void Edge(string id, float x, float y, float w, float h, Color color)
                {
                    var stroke = area.Place(id, x, y);
                    stroke.transform.SetParent(outline.transform, true);
                    var ink = stroke.AddComponent<SpriteRenderer>();
                    ink.color = color;
                    ink.sprite = area.waveInk;
                    stroke.transform.localScale = new Vector3(w, h, 1);
                    ink.sortingOrder = 5;
                }
                Edge("lower_edge", left + deck.width / 2, floor + .06f, deck.width, .08f, amber);
                Edge("upper_edge", left + deck.width / 2, floor + data.height, deck.width, .06f, amber);
                Edge("west_edge", left, floor + data.height / 2, .07f, data.height, amber);
                Edge("east_edge", right, floor + data.height / 2, .07f, data.height, amber);
                Edge("refuge_lip", data.atX, area.Room.SurfaceTop(data.refuge) + .07f, 2.2f, .09f, Color.white);
                outline.SetActive(false);
                front = area.Place("wave_front_" + data.surface, left, floor).AddComponent<SpriteRenderer>();
                front.sprite = area.waveInk;
                front.transform.localScale = new Vector3(.32f, data.height, 1);
                front.color = new Color(.88f, 1f, .97f);
                front.sortingOrder = 7;
                front.enabled = false;
            }

            internal void Tick(RepresentativeArea area, CombatMotor2D actor)
            {
                if (elapsed < 0)
                {
                    rest -= Time.fixedDeltaTime;
                    if (rest > 0 || !area.StandingOn(data.surface) || Mathf.Abs(actor.Body.position.x - data.atX) > 3f) return;
                    elapsed = 0;
                    struck = tookRefuge = false;
                    if (PassedAt < 0) SafeAt = -1;
                    outline.SetActive(true);
                }
                float progress = Mathf.Clamp01(elapsed / data.windup);
                weight.transform.localRotation = Quaternion.Euler(0, 0, 38f * Mathf.Sin(progress * Mathf.PI));
                weight.transform.position = new Vector2(data.atX + .75f * Mathf.Sin(progress * Mathf.PI), floor + 3.4f - progress);
                bool onRefuge = area.StandingOn(data.refuge) && Mathf.Abs(actor.Body.position.x - data.atX) < .8f;
                if (onRefuge && !tookRefuge && PassedAt < 0) { tookRefuge = true; SafeAt = elapsed; }
                if (elapsed >= data.windup)
                {
                    float x = left + (elapsed - data.windup) * data.speed;
                    float previous = Mathf.Max(left, x - data.speed * Time.fixedDeltaTime);
                    if (PassedAt < 0 && !struck && onRefuge && previous <= data.atX && x >= data.atX)
                        PassedAt = elapsed;
                    front.enabled = true;
                    front.transform.position = new Vector2(Mathf.Min(x, right), floor + data.height / 2);
                    var body = actor.GetComponent<Collider2D>().bounds;
                    if (!struck && body.max.x >= previous - .16f && body.min.x <= Mathf.Min(right, x) + .16f &&
                        body.max.y >= floor && body.min.y < floor + data.height)
                    {
                        struck = true;
                        actor.Health.Hit(1, front.transform.position);
                    }
                    if (x >= right)
                    {
                        elapsed = -1;
                        rest = 1.6f;
                        front.enabled = false;
                        outline.SetActive(false);
                        weight.transform.position = new Vector2(data.atX, floor + 3.4f);
                        return;
                    }
                }
                elapsed += Time.fixedDeltaTime;
            }
        }
    }
}
