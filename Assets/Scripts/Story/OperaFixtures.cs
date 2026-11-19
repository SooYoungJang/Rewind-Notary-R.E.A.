using UnityEngine;

namespace RewindNotary
{
    public sealed partial class RepresentativeArea
    {
        private BoxCollider2D lightShutter;
        private SpriteRenderer lightCone, shutterCloth;
        private float cueStart;
        private Transform maskingCloth;
        private StorySurface ventFloor;
        private bool ventBoosted;
        private const float VentX = 26f, VentSpeed = 16f;
        private StoryDoor roofCue;
        private bool roofLampLit;
        private SpriteRenderer roofSpot;
        private float CueBeat => Mathf.Repeat(Time.fixedTime - cueStart, 6f);
        private bool CueShade => CueBeat >= 2.8f;
        private bool RoofDoorOpen => Has("opera_return_02_done") || roofLampLit && CueShade;
        internal bool ChandelierShaded => lightShutter != null && CueShade && !lightShutter.enabled;
        private Sprite OperaSprite(string resource, float width)
        {
            if (rosterArt.TryGetValue(resource, out var sprite)) return sprite;
            var art = Resources.Load<Texture2D>("GameArt/backgrounds/" + resource);
            sprite = Sprite.Create(art, new Rect(0, 0, art.width, art.height), Vector2.one * .5f,
                art.width / width, 0, SpriteMeshType.FullRect);
            rosterArt.Add(resource, sprite);
            return sprite;
        }

        private void OperaLampHousing(string id, float x, float y)
        {
            var socket = Place(id + "_socket", x, y).AddComponent<SpriteRenderer>();
            socket.sprite = OperaSprite("opera-lamp", 1.4f);
            socket.color = new Color(.71f,.68f,.84f);
            socket.sortingOrder = 3;
        }

        private void DrawDropHatch(float x, float y)
        {
            var rim = new Color(.82f,.72f,.61f);
            Block("hatch_header", x, y + 1.3f, 2.5f, .24f, rim);
            for (int n = 0; n < 3; n++)
                Block("hatch_rung", x, y - n * .42f, 1.4f, .12f, rim);
        }

        private void BuildOperaFixtures()
        {
            lightShutter = null;
            roofCue = System.Array.Find(Room.exits, door => door.waltzCue);
            roofLampLit = false;
            cueStart = Time.fixedTime;
            if (roofCue != null)
            {
                var socket = Place("roof_masking_spot", roofCue.x, roofCue.approach.y + 3f);
                roofSpot = socket.AddComponent<SpriteRenderer>();
                roofSpot.sprite = OperaSprite("opera-beam", 4);
                roofSpot.sortingOrder = 2;
            }
            ventFloor = Room.Surface("gutter");
            ventBoosted = false;
            if (ventFloor != null)
            {
                float lip = Room.SurfaceTop(ventFloor.id);
                var vent = Place("roof_vent", VentX, lip + 1.62f).AddComponent<SpriteRenderer>();
                vent.sprite = OperaSprite("opera-beam", 4);
                vent.flipY = true;
                vent.transform.localScale = new Vector3(.56f,.55f,1);
                vent.color = new Color(.70f,.86f,1f,.56f);
                vent.sortingOrder = -1;
                Block("vent_grille", VentX, lip + .03f, 2.4f, .06f, new Color(.65f,.75f,.82f));
            }
            var pendant = Room.Surface("pendant");
            if (Room.stage != "opera" || pendant == null) return;
            float y = Room.SurfaceTop(pendant.id);
            float x = pendant.rect.x + pendant.rect.width / 2;
            var span = surfaces[pendant.id].transform;
            // The moving deck carries the two suspended luminaires as well as its passenger.
            foreach (float offset in new[] { -3.8f, 3.8f })
            {
                var light = Place("crystal", x + offset, y - 1.1f);
                light.transform.SetParent(span, true);
                light.AddComponent<SpriteRenderer>().sprite = OperaSprite("opera-chandelier", 2.25f);
            }
            const float height = 6f;
            var shutter = Block("masking_curtain", x, y + height / 2, .45f, height,
                new Color(.39f,.19f,.32f), true);
            lightShutter = shutter.GetComponent<BoxCollider2D>();
            fixedFloors.Remove(lightShutter);
            shutterCloth = shutter.GetComponentInChildren<SpriteRenderer>();
            lightCone = Place("follow_spot", x, y + height / 2).AddComponent<SpriteRenderer>();
            lightCone.sprite = OperaSprite("opera-beam", 4);
            lightCone.sortingOrder = 2;
            maskingCloth = Block("overhead_mask", x + 2, y + height, 2.1f, .48f, new Color(.42f,.24f,.32f)).transform;
            UpdateOperaShutter();
        }

        private void UpdateOperaShutter()
        {
            if (ventFloor != null)
            {
                var body = player.Body;
                float aboveGrille = body.position.y - Room.SurfaceTop(ventFloor.id);
                if (aboveGrille <= .06f || Mathf.Abs(body.position.x - VentX) > 1.2f) ventBoosted = false;
                else if (!ventBoosted && aboveGrille < .6f && body.linearVelocity.y > 0 && GameInput.JumpHeld)
                {
                    body.linearVelocity = new Vector2(body.linearVelocity.x, VentSpeed);
                    ventBoosted = true;
                }
            }
            bool warning = CueBeat < 1f;
            bool shade = CueShade;
            if (roofCue != null)
            {
                roofSpot.color = !roofLampLit ? new Color(.43f,.53f,.75f,.18f) :
                    shade ? new Color(.54f,.65f,.91f,.10f) : warning ? Color.yellow : Color.white;
            }
            if (lightShutter == null) return;
            // A walker already inside the portiere can finish clearing its edge.
            bool inside = Mathf.Abs(player.Body.position.x - lightShutter.transform.position.x) < .7f;
            lightShutter.enabled = !shade && (lightShutter.enabled || !inside);
            shutterCloth.enabled = lightShutter.enabled;
            var mask = maskingCloth.position;
            mask.x = Mathf.MoveTowards(mask.x, lightShutter.transform.position.x + (shade ? 0 : 2), 8 * Time.fixedDeltaTime);
            maskingCloth.position = mask;
            lightCone.color = shade ? new Color(.54f,.65f,.91f,.10f) :
                warning ? new Color(.96f,.62f,.32f,.50f) : new Color(1,.96f,.84f,.90f);
        }
    }
}
