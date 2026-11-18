#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RewindNotary
{
    public static class StoryLinkVerification
    {
        [MenuItem("Rewind Notary/Story/Verify links in a temporary slot")]
        public static void Verify()
        {
            string directory = Path.Combine(Path.GetTempPath(), "rewind-story-" + Guid.NewGuid().ToString("N"));
            var host = new GameObject("Story link verification") { hideFlags = HideFlags.HideAndDontSave };
            var session = host.AddComponent<SkillSession>();
            try
            {
                var store = new SkillSaveStore(directory, SkillCatalog.Load());
                var record = store.Create();
                session.Initialize(store, record);
                CheckInvestigation(session);
                CheckSupport(session, store, directory);
                CheckEnding(session, store, directory);
                Debug.Log("Story links passed in an isolated slot.");
            }
            finally
            {
                session.Close(false);
                UnityEngine.Object.DestroyImmediate(host);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void Require(bool ok, string step)
        { if (!ok) throw new InvalidOperationException("Story link verification failed: " + step); }

        private static void Use(SkillSession session, string skill, string target)
        {
            var use = session.Progression.BeginUse(skill);
            Require(use != null, skill);
            session.Progression.Apply(use, new CombatEffectResult(EffectTargetKind.Enemy, target, true));
            session.Progression.EndUse(use);
        }
        private static void CheckInvestigation(SkillSession session)
        {
            var record = session.Progression.Record;
            var investigation = session.Investigation;
            var story = session.Story;
            investigation.TryCollect("route_ledger");
            investigation.TryCollect("route_waveform");
            investigation.TryCollect("contract_copy"); // A legacy prototype pickup.
            Require(!story.CompleteMartaAtticHandoff(), "prototype is not the attic");
            record.lastStageId = "hub";
            Require(story.CompleteMartaAtticHandoff(), "Marta");
            long revision = record.revision;
            Require(story.CompleteMartaAtticHandoff() && record.revision == revision
                && investigation.State.clueIds.FindAll(c => c == "contract_copy").Count == 1, "legacy copy replay");
            Require(!story.CompleteShelterHelp("mikhail") && !investigation.CanRevealClue("contract_original"), "early hub");
            // A real early prototype pickup must also stay hidden in the main-story log.
            record.lastStageId = "prototype";
            investigation.TryCollect("contract_original");
            record.lastStageId = "hub";
            Require(!investigation.HasClue("contract_original") && investigation.ClueCount("cassel_contract") == 1, "hidden legacy original");
            string[] districts = { "velvet", "clock", "opera", "bell", "gallery" };
            var trials = BenefactorTrialCatalog.Load().trials;
            for (int i = 0; i < trials.Length; i++)
            {
                var d = trials[i];
                record.lastStageId = districts[i];
                Require(story.CompleteLukaKeepsakeGuide(d.id) && investigation.HasClue("imprint_" + d.id), d.id);
                session.Progression.CompleteEncounter("duel:" + d.id, d.skillId);
                record.Get(d.skillId).validUseCount = 79; // Seed only the boundary; transfer via the actual use path.
                Use(session, d.skillId, "verification_opponent");
            }
            record.lastStageId = "hub";
            investigation.PrepareStoryComparison(true, "contract_comparison");
            Require(!investigation.HasClue("contract_original"), "C5 before I");
            Require(investigation.CompleteMiddleInvestigation(), "investigation decision");
            investigation.PrepareStoryComparison(false, "contract_comparison");
            Require(!investigation.HasClue("contract_original"), "pause log");
            investigation.PrepareStoryComparison(true, "contract_comparison");
            var comparison = investigation.Catalog.Stage("contract_comparison");
            Require(investigation.HasClue("contract_original") && !investigation.CanRead(comparison), "index still required");
            Confirm(session, "transfer_index");
            investigation.PrepareStoryComparison(true, comparison.id);
            Require(investigation.CanRead(comparison), "terminal");
            Require(investigation.State.clueIds.FindAll(c => c == "contract_original").Count == 1, "original granted once");
            Confirm(session, "contract_comparison");
            Confirm(session, "stored_records");
        }
        private static void Confirm(SkillSession session, string id)
        {
            var investigation = session.Investigation;
            var stage = investigation.Catalog.Stage(id);
            for (int i = 0; i < stage.entries.Length; i++)
                Require(investigation.TryConfirmEntry(id, i, true), stage.entries[i].id);
        }
        private static void CheckSupport(SkillSession session, SkillSaveStore store, string directory)
        {
            var record = session.Progression.Record;
            var trials = session.Progression.Trials;
            var d = BenefactorTrialCatalog.Load().ForSkill(SkillRules.SilverStitch);
            record.lastStageId = "velvet";
            trials.Configure("velvet");
            string failedTemp = Path.Combine(directory, "slot-1.json.tmp");
            Directory.CreateDirectory(failedTemp);
            Require(!session.Story.CompleteDuelHelp(d.id) && !BenefactorTrialProgress.Has(store.Load(), d, "charged"), "failed scene save");
            Directory.Delete(failedTemp);
            Require(session.Story.CompleteDuelHelp(d.id) && BenefactorTrialProgress.Has(store.Load(), d, "charged")
                && !BenefactorTrialProgress.HasTrialProof(record, d), "atomic retry");
            Require(trials.ReserveSupport(d), "spend");
            record.lastStageId = "hub";
            Require(session.Story.CompleteShelterHelp(d.id) && !BenefactorTrialProgress.Has(store.Load(), d, "charged"), "reunion balance");
            var old = BenefactorTrialCatalog.Load().ForStage("trial_julius");
            record.completedEncounters.Add("benefactor:v1:julius:unlocked");
            Require(session.Story.CompleteShelterHelp(old.id) && BenefactorTrialProgress.HasTrialProof(store.Load(), old)
                && !BenefactorTrialProgress.Has(record, old, "charged"), "legacy spent practice proof");
            trials.Configure("velvet");
            for (int i = 0; i < 11; i++) Use(session, d.skillId, "field_opponent");
            trials.Configure(d.stageId);
            for (int i = 0; i < d.sessionCap; i++) Use(session, SkillRules.BasicSword, d.opponentId);
            int before = record.Get(d.skillId).validUseCount;
            for (int i = 0; i < 3; i++) Use(session, d.skillId, d.opponentId + ":joint");
            Require(record.Get(d.skillId).validUseCount == before && BenefactorTrialProgress.HasTrialProof(record, d)
                && !BenefactorTrialProgress.Has(record, d, "charged"), "capped proof at eleven");
            trials.Configure("velvet");
            Use(session, d.skillId, "field_opponent");
            trials.Configure(d.stageId);
            for (int i = 1; i <= 3; i++)
            {
                Use(session, d.skillId, d.opponentId + ":joint");
                Require(BenefactorTrialProgress.Has(store.Load(), d, "charged") == (i == 3), "renewal " + i);
            }
        }
        private static void CheckEnding(SkillSession session, SkillSaveStore store, string directory)
        {
            var record = session.Progression.Record;
            record.storyFlags.Add(InvestigationService.EchoDefeatedFlag);
            record.storyFlags.Add(InvestigationService.OrderWithdrawnFlag);
            record.storyFlags.Add("cassel_open");
            record.lastStageId = "cassel";
            var finale = session.Finale;
            finale.BeginAttempt();
            finale.EnterMirrorPhase();
            foreach (string id in BenefactorIds.All) Require(finale.Reserve(id), "return " + id);
            int notifications = 0;
            finale.Settled += (a, e) => notifications++;
            string failedTemp = Path.Combine(directory, "slot-1.json.previous-a.tmp");
            Directory.CreateDirectory(failedTemp);
            Require(finale.Settle(false, true) && !session.LastSaveSucceeded && notifications == 0
                && store.Load().finalBattleAttempt.active, "failed checkpoint leaves committed fight");
            Directory.Delete(failedTemp);
            session.Save();
            Require(session.LastSaveSucceeded && notifications == 1 && store.Load().lastStageId == "court_lift", "settlement retry");
            session.Close(false);
            var trained = store.Load();
            var basic = trained.Get(SkillRules.BasicSword);
            basic.validUseCount = 400; trained.basicMasteryReached = true;
            trained.completedEncounters.Add("later_training");
            Require(store.Save(trained) && store.Save(trained), "rotate recovery backup");
            var resumed = store.RestorePrevious();
            Require(resumed.finalBattleAttempt.returnCount == 0 && resumed.finalBattleAttempt.remainingSkills.Count == 5
                && resumed.storyFlags.Contains("ending_basic") && StageCatalog.Find("cassel").Unlocked(resumed)
                && resumed.Get(SkillRules.BasicSword).validUseCount == 400 && resumed.completedEncounters.Contains("later_training")
                && FinaleEndingEvaluator.Evaluate(resumed.lastFinalBattleAttempt) == FinaleEnding.Basic, "resume on elevator");
            resumed.lastStageId = "cassel";
            var retry = new FinalBattleService(new SkillProgressionService(SkillCatalog.Load(), resumed), () => Require(store.Save(resumed), "save retry"));
            retry.BeginAttempt();
            Require(retry.State.active && retry.State.returnCount == 0 && Array.TrueForAll(BenefactorIds.All, retry.State.CanUse)
                && resumed.storyFlags.Contains("ending_basic"), "new attempt");
            store.Delete();
            Require(Directory.GetFiles(directory, "slot-1.json*").Length == 0, "delete previous records with slot");
        }
    }
}
#endif
