using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed partial class RepresentativeArea
    {
        private const string AtticRoomId = "hub_attic";
        private const string Marta = "marta";
        private const string CandyPickup = "mint_candy_pickup";
        private const string CandyTray = "mint_candy_tray";
        private const string CopyEnvelope = "contract_copy_envelope";
        private const string BreakfastBoard = "breakfast_board";
        private const string StorageChest = "attic_storage_chest";
        internal bool InAttic => Room?.id == AtticRoomId;
        internal bool InAtticTutorial => InAttic && !Has("intro_complete");
        internal StoryMark AtticDialogueTarget
        {
            get
            {
                if (!InAttic) return null;
                if (InAtticTutorial && !Has("intro_attic_01_read") && !Has("mint_candy_placed")) return Room.Point(Marta);
                return Has("intro_copy_read") && !Has("intro_board_01_read") ? Room.Point(BreakfastBoard) : null;
            }
        }

        private string AtticDialogueHint(StoryMark target) =>
            Loc.Get(target.id == Marta ? "area.speaker.marta" : "area.target." + target.id) + Arrow(target.x);

        private static readonly Dictionary<string, Sprite> atticProps = new Dictionary<string, Sprite>();

        private GameObject AtticProp(string id, RectInt pixels, float x, float baseY, float height, int order)
        {
            if (!atticProps.TryGetValue(id, out var sprite))
            {
                var atlas = Resources.Load<Texture2D>("GameArt/sprites/attic-tutorial-props");
                if (atlas == null) throw new InvalidOperationException("Missing attic tutorial prop atlas");
                var frame = new Rect(pixels.x, atlas.height - pixels.yMax, pixels.width, pixels.height);
                sprite = Sprite.Create(atlas, frame, new Vector2(.5f, 0f), pixels.height, 0, SpriteMeshType.FullRect);
                atticProps.Add(id, sprite);
            }
            var go = Place(id, x, baseY);
            var picture = go.AddComponent<SpriteRenderer>();
            picture.sprite = sprite;
            picture.sortingOrder = order;
            go.transform.localScale = Vector3.one * height;
            return go;
        }

        private void BuildAtticFurniture()
        {
            var marta = Room.Point(Marta);
            fixtures[Marta] = PlaceMarker(Marta, marta.x, 0f, RoomArt.Picture("marta-attic"), 1.95f);

            var chest = Room.Surface(StorageChest).rect;
            surfaces[StorageChest].GetComponent<SpriteRenderer>().enabled = false;
            AtticProp("Oak chest", new RectInt(552, 194, 434, 239),
                chest.x + chest.width * .5f, chest.y, chest.height, 1);

            float trayX = Room.Point(CandyTray).x;
            float copyX = Room.Point(CopyEnvelope).x;
            float tableX = (trayX + copyX) * .5f;
            AtticProp("Tea table", new RectInt(1056, 54, 422, 432), tableX, 0f, 2.6f, 1);
            fixtures[BreakfastBoard] = AtticProp(BreakfastBoard, new RectInt(52, 534, 428, 464),
                Room.Point(BreakfastBoard).x, 0f, 2.0f, 1);

            var candyFrame = new RectInt(612, 692, 308, 136);
            fixtures[CandyPickup] = AtticProp(CandyPickup, candyFrame, Room.Point(CandyPickup).x, .035f, .19f, 3);
            fixtures["placed_candy"] = AtticProp("placed_candy", candyFrame, trayX + .12f, 1.54f, .13f, 3);
            fixtures[CopyEnvelope] = AtticProp(CopyEnvelope, new RectInt(1072, 646, 392, 240), copyX, 1.48f, .32f, 3);
        }

        private void RefreshAtticProps()
        {
            bool hasCandy = Has("mint_candy_placed");
            bool carryingCandy = ReadyForAction && ActionToken == "use:" + CandyTray;
            fixtures[CandyPickup].SetActive(!hasCandy && !carryingCandy);
            fixtures["placed_candy"].SetActive(hasCandy);
            fixtures[CopyEnvelope].SetActive(!Has("contract_copy_received"));
        }

        private string AtticActionHint()
        {
            if (ActionToken != "use:" + CandyPickup ||
                player.Body.position.x >= Room.Surface(StorageChest).rect.x) return null;
            string jump = GameInput.UsingGamepad ? "[A]" : GameInput.KeyLabel(GameInput.JumpKey);
            return Loc.Get("tutorial.attic_chest", jump);
        }
    }
}
