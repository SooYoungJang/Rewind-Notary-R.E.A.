using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed class CasselArena : MonoBehaviour
    {
        private Sprite sprite;
        private readonly List<SpriteRenderer> mirrors = new List<SpriteRenderer>();
        private readonly Dictionary<StoryDoor, RoomDoorView> doors = new Dictionary<StoryDoor, RoomDoorView>();
        private SpriteRenderer imprint;
        private SkillSession session;
        public void Build(SkillSession skills)
        {
            session = skills; var d = CasselDefinition.Load();
            sprite = RoomArt.Tile("cassel", false);
            // One continuous floor under every temporary surface; no skill-dependent path or pit.
            Block("심판석 고정 바닥", (d.arenaMin + d.arenaMax) / 2, -1, d.arenaMax - d.arenaMin, 2);
            Block("왼쪽 낮은 단차", d.arenaMin + 1.5f, .6f, 3, 1.2f);
            Block("오른쪽 낮은 단차", d.arenaMax - 1.5f, .6f, 3, 1.2f);
            Block("왼쪽 경계", d.arenaMin - .25f, 10, .5f, 24);
            Block("오른쪽 경계", d.arenaMax + .25f, 10, .5f, 24);
            var room = RepresentativeScript.Load().Room("court_04_tribunal");
            foreach (var exit in room.exits)
            {
                var door = new GameObject(exit.room);
                door.transform.SetParent(transform, false);
                door.transform.position = new Vector2(exit.x, 1.3f);
                doors.Add(exit, new RoomDoorView(door, room.stage));
            }
            RefreshDoors();
            for (int i = 0; i < 5; i++) mirrors.Add(Visual("반사층 " + BenefactorIds.All[i], new Vector2(43 + i * 1.7f, 5), new Vector2(.9f, 2)));
            imprint = Visual("레아 각인 출력", new Vector2(47, 6.5f), new Vector2(10, .12f));
        }
        private SpriteRenderer Visual(string label, Vector2 at, Vector2 size)
        {
            var go = new GameObject(label); go.transform.SetParent(transform, false); go.transform.position = at;
            var render = go.AddComponent<SpriteRenderer>(); render.sprite = sprite; render.drawMode = SpriteDrawMode.Sliced;
            render.size = size; render.sortingOrder = -1; return render;
        }
        private void Block(string label, float x, float y, float width, float height)
        {
            var visual = Visual(label, new Vector2(x, y), new Vector2(width, height));
            visual.drawMode = SpriteDrawMode.Tiled;
            visual.size = new Vector2(width, height);
            visual.gameObject.layer = LevelBuilder.GroundLayer;
            visual.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(width, height);
        }
        private void Update()
        {
            if (session == null) return;
            RefreshDoors();
            var state = session.Finale.State;
            for (int i = 0; i < mirrors.Count; i++) mirrors[i].color = state.CanUse(BenefactorIds.All[i])
                ? new Color(.5f, .65f, .9f, .7f) : new Color(.2f, .3f, .4f, .08f);
            float theme = CasselDefinition.Load().Profile(state.returnCount).reaTheme;
            imprint.color = new Color(.2f, 1f, .85f, .15f + theme * .85f);
            imprint.size = new Vector2(1 + theme * 9, .12f + theme * .12f);
        }
        private void RefreshDoors()
        {
            bool settled = FinaleEndingEvaluator.Evaluate(session.Finale.State) != FinaleEnding.None;
            foreach (var entry in doors)
                entry.Value.Show(string.IsNullOrEmpty(entry.Key.gate) || entry.Key.gate == "settled" && settled);
        }
    }
}
