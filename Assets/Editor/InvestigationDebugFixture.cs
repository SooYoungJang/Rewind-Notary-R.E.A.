#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace RewindNotary
{
    public static class InvestigationDebugFixture
    {
        [MenuItem("Rewind Notary/Investigation/Prepare isolated deep-read fixture")]
        private static void Prepare()
        {
            var session = Object.FindFirstObjectByType<SkillSession>();
            if (!Application.isPlaying || session == null || session.Progression.Record.lastStageId != "prototype")
            { Debug.LogWarning("테스트 스테이지를 Play Mode로 실행한 뒤 사용하세요."); return; }
            // Explicit Editor-only fixture: seed/copied state is isolated BEFORE any receipts are granted.
            session.SetPreview(0);
            session.Progression.SeedBoundary(true);
            foreach (string id in BenefactorIds.All)
            {
                var use = session.Progression.BeginUse(id);
                session.Progression.Apply(use, new CombatEffectResult(EffectTargetKind.TerrainMechanism, "investigation_fixture", true));
                session.Progression.EndUse(use);
            }
            foreach (var clue in session.Investigation.Catalog.clues) session.Investigation.TryCollect(clue.clueId);
            session.Investigation.CompleteMiddleInvestigation();
            session.Save();
            Debug.Log("조사 검증 저장 준비: C=5, I=1 훅, B=2(399회). 시작 지점 왼쪽 코어 단말에서 F로 순서대로 열람하세요. 본 저장은 유지됩니다.");
        }
    }
}
#endif
