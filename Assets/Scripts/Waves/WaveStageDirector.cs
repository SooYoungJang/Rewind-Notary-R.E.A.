using UnityEngine;

namespace RewindNotary
{
    // One front-line target at a time. Reserves enter only after both originating contexts
    // close, so a multi-hit/area sequence cannot silently collapse the designed supply.
    public sealed class WaveStageDirector : MonoBehaviour
    {
        private WaveCatalog data;
        private CombatMotor2D player;
        private SkillSession session;
        private Font font;
        private Sprite square;
        private readonly GameObject[] barriers = new GameObject[3];
        private WaveTarget2D target;
        private int zoneIndex, waveIndex, groupIndex, memberIndex, attempt;
        private float readyAt;
        private bool zoneDone, complete;
        public bool Complete => complete;
        private WaveZone Zone => data.zones[zoneIndex];
        private WaveDefinition Wave => Zone.waves[waveIndex];
        public Vector2 Respawn => new Vector2(Zone.entryX, 0.15f);

        public void BuildWorld()
        {
            data = WaveCatalog.Load();
            square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2);
            float left = data.zones[0].left, right = data.zones[2].right;
            Box("Wave ground", new Vector2((left + right) * 0.5f, -1), new Vector2(right - left, 2), new Color(0.24f, 0.32f, 0.40f), true);
            Box("Left boundary", new Vector2(left, 5), new Vector2(0.5f, 16), Color.gray, true);
            for (int i = 0; i < data.zones.Length; i++)
            {
                barriers[i] = Box(data.zones[i].id + ":gate", new Vector2(data.zones[i].right, 5),
                    new Vector2(0.5f, 16), new Color(0.35f, 0.65f, 0.8f, 0.6f), true);
                Box(data.zones[i].id + ":entry", new Vector2(data.zones[i].entryX, 0.04f),
                    new Vector2(1.2f, 0.08f), Color.cyan, false);
            }
        }
        private GameObject Box(string id, Vector2 at, Vector2 size, Color color, bool solid)
        {
            var go = new GameObject(id); go.transform.SetParent(transform, false); go.transform.position = at;
            if (solid) { go.layer = LevelBuilder.GroundLayer; go.AddComponent<BoxCollider2D>().size = size; }
            var image = new GameObject("Visual"); image.transform.SetParent(go.transform, false); image.transform.localScale = size;
            var render = image.AddComponent<SpriteRenderer>(); render.sprite = square; render.color = color;
            return go;
        }
        public void Initialize(CombatMotor2D actor, SkillSession skills, Font hudFont)
        {
            player = actor; session = skills; font = hudFont;
            ResetAttempt(true);
        }
        public void ResetAttempt(bool fromStart)
        {
            RetireTarget();
            if (fromStart) zoneIndex = 0;
            waveIndex = groupIndex = memberIndex = 0; zoneDone = complete = false;
            attempt++; readyAt = Time.time;
            for (int i = 0; i < barriers.Length; i++) barriers[i].SetActive(i >= zoneIndex);
            EnterWave();
        }
        private void EnterWave()
        {
            session.EncounterId = "wave_test:" + Zone.id + ":" + Wave.id + ":attempt_" + attempt;
            session.Progression.EmitSimple("wave_start", "", session.EncounterId);
        }
        private void Update()
        {
            if (!GameInput.GameplayEnabled || player == null || player.Health.Dead || complete) return;
            if (zoneDone)
            {
                if (player.transform.position.x < data.zones[zoneIndex + 1].entryX) return;
                zoneIndex++; waveIndex = groupIndex = memberIndex = 0; zoneDone = false; EnterWave();
            }
            if (target != null)
            {
                if (!target.State.Cleared || !target.State.EffectsClosed) return;
                bool cross = target.State.CrossLoopFinish;
                session.Progression.EmitSimple("wave_target_clear", "", target.TargetId);
                // 1.8x only applies to reinforcement arrival. Count increment remains exactly one.
                float wait = data.handoffSeconds / (cross ? data.crossLoopSpeed : 1f);
                if (cross) session.Progression.EmitSimple("wave_cross_loop", "", target.TargetId + ":handoff_1.8x;count_1");
                RetireTarget(); readyAt = Time.time + wait;
                AdvanceRoster();
                return;
            }
            if (zoneDone || complete || Time.time < readyAt) return;
            var spawn = Wave.spawns[groupIndex];
            var go = new GameObject(session.EncounterId + ":" + groupIndex + ":" + memberIndex);
            go.transform.SetParent(transform, false); go.transform.position = new Vector3(spawn.x, 0, 0);
            target = go.AddComponent<WaveTarget2D>();
            target.Initialize(data.Archetype(spawn.archetypeId), spawn, player, square);
        }
        private void AdvanceRoster()
        {
            if (++memberIndex < Wave.spawns[groupIndex].count) return;
            memberIndex = 0;
            if (++groupIndex < Wave.spawns.Length) return;
            groupIndex = 0;
            session.Progression.EmitSimple("wave_complete", "", session.EncounterId);
            if (++waveIndex < Zone.waves.Length) { EnterWave(); return; }
            waveIndex = Zone.waves.Length - 1;
            session.Progression.EmitSimple("wave_zone_complete", SkillRules.BasicSword,
                Zone.id + ":actual=" + session.Progression.Record.Get(SkillRules.BasicSword).validUseCount + ":gate=" + Zone.gate);
            if (zoneIndex == data.zones.Length - 1) { complete = true; return; }
            zoneDone = true; barriers[zoneIndex].SetActive(false);
        }
        private void RetireTarget()
        {
            if (target == null) return;
            target.gameObject.SetActive(false); Destroy(target.gameObject); target = null;
        }
        private void OnGUI()
        {
            if (!GameInput.GameplayEnabled || session == null) return;
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
            var style = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            if (font != null) style.font = font;
            GUI.Box(new Rect(480, 278, 770, 122), "");
            GUI.Label(new Rect(496, 288, 738, 30), Loc.Get("wave.progress", Zone.Label, (waveIndex + 1), Zone.waves.Length), style);
            string hint = complete ? Loc.Get("wave.all_complete")
                : zoneDone ? Loc.Get("wave.zone_complete")
                : target != null ? target.Hint : Loc.Get("wave.next_enemy");
            GUI.Label(new Rect(496, 322, 738, 66), hint, style);
            GUI.matrix = previous;
        }
        private void OnDisable() { RetireTarget(); }
        private void OnDestroy() { if (square != null) Destroy(square); }
    }
}
