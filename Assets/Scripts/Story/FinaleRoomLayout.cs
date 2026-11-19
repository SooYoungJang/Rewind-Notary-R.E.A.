using UnityEngine;

namespace RewindNotary
{
    public sealed partial class RepresentativeArea
    {
        private JanhyangBoss courtEcho;
        private WaveTarget2D trainingTarget;
        private int trainingWave;
        private bool InEnding => Has("endingScene:Basic") || Has("endingScene:True");
        private StoryMark ForgeReturn => Room.id == "hub_forge" && (Has("court_lift_ready") || Has("ending_basic") || Has("ending_true"))
            ? Room.Point("forge_to_court_lift") : null;

        private bool TryForgeReturn()
        {
            var point = ForgeReturn;
            if (point == null || Distance(point.x) > 1.25f) return false;
            Checkpoint(script.Room("court_03_lift"), "court_lift_floor");
            Record.lastStageId = EndingCheckpoint.Lift;
            session.Save();
            return true;
        }

        private void PrepareCourtRoom()
        {
            trainingTarget = null;
            bool hall = Room.id == "court_01_hall", arena = Room.id == "court_02_echo_arena";
            if (!hall && !arena)
            {
                if (courtEcho != null) { Destroy(courtEcho); courtEcho = null; }
                return;
            }
            if (courtEcho == null)
            {
                // Subscribe in the hall so the encounter can imitate the approach wave.
                opponent.gameObject.SetActive(true);
                courtEcho = opponent.gameObject.AddComponent<JanhyangBoss>();
                courtEcho.Initialize(player);
            }
            courtEcho.ResetPattern();
            if (arena && !Has("janhyang_defeated"))
            {
                // A retry starts with 12 HP and replays the phase actions of this attempt.
                foreach (var row in script.rows)
                    if (row.sceneId == "echo_battle") Record.storyFlags.Remove(row.completionFlag);
            }
        }

        private bool CourtAction()
        {
            if (ActionToken == "clearDoor:court_02_echo_arena")
            {
                if (Safe && InspectPressed && Distance(Room.exits[1].x) <= 1.25f &&
                    roomTargets.TrueForAll(t => t.State.Cleared)) FinishAction();
                return true;
            }
            if (ActionToken.StartsWith("echoHP:", System.StringComparison.Ordinal))
            {
                int targetHP = int.Parse(ActionToken.Substring(7));
                if (!opponent.gameObject.activeSelf)
                {
                    opponent.gameObject.SetActive(true);
                    opponent.name = "rea_echo";
                    opponent.Health.Initialize(12, .35f);
                    opponent.ResetAt(LevelBuilder.BossStart);
                    courtEcho.Activate();
                }
                if (opponent.Body.position.y < -7 && !opponent.Health.Dead)
                    opponent.Body.position = LevelBuilder.BossStart;
                // An opening can span the shift. Only a counter in this phase unlocks its HP boundary.
                int requiredPhase = targetHP == 8 ? 1 : 2;
                opponent.Health.MinimumHealth = targetHP == 0 || courtEcho.LastCounterPhase == requiredPhase
                    ? targetHP : targetHP + 1;
                if (opponent.Health.Current <= targetHP && (targetHP != 0 || (Safe && OnFixedFloor())))
                {
                    FinishAction();
                    if (targetHP == 0) StopBout();
                }
                return true;
            }
            if (ActionToken == "train:resonant_crucible")
            {
                if (Record.Get(SkillRules.BasicSword).stage == 3)
                {
                    if (trainingTarget != null) trainingTarget.gameObject.SetActive(false);
                    FinishAction();
                }
                else if (trainingTarget == null || trainingTarget.State.Cleared)
                {
                    if (trainingTarget != null) Destroy(trainingTarget.gameObject);
                    trainingTarget = Place("crucible_wave_" + ++trainingWave, 18, .05f).AddComponent<WaveTarget2D>();
                    trainingTarget.Initialize(waves.Archetype("reflection_statue"), new WaveSpawn { segments = 3 }, player,
                        RoomArt.Enemy(Room.stage, "reflection_statue"));
                }
                return true;
            }
            return false;
        }
    }
}
