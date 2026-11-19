using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    // Shell owns pausing. Inspect before defense/attacks; never add physics targets or growth effects.
    [DefaultExecutionOrder(-150)]
    public sealed class InvestigationWorld : MonoBehaviour
    {
        private CombatMotor2D player;
        private SkillSession session;
        private GameShell shell;
        private Font font;
        private readonly List<InvestigationPointDefinition> points = new List<InvestigationPointDefinition>();
        private readonly List<SpriteRenderer> markers = new List<SpriteRenderer>();
        private InvestigationPointDefinition nearest;
        public void Initialize(CombatMotor2D actor, SkillSession skills, GameShell owner, Font uiFont)
        {
            player = actor; session = skills; shell = owner; font = uiFont;
            foreach (var p in session.Investigation.Catalog.points)
            {
                if (p.stageId != session.Progression.Record.lastStageId) continue;
                var go = new GameObject(p.id); go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(p.x, p.y, 0);
                go.transform.localScale = p.kind == "document" ? new Vector3(.55f, .35f, 1)
                    : p.kind == "imprint" ? new Vector3(.32f, .65f, 1) : new Vector3(.65f, 1f, 1);
                if (p.kind == "imprint") go.transform.rotation = Quaternion.Euler(0, 0, 45);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = RoomArt.Picture(p.kind == "imprint" ? "seam-marker" : "notary-desk-props");
                renderer.sortingOrder = 3;
                points.Add(p); markers.Add(renderer);
            }
        }
        private bool CanInspect => player != null && !player.Health.Dead && player.Grounded && !player.Attacking
            && !player.Stunned && !player.Defense.BlocksActions && !player.BenefactorSkills.BlocksActions;
        private void Update()
        {
            if (session == null || !shell.Playing || !GameInput.CanPlay) return;
            nearest = null; float best = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                bool collected = p.kind != "core" && session.Investigation.HasClue(p.clueId);
                markers[i].color = collected ? new Color(.42f, .5f, .54f) : p.kind == "core"
                    ? new Color(.3f, .9f, .82f) : new Color(.5f, .72f, 1f);
                float distance = Vector2.Distance(player.transform.position, new Vector2(p.x, p.y));
                if (distance <= p.radius && distance < best) { nearest = p; best = distance; }
            }
            if (!CanInspect || nearest == null || !GameInput.InvestigateDown || GameInput.RetryDown
                || GameInput.JumpDown || GameInput.AttackDown || GameInput.CastDown || GameInput.DashDown || GameInput.ParryDown) return;
            if (nearest.kind != "core") session.Investigation.TryCollect(nearest.clueId);
            shell.OpenInvestigation(nearest.clueId, nearest.kind == "core");
        }
        private void OnGUI()
        {
            if (session == null || !shell.Playing) return;
            var previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
            var style = new GUIStyle(GUI.skin.label) { font = font, fontSize = 16, wordWrap = true };
            GUI.Box(new Rect(20, 500, 380, 100), "");
            GUI.Label(new Rect(30, 506, 360, 26), session.Investigation.ProgressLabel, style);
            GUI.Label(new Rect(30, 534, 360, 60), nearest == null ? Loc.Get("input.investigation")
                : nearest.Label + "\n" + (CanInspect ? Loc.Get("input.inspect_action", (nearest.kind == "core" ? Loc.Get("investigation.read_core") : session.Investigation.HasClue(nearest.clueId) ? Loc.Get("investigation.reread") : Loc.Get("investigation.inspect"))) : Loc.Get("investigation.wait_for_action")), style);
            GUI.matrix = previous;
        }
    }
}
