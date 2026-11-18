using UnityEngine;

namespace RewindNotary
{
    public sealed class BenefactorSupportController : MonoBehaviour
    {
        private CombatMotor2D player, enemy;
        private SkillSession session;
        private System.Func<string> notice;
        public string Notice => notice?.Invoke();
        public void Initialize(CombatMotor2D actor, CombatMotor2D opponent, SkillSession skills)
        { player = actor; enemy = opponent; session = skills; }
        private void Update()
        {
            if (!GameInput.CanPlay || !GameInput.SupportDown || player.Health.Dead || session.PreviewStage != 0) return;
            var d = BenefactorTrialCatalog.Load().ForSkill(player.BenefactorSkills.InputSnapshot.SupportSkillId);
            if (d == null) return;
            if (!session.Progression.Trials.OrdinaryCombat) { notice = () => Loc.Get("support.ordinary_only"); return; }
            if (!BenefactorTrialProgress.Has(session.Progression.Record, d, "charged")) { notice = () => Loc.Get("support.no_charge"); return; }
            var s = d.support;
            bool inRange = !enemy.Health.Dead && SkillGeometry.Area(player.Body.position + Vector2.up, s.radius)
                .Intersects(enemy.GetComponent<Collider2D>().bounds);
            WaveTarget2D fieldTarget = null;
            if (!inRange && s.effect != "heal" && RepresentativeScript.IsDistrict(session.Progression.Record.lastStageId))
            {
                var reach = SkillGeometry.Area(player.Body.position + Vector2.up, s.radius);
                fieldTarget = WaveCombatTargets.All.Find(t => t.CanReceiveSupport && reach.Intersects(t.Bounds));
            }
            bool possible = s.effect == "heal" ? player.Health.Current < player.Health.Maximum : inRange || fieldTarget != null;
            if (!possible) { notice = () => Loc.Get("support.no_target"); return; }
            // Persist consumption BEFORE any world effect. A failed write does not grant free assistance.
            if (!session.Progression.Trials.ReserveSupport(d)) return;
            if (!session.LastSaveSucceeded) { session.Progression.Trials.RefundSupport(d); notice = () => Loc.Get("support.save_failed"); return; }
            bool applied = s.effect == "heal" ? player.Health.Heal(s.heal)
                : fieldTarget != null ? fieldTarget.ReceiveSupport(s)
                : s.effect == "bind" ? enemy.GetComponent<StitchBinding>().TryBind(s.seconds)
                : s.effect == "freeze" ? enemy.Temporal.TryFreeze(s.seconds)
                : enemy.Health.Hit(s.damage, player.Body.position);
            if (!applied) { session.Progression.Trials.RefundSupport(d); notice = () => Loc.Get("support.invalid_effect"); return; }
            // Support has no UseContext: neither growth, trial proofs nor a renewal receipt is created.
            session.Progression.EmitSimple("benefactor_support", d.skillId, s.id);
            notice = () => Loc.Get("support.used_notice", d.DisplayName, s.Label);
            SkillStroke.Show(player.Body.position + Vector2.up, s.effect == "heal" ? player.Body.position + Vector2.up * 2 : fieldTarget != null ? (Vector2)fieldTarget.Bounds.center : enemy.Body.position + Vector2.up,
                Color.green, 0.7f);
        }
    }
}
