using UnityEngine;
using Input = RewindNotary.GameInputDevice;
using Unity.Cinemachine;

namespace RewindNotary
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        private CombatMotor2D player, boss;
        private PlayerController2D controller;
        private JanhyangBoss bossAI;
        private CasselBoss cassel;
        public CasselBoss ActiveCassel => initialized ? cassel : null;
        private CameraTarget2D cameraTarget;
        private CinemachineCamera virtualCamera;
        private SkillSession skills;
        public SkillSession ActiveSkills => initialized ? skills : null;
        internal Health ActivePlayerHealth => initialized ? player?.Health : null;
        internal RepresentativeArea Area { get; private set; }
        private GameShell shell;
        private GameObject world;
        private EncounterRewardBridge reward;
        private BenefactorEncounter encounter;
        private SkillPracticeArena2D practice;
        private WaveStageDirector waves;
        private BenefactorTrialOpponent trial;
        private BenefactorSupportController support;
        private StitchBinding bossBinding;
        private Font hudFont;
        private GameHudCanvas hudCanvas;
        private CinematicJuiceDirector cinematic;
        private DistrictMinimapCanvas minimap;
        private NotaryJournalCanvas journal;
        private readonly bool[] knownOwnedSkills = new bool[5];
        private bool skillOwnershipSeeded;
        private int lastContactSaves;
        private const float GameplayCameraSize = 6.1f;
        private float currentCameraSize = GameplayCameraSize;
        private bool checkpoint, won, initialized, echoDuel;
        private float deathTimer;
        private string failure;

        private void Start()
        {
            try
            {
                Application.targetFrameRate = 60;
                Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
                Physics2D.gravity = new Vector2(0f, -9.81f);
                Time.timeScale = 1f;
                Child("Audio").AddComponent<AudioManager>();
                hudFont = LocalizationFonts.LoadBodyFont(Loc.DefaultLanguage(Application.systemLanguage));
                shell = gameObject.AddComponent<GameShell>();
                shell.Initialize(this, hudFont);
                hudCanvas = gameObject.AddComponent<GameHudCanvas>();
                hudCanvas.Initialize(transform);
                cinematic = gameObject.AddComponent<CinematicJuiceDirector>();
                cinematic.Initialize(transform);
                minimap = gameObject.AddComponent<DistrictMinimapCanvas>();
                minimap.Initialize(transform);
                journal = gameObject.AddComponent<NotaryJournalCanvas>();
                journal.Initialize(transform);
                CombatFloatingText2D.Ensure(transform);
            }
            catch (System.Exception exception) { failure = exception.Message; Debug.LogException(exception); }
        }
        public void StartStage(SkillSaveStore store, CoreRecord record)
        {
            if (world != null) throw new System.InvalidOperationException(Loc.Get("stage.already_running"));
            double loadStarted = AutoTestRuntime.LoadStart();
            world = new GameObject("Playable stage"); world.transform.SetParent(transform, false);
            SkillStroke.WorldRoot = world.transform;
            checkpoint = won = false; deathTimer = 0;
            skillOwnershipSeeded = false;
            Build(store, record); initialized = true;
            if (GameShell.DebugAvailable && cassel == null) ApplyStageOverride(shell.Settings.Value.stageOverride);
            AutoTestRuntime.LoadEnd("stage", record.lastStageId, loadStarted);
        }
        private void Build(SkillSaveStore store, CoreRecord record)
        {
            echoDuel = record.lastStageId == "echo_duel";
            bool finale = record.lastStageId == "cassel";
            if (finale && !record.testData && !StageCatalog.Find("cassel").Unlocked(record))
                throw new System.InvalidOperationException(Loc.Get("finale.entry_locked"));
            skills = Child("Skill session").AddComponent<SkillSession>();
            encounter = BenefactorEncounter.ForStage(record.lastStageId);
            var trialDefinition = BenefactorTrialCatalog.Load().ForStage(record.lastStageId);
            skills.EncounterId = trialDefinition != null ? trialDefinition.stageId
                : record.lastStageId == "skill_lab" ? "practice_skill_lab" : encounter.EncounterId;
            if (echoDuel) skills.EncounterId = record.lastStageId;
            if (finale) skills.EncounterId = "cassel_finale";
            skills.Initialize(store, record);
            skills.Progression.Trials.Configure(record.lastStageId);
            skills.PresentationRequested += PresentSkill;
            Camera camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera"); go.tag = "MainCamera";
                camera = go.AddComponent<Camera>(); go.AddComponent<AudioListener>();
            }
            camera.orthographic = true; camera.orthographicSize = GameplayCameraSize;
            camera.nearClipPlane = 0.1f; camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.53f, 0.70f, 0.85f);
            camera.transform.SetPositionAndRotation(new Vector3(0, 2.7f, -10), Quaternion.identity);
            bool story = RepresentativeScript.Handles(record.lastStageId);
            bool waveTest = record.lastStageId == "wave_test";
            if (waveTest)
            {
                waves = Child("Zone waves").AddComponent<WaveStageDirector>();
                waves.BuildWorld();
            }
            else if (finale) Child("심판석").AddComponent<CasselArena>().Build(skills);
            else if (!story) Child("Level").AddComponent<LevelBuilder>().Build();
            player = CreateActor("Rea", LevelBuilder.StartPoint, "rea-sheet", 6, 0.75f, true);
            player.Skills = skills;
            controller = player.gameObject.AddComponent<PlayerController2D>();
            boss = CreateActor("Janhyang - yesterday's Rea", LevelBuilder.BossStart, "janhyang-sheet", 12, 0.35f);
            boss.Temporal = boss.gameObject.AddComponent<TemporalActor2D>();
            boss.gameObject.AddComponent<ResonanceTarget2D>();
            boss.Face(-1);
            player.SetOpponent(boss.Health); boss.SetOpponent(player.Health);
            Physics2D.IgnoreCollision(player.GetComponent<Collider2D>(), boss.GetComponent<Collider2D>());
            bossBinding = boss.gameObject.AddComponent<StitchBinding>();
            reward = boss.gameObject.AddComponent<EncounterRewardBridge>();
            if (finale)
            {
                boss.name = "Cassel";
                boss.Animation.Tint(new Color(0.65f, 0.8f, 1f));
            }
            else if (story) boss.name = "Mikhail";
            else if (trialDefinition == null)
            {
                bossAI = boss.gameObject.AddComponent<JanhyangBoss>(); bossAI.Initialize(player);
                reward.EncounterId = encounter.EncounterId; reward.RewardSkillId = encounter.SkillId;
                if (!echoDuel) reward.Initialize(boss.Health, skills.Progression);
            }
            else
            {
                boss.name = trialDefinition.opponentId;
                trial = boss.gameObject.AddComponent<BenefactorTrialOpponent>();
                trial.Initialize(player, skills, trialDefinition);
                player.ResetAt(new Vector2(trialDefinition.playerX, 0.15f));
                if (trialDefinition.condition == "stitch")
                {
                    var anchor = Child("trial_anchor:" + trialDefinition.id);
                    anchor.transform.position = new Vector3(trialDefinition.playerX + 1, 0.18f, 0);
                    anchor.AddComponent<StitchSurface2D>().Initialize(anchor.name, 0.9f, true, player, boss);
                }
            }
            controller.Silver = player.gameObject.AddComponent<SilverStitchSkill>();
            controller.Silver.Initialize(skills, boss);
            player.BenefactorSkills = player.gameObject.AddComponent<BenefactorSkillRunner>();
            player.BenefactorSkills.Initialize(skills, boss, controller.Silver, shell.Settings.Value);
            if (!finale)
            {
                support = player.gameObject.AddComponent<BenefactorSupportController>();
                support.Initialize(player, boss, skills);
            }
            else
            {
                cassel = boss.gameObject.AddComponent<CasselBoss>(); cassel.Initialize(player, skills);
                for (int i = 0; i < 3; i++)
                {
                    var anchor = Child("cassel_anchor:" + i); anchor.transform.position = new Vector3(42 + i * 5, .18f);
                    anchor.AddComponent<StitchSurface2D>().Initialize(anchor.name, 2.5f, false, player, boss);
                }
            }
            if (!story && !finale && !waveTest && trialDefinition == null && !echoDuel)
            {
                SkillPracticeWorld.Build(Child("Skill practice").transform, player, boss);
                if (record.lastStageId != "prototype")
                {
                    practice = Child("Remaining skill practice").AddComponent<SkillPracticeArena2D>();
                    practice.Initialize(player, boss);
                }
            }
            if (echoDuel)
            {
                checkpoint = true;
                player.ResetAt(LevelBuilder.ArenaStart);
                bossAI.Activate();
            }
            if (record.lastStageId == "skill_lab")
            {
                skills.Progression.GrantPracticeSkills();
                player.ResetAt(new Vector2(SkillGeometry.PracticeStart, 0));
            }
            if (waveTest)
            {
                skills.Progression.GrantPracticeSkills();
                // Keep executor references valid, but the dormant duel cannot receive hidden hits/rewards.
                boss.Health.Initialize(0, 0f);
                boss.gameObject.SetActive(false);
                waves.Initialize(player, skills, hudFont);
            }

            if (story)
            {
                Area = Child("Representative area").AddComponent<RepresentativeArea>();
                Area.Initialize(skills, shell, player, boss);
            }
            cameraTarget = Child("Camera target").AddComponent<CameraTarget2D>();
            cameraTarget.Area = Area;
            cameraTarget.Player = player.transform; cameraTarget.Snap();
            if (camera.GetComponent<CinemachineBrain>() == null) camera.gameObject.AddComponent<CinemachineBrain>();
            virtualCamera = Child("Cinemachine follow camera").AddComponent<CinemachineCamera>();
            virtualCamera.Follow = cameraTarget.transform;
            var lens = LensSettings.Default;
            lens.OrthographicSize = GameplayCameraSize; lens.NearClipPlane = 0.1f; lens.FarClipPlane = 100f;
            virtualCamera.Lens = lens;
            var composer = virtualCamera.gameObject.AddComponent<CinemachinePositionComposer>();
            composer.CameraDistance = 10f;
            composer.TargetOffset = new Vector3(0f, 1.2f, 0f);
            composer.Damping = new Vector3(0.18f, 0.35f, 0f);
            var shake = virtualCamera.gameObject.AddComponent<ImpactCameraShake>();
            Child("Impact feedback").AddComponent<ImpactFeedback>().Initialize(shake);
            Child("Skill evolution").AddComponent<SkillEvolutionPresentation>().Initialize(skills, player.transform, world.transform, hudFont);
            Child("Parallax").AddComponent<ParallaxBackground>().Initialize(camera, Area);
            if (camera.GetComponent<AtmospherePostEffect>() == null) camera.gameObject.AddComponent<AtmospherePostEffect>();
            Child("Atmosphere").AddComponent<DistrictAtmosphere2D>().Initialize(camera, Area, finale ? "cassel" : null);
            Child("Room architecture").AddComponent<RoomArchitectureDecorator>().Initialize(camera, Area, world.transform, finale ? "cassel" : null);
            Child("Stage2 visual director").AddComponent<Stage2VisualDirector>().Initialize(player);
            if (record.lastStageId == "prototype")
                Child("Investigation points").AddComponent<InvestigationWorld>().Initialize(player, skills, shell, hudFont);
            skills.PresentRestoredState();
        }
        private GameObject Child(string label)
        {
            var go = new GameObject(label); go.transform.SetParent(world != null ? world.transform : transform, false); return go;
        }
        private CombatMotor2D CreateActor(string label, Vector2 position, string sheet, int hp, float immunity, bool isPlayer = false)
        {
            var go = Child(label); go.transform.position = position;
            var motor = go.AddComponent<CombatMotor2D>();
            motor.Initialize("GameArt/sprites/" + sheet, hp, immunity, 1, Color.white, isPlayer);
            go.AddComponent<ActorVisualPolish2D>().Initialize(motor, isPlayer);
            if (isPlayer)
            {
                var growth = go.AddComponent<ReaGrowthVisual2D>();
                growth.Initialize(motor);
                go.AddComponent<ReaMotionPolish2D>().Initialize(motor);
            }
            else
            {
                go.AddComponent<BossVisualIdentity2D>().Initialize(motor);
            }
            return motor;
        }
        private void Update()
        {
            if (!initialized || !shell.Playing || !GameInput.CanPlay) return;
            if (skills.Progression.Record.lastStageId == EndingCheckpoint.Lift) return;
            if (cassel != null && skills.Progression.Record.lastStageId != "cassel") return;
            if (Area != null && skills.Progression.Record.lastStageId == "cassel") return;
            if ((Area != null || cassel != null) && !skills.LastSaveSucceeded) return;
            if (!echoDuel && (Debug.isDebugBuild || Application.isEditor) && Input.GetKeyDown(KeyCode.F8))
            { shell.EnterCasselPractice(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)); return; }
            if (Debug.isDebugBuild || Application.isEditor) UpdateSkillDebug();
            if (echoDuel && won && GameInput.InvestigateDown)
            { shell.FinishEchoDuel(); return; }
            if (GameInput.RetryDown) { ResetRun(true); return; }
            if (player.Health.Dead)
            {
                deathTimer += Time.deltaTime;
                if (deathTimer >= 1.1f) ResetRun(false);
                return;
            }
            if (player.transform.position.y < (Area?.Room.FallY ?? -7f))
            {
                ResetRun(false);
                AudioManager.Play(AudioManager.Sfx.PlayerDown);
                return;
            }
            if (Area != null || cassel != null || waves != null || trial != null) return;
            if (!checkpoint && player.transform.position.x >= 35f)
            { checkpoint = true; bossAI.Activate(); }
            if (!boss.Health.Dead && boss.transform.position.y < -7f)
            { bossBinding.Break(); boss.ResetAt(LevelBuilder.BossStart); bossAI.ResetPattern(); bossAI.Activate(); }
            if (boss.Health.Dead && !won) won = true;
        }
        private void ResetRun(bool fromStart)
        {
            // A retry in the lethal-hit frame must deliver the queued reward before invalidating uses.
            Area?.FlushVictory();
            reward.Flush();
            if (cassel != null) skills.Finale.Defeat();
            skills.Progression.ResetAttempt();
            skills.Save();
            SkillPracticeWorld.ResetAttempt();
            SkillProjectile2D.ClearAll(); practice?.ResetAttempt();
            foreach (var target in ResonanceTarget2D.All) target.ResetAttempt();
            foreach (var lamp in PracticeLamp2D.All) lamp.ResetAttempt();
            foreach (var hazard in PracticeTemporalHazard2D.All) hazard.ResetAttempt();
            bossBinding.Break();
            if (Area != null) { Area.Restore(); deathTimer = 0; cameraTarget.Snap(); return; }
            if (fromStart) checkpoint = echoDuel;
            waves?.ResetAttempt(fromStart);
            ImpactFeedback.ResetEffects();
            AudioManager.ResetPlayback(checkpoint);
            won = false; deathTimer = 0;
            player.ResetAt(checkpoint ? LevelBuilder.ArenaStart : LevelBuilder.StartPoint);
            if (waves != null) player.ResetAt(waves.Respawn);
            controller.ClearInput();
            boss.ResetAt(LevelBuilder.BossStart); boss.Face(-1);
            if (trial != null)
            { trial.ResetAttempt(); player.ResetAt(new Vector2(trial.Definition.playerX, 0.15f)); }
            else if (cassel != null) cassel.ResetBattle();
            else { bossAI.ResetPattern(); if (checkpoint) bossAI.Activate(); }
            cameraTarget.Snap();
            virtualCamera.PreviousStateIsValid = false;
        }
        private void UpdateSkillDebug()
        {
            if (echoDuel || Area != null || cassel != null) return;
            if (Input.GetKeyDown(KeyCode.F7)) EnterJanhyangPractice();
            int preview = Input.GetKeyDown(KeyCode.F1) ? 1 : Input.GetKeyDown(KeyCode.F2) ? 2
                : Input.GetKeyDown(KeyCode.F3) ? 3 : Input.GetKeyDown(KeyCode.F4) ? 0 : -1;
            if (preview >= 0)
            {
                shell.SetStageOverride(preview);
            }
            if (Input.GetKeyDown(KeyCode.F5) || Input.GetKeyDown(KeyCode.F6))
            {
                ResetRun(false);
                shell.SetStageOverride(0);
                skills.Progression.SeedBoundary(Input.GetKeyDown(KeyCode.F6));
            }
        }
        public void EnterJanhyangPractice()
        {
            if (!initialized || echoDuel || Area != null || !GameShell.DebugAvailable || cassel != null || waves != null || trial != null) return;
            // Existing prototype / skill-lab route; never unlock a story flag or overwrite the main slot.
            skills.Progression.GrantPracticeSkills();
            checkpoint = true;
            ResetRun(false);
        }
        public void ApplyStageOverride(int stage)
        {
            if (!initialized || echoDuel || Area != null || cassel != null || !GameShell.DebugAvailable || skills.PreviewStage == stage) return;
            ResetRun(false);
            skills.SetPreview(stage);
        }
        public void SuspendInput() { if (controller != null) controller.SuspendInput(); }
        public void StopStage()
        {
            // Caller has saved successfully; close the session before deferred destruction.
            if (skills != null)
            {
                if (trial != null) skills.Progression.Trials.RequestText(trial.Definition.dialogue.returnKey);
                skills.PresentationRequested -= PresentSkill; skills.Close(false);
            }
            initialized = false; SkillStroke.WorldRoot = null;
            lastContactSaves = 0;
            currentCameraSize = GameplayCameraSize;
            cinematic?.ResetState();
            minimap?.CloseFullMap();
            ImpactFeedback.ResetEffects(); AudioManager.ResetPlayback(false);
            if (world != null) { world.SetActive(false); Destroy(world); world = null; }
            player = boss = null; controller = null; skills = null; reward = null; practice = null;
            Area = null; waves = null; trial = null; support = null; bossAI = null; cassel = null;
        }
        public void AbortStage()
        {
            if (skills != null) skills.Save();
            StopStage();
        }
        private void PresentSkill(string cue, string skillId)
        {
            // Boundary feedback is owned by SkillEvolutionPresentation; keep narrative notices separate.
            if (cue == "stage_up" || cue == "basic_mastery" || cue == "doom_transfer") return;
            if (player == null) return;
            Color color = cue == "doom_transfer" || cue == "doom_restored" ? new Color(1f, 0.3f, 0.4f) : Color.cyan;
            SkillStroke.Show(player.transform.position + Vector3.up * 0.3f,
                player.transform.position + Vector3.up * 2.5f, color, 0.8f);
        }
        private static string SkillInputLabel(string id)
        {
            switch (id)
            {
                case BenefactorIds.Waltz: return Loc.Get("input.direction_up");
                case BenefactorIds.Forge: return Loc.Get("input.direction_down_air");
                case BenefactorIds.Canvas: return Loc.Get("input.direction_down_ground");
                case BenefactorIds.Pendulum: return Loc.Get("input.direction_back");
                default: return Loc.Get("input.direction_neutral");
            }
        }
        private string SkillLabel(string id)
        {
            var progress = skills.Progression.Record.Get(id);
            string name = skills.Progression.Catalog.Get(id).DisplayName;
            return Loc.Get("hud.skill_progress", name, progress.validUseCount, progress.stage, (progress.acquired ? "" : Loc.Get("skill.unowned_suffix")), (cassel != null && !skills.Finale.State.CanUse(id) ? Loc.Get("skill.return_suffix") : ""));
        }
        private void LateUpdate()
        {
            if (virtualCamera != null && shell != null && initialized)
            {
                float targetSize = shell.Page == ShellPage.StoryDialogue ? 4.85f : GameplayCameraSize;
                currentCameraSize = Mathf.Lerp(currentCameraSize, targetSize, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 5.5f));
                var lens = virtualCamera.Lens;
                if (Mathf.Abs(lens.OrthographicSize - currentCameraSize) > 0.002f)
                {
                    lens.OrthographicSize = currentCameraSize;
                    virtualCamera.Lens = lens;
                }
            }
            if (hudCanvas == null) return;
            if (!initialized || shell == null || !shell.Playing || failure != null)
            {
                hudCanvas.Render(default);
                cinematic?.Observe(false, false, null, null, null);
                minimap?.Observe(false, null, false);
                journal?.Observe(false, null, null, 0, false);
                return;
            }
            CheckNewlyAcquiredSkills();
            if (Area != null && Area.ContactSaveCount > lastContactSaves)
            {
                lastContactSaves = Area.ContactSaveCount;
                player.Health.Heal(player.Health.Maximum);
                UiAudioSynth.PlayCheckpoint();
                ActorVisualPolish2D.SpawnSkillFx(SkillRules.BasicSword, player.transform.position + Vector3.up, 1, 2.8f, 0.45f);
                hudCanvas.ShowToast(Loc.Get("hud.checkpoint_saved"));
            }
            var snap = new GameHudCanvas.HudSnapshot
            {
                Visible = true,
                LocationTitle = Loc.Get("game.title"),
                PlayerHp = player.Health.Current,
                PlayerMaxHp = player.Health.Maximum,
                ControlsText = GameInput.FormatControlsPrompt(true),
                Skills = BuildSkillBadges(),
                DashReadyRatio = player.Defense != null
                    ? 1f - Mathf.Clamp01((player.Defense.State.DashReadyAt - Time.time) / Mathf.Max(0.05f, DefenseTuning.DashCooldown))
                    : 1f,
                AirDashReady = player.Defense == null || !player.Defense.State.AirDashUsed
            };
            if (Area != null)
            {
                Area.FillHudSnapshot(ref snap);
                hudCanvas.Render(snap);
                cinematic?.Observe(true, snap.BossVisible, snap.BossName, snap.BossPhase, boss);
                minimap?.Observe(true, Area, snap.BossVisible);
                journal?.Observe(true, skills, Area, minimap != null ? minimap.VisitedCount : 0, minimap != null && minimap.FullMapOpen);
                return;
            }
            if (echoDuel)
            {
                snap.LocationTitle = encounter.Label;
                snap.BossVisible = true;
                snap.BossName = encounter.Label;
                snap.BossPhase = Loc.Get("hud.echo_phase", bossAI.Phase, bossAI.PatternLabel);
                snap.BossHp = boss.Health.Current;
                snap.BossMaxHp = boss.Health.Maximum;
                snap.ObjectiveHint = Loc.Get("input.retry_pause", encounter.Label);
                snap.OverlayVisible = won || player.Health.Dead;
                snap.OverlayTitle = won ? Loc.Get("finale.result", encounter.Label) : Loc.Get("hud.retrying");
                snap.OverlayDetail = won ? Loc.Get("area.travel", Loc.Get("ui.stage_select")) : Loc.Get("hud.returning_checkpoint");
                hudCanvas.Render(snap);
                cinematic?.Observe(true, snap.BossVisible, snap.BossName, snap.BossPhase, boss);
                minimap?.Observe(false, null, true);
                journal?.Observe(true, skills, null, minimap != null ? minimap.VisitedCount : 0, false);
                return;
            }
            var defense = player.Defense.State;
            string defenseStatus = defense.Phase == DefensePhase.Dash ? Loc.Get("defense.dashing")
                : Loc.Get("defense.ready");
            snap.StatusLine = SkillLabel(SkillRules.BasicSword);
            snap.SubStatusLine = defenseStatus + "  ·  " + Loc.Get("hud.dash_cooldown",
                Mathf.Max(0f, defense.DashReadyAt - Time.time).ToString("0.0", Loc.Culture),
                defense.AirDashUsed ? Loc.Get("defense.air_spent") : Loc.Get("defense.air_available"));
            snap.NoticeText = !string.IsNullOrEmpty(skills.StorageError) ? skills.StorageError : skills.Notice;
            if (trial != null)
            {
                snap.BossVisible = true;
                snap.BossName = trial.Definition.DisplayName;
                snap.BossPhase = trial.PatternLabel;
                snap.BossHp = boss.Health.Current;
                snap.BossMaxHp = boss.Health.Maximum;
                snap.ObjectiveHint = trial.Definition.ObjectiveLabel + "\n" + skills.Progression.Trials.SessionStatus;
                string key = skills.Progression.Trials.LastTextKey;
                if (!string.IsNullOrEmpty(key)) snap.NoticeText = Loc.Get(key);
            }
            else if (cassel != null)
            {
                snap.LocationTitle = Loc.Get("stage.cassel.name");
                snap.BossVisible = true;
                snap.BossName = Loc.Get("area.speaker.cassel");
                snap.BossHp = boss.Health.Current;
                snap.BossMaxHp = boss.Health.Maximum;
                snap.ObjectiveHint = cassel.Status;
            }
            else if (bossAI != null && bossAI.Active)
            {
                snap.BossVisible = true;
                snap.BossName = encounter.Label;
                snap.BossPhase = Loc.Get("hud.echo_phase", bossAI.Phase, bossAI.PatternLabel);
                snap.BossHp = boss.Health.Current;
                snap.BossMaxHp = boss.Health.Maximum;
            }
            else
            {
                snap.ObjectiveHint = Loc.Get("input.retry_pause", waves != null ? Loc.Get("hud.wave_hint") : checkpoint ? Loc.Get("hud.checkpoint_hint") : Loc.Get("hud.reach_arena"));
            }
            if (player.Health.Dead || won)
            {
                snap.OverlayVisible = true;
                snap.OverlayTitle = won ? Loc.Get("hud.practice_complete") : Loc.Get("hud.retrying");
                snap.OverlayDetail = won ? Loc.Get("input.retry_button") : Loc.Get("hud.returning_checkpoint");
            }
            hudCanvas.Render(snap);
            cinematic?.Observe(true, snap.BossVisible, snap.BossName, snap.BossPhase, boss);
            minimap?.Observe(false, null, snap.BossVisible);
            journal?.Observe(true, skills, Area, minimap != null ? minimap.VisitedCount : 0, false);
        }
        public void OpenJournal()
        {
            if (journal == null || skills == null) return;
            minimap?.CloseFullMap();
            journal.Open(skills, Area, minimap != null ? minimap.VisitedCount : 0);
        }
        private void CheckNewlyAcquiredSkills()
        {
            if (skills?.Progression?.Record == null || hudCanvas == null) return;
            var rec = skills.Progression.Record;
            var cat = skills.Progression.Catalog;
            Color[] accents =
            {
                new Color(0.88f, 0.34f, 0.44f),
                new Color(0.96f, 0.78f, 0.32f),
                new Color(0.95f, 0.48f, 0.68f),
                new Color(0.32f, 0.84f, 0.92f),
                new Color(0.54f, 0.78f, 1.00f)
            };
            if (!skillOwnershipSeeded)
            {
                for (int i = 0; i < BenefactorIds.All.Length && i < knownOwnedSkills.Length; i++)
                    knownOwnedSkills[i] = rec.Get(BenefactorIds.All[i])?.acquired == true;
                skillOwnershipSeeded = true;
                return;
            }
            for (int i = 0; i < BenefactorIds.All.Length && i < knownOwnedSkills.Length; i++)
            {
                string id = BenefactorIds.All[i];
                bool owned = rec.Get(id)?.acquired == true;
                if (owned && !knownOwnedSkills[i])
                {
                    knownOwnedSkills[i] = true;
                    string name = cat.Get(id).DisplayName;
                    string formula = NotaryJournalCanvas.FormatSkillFormula(id);
                    string tip = Loc.Get("skillcard.tip_" + id);
                    hudCanvas.ShowSkillCard(name, formula, tip, accents[i]);
                    UiAudioSynth.PlayCheckpoint();
                }
            }
        }
        private GameHudCanvas.SkillBadge[] BuildSkillBadges()
        {
            if (skills?.Progression?.Record == null) return null;
            var rec = skills.Progression.Record;
            var cat = skills.Progression.Catalog;
            string[] cues = { "·", "◀", "▲", "▽", "▼" };
            Color[] accents =
            {
                new Color(0.88f, 0.34f, 0.44f),
                new Color(0.96f, 0.78f, 0.32f),
                new Color(0.95f, 0.48f, 0.68f),
                new Color(0.32f, 0.84f, 0.92f),
                new Color(0.54f, 0.78f, 1.00f)
            };
            var list = new GameHudCanvas.SkillBadge[BenefactorIds.All.Length];
            for (int i = 0; i < BenefactorIds.All.Length; i++)
            {
                string id = BenefactorIds.All[i];
                var p = rec.Get(id);
                string dn = cat.Get(id).DisplayName;
                string shortName = string.IsNullOrEmpty(dn) ? id.Substring(0, 2) : (dn.Length > 2 ? dn.Substring(0, 2) : dn);
                list[i] = new GameHudCanvas.SkillBadge
                {
                    ShortLabel = shortName,
                    DirCue = cues[i],
                    Owned = p != null && p.acquired,
                    Stage = p != null ? p.stage : 1,
                    Accent = accents[i]
                };
            }
            return list;
        }
        private void OnGUI()
        {
            if (failure == null && (!initialized || shell == null || !shell.Playing)) return;
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2f,
                (Screen.height - 720 * scale) / 2f, 0), Quaternion.identity, Vector3.one * scale);
            var title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            var label = new GUIStyle(GUI.skin.label) { fontSize = 17 };
            if (hudFont != null) { title.font = hudFont; label.font = hudFont; }
            if (failure != null)
            {
                GUI.Box(new Rect(220, 210, 840, 220), "");
                GUI.Label(new Rect(245, 235, 800, 40), Loc.Get("boot.failure"), title);
                GUI.Label(new Rect(245, 285, 780, 130), failure, new GUIStyle(label) { wordWrap = true });
                GUI.matrix = previous; return;
            }
            if (!initialized || !shell.Playing) { GUI.matrix = previous; return; }
            if (hudCanvas != null)
            {
                if (Area?.InAtticTutorial != true && minimap?.FullMapOpen != true && !NotaryJournalCanvas.IsOpen)
                    DrawQuickSkills(label, 272);
                GUI.matrix = previous;
                return;
            }
            if (Area != null)
            {
                Area.DrawHUD(title, label);
                if (!Area.InAtticTutorial) DrawQuickSkills(label, 18);
                GUI.matrix = previous;
                return;
            }
            DrawQuickSkills(label, 576);
            GUI.Box(new Rect(20, 18, 400, 128), "");
            GUI.Label(new Rect(36, 28, 380, 34), Loc.Get("game.title"), title);
            GUI.Label(new Rect(36, 64, 370, 25), Loc.Get("hud.rea_health", player.Health.Current, player.Health.Maximum), label);
            Bar(new Rect(36, 99, 366, 15), player.Health, new Color(0.18f, 0.82f, 0.73f));
            if (echoDuel)
            {
                GUI.Box(new Rect(455, 18, 800, 128), "");
                GUI.Label(new Rect(475, 27, 760, 32), encounter.Label + "   " + boss.Health.Current + " / " + boss.Health.Maximum, title);
                GUI.Label(new Rect(475, 66, 760, 29), Loc.Get("hud.echo_phase", bossAI.Phase, bossAI.PatternLabel), label);
                Bar(new Rect(475, 106, 760, 14), boss.Health, new Color(.47f, .65f, .92f));
                GUI.Label(new Rect(30, 630, 1220, 36), Loc.Get("input.gameplay"), label);
                GUI.Label(new Rect(30, 673, 1220, 32), Loc.Get("input.retry_pause", encounter.Label), label);
                if (won || player.Health.Dead)
                {
                    GUI.Box(new Rect(390, 265, 500, 120), "");
                    GUI.Label(new Rect(412, 277, 455, 42), won ? Loc.Get("finale.result", encounter.Label) : Loc.Get("hud.retrying"), title);
                    GUI.Label(new Rect(412, 333, 455, 42), won ? Loc.Get("area.travel", Loc.Get("ui.stage_select")) : Loc.Get("hud.returning_checkpoint"), label);
                }
                GUI.matrix = previous; return;
            }
            GUI.Box(new Rect(20, 154, 440, 366), "");
            GUI.Label(new Rect(36, 164, 420, 28), SkillLabel(SkillRules.BasicSword), label);
            var mapping = new GUIStyle(label) { fontSize = 14, wordWrap = true };
            for (int i = 0; i < BenefactorIds.All.Length; i++)
                GUI.Label(new Rect(36, 194 + i * 44, 408, 44),
                    Loc.Get("input.skill_mapping_row", SkillInputLabel(BenefactorIds.All[i]), SkillLabel(BenefactorIds.All[i])), mapping);
            GUI.Label(new Rect(36, 416, 408, 54), Loc.Get("input.cast_hint"), mapping);
            string mode = skills.Progression.Record.testData ? Loc.Get("hud.test_save") : Loc.Get("hud.save_slot", skills.ActiveSlot);
            if (skills.PreviewStage != 0) mode += Loc.Get("hud.preview_suffix", skills.PreviewStage);
            GUI.Label(new Rect(36, 474, 408, 44), mode, mapping);
            var defense = player.Defense.State;
            string defenseStatus = defense.Phase == DefensePhase.Dash ? Loc.Get("defense.dashing")
                : Loc.Get("defense.ready");
            GUI.Label(new Rect(36, 524, 370, 26), defenseStatus, mapping);
            GUI.Label(new Rect(36, 552, 370, 44), Loc.Get("hud.dash_cooldown", Mathf.Max(0f, defense.DashReadyAt - Time.time).ToString("0.0", Loc.Culture), (defense.AirDashUsed ? Loc.Get("defense.air_spent") : Loc.Get("defense.air_available"))), mapping);
            string line = skills.Notice;
            if (!string.IsNullOrEmpty(line))
            {
                var subtitle = new GUIStyle(label) { wordWrap = true };
                float height = Mathf.Max(78f, subtitle.CalcHeight(new GUIContent(line), 750));
                GUI.Box(new Rect(470, 30, 785, height + 26), "");
                GUI.Label(new Rect(486, 43, 750, height), line, subtitle);
            }
            if (!string.IsNullOrEmpty(skills.StorageError))
                GUI.Label(new Rect(470, 424, 760, 65), skills.StorageError, new GUIStyle(label) { wordWrap = true });
            GUI.Label(new Rect(28, 640, 1220, 30), Loc.Get("input.gameplay"), mapping);
            GUI.Label(new Rect(28, 673, 1220, 28), Loc.Get("input.retry_pause", (trial != null ? Loc.Get("hud.trial_hint") : waves != null ? Loc.Get("hud.wave_hint") : checkpoint ? Loc.Get("hud.checkpoint_hint") : Loc.Get("hud.reach_arena"))), label);
            if (Debug.isDebugBuild || Application.isEditor)
                GUI.Label(new Rect(28, 606, 1220, 28), Loc.Get("input.debug"), mapping);
            if (trial != null)
            {
                string key = skills.Progression.Trials.LastTextKey;
                if (!string.IsNullOrEmpty(key))
                {
                    GUI.Box(new Rect(475, 150, 780, 118), "");
                    GUI.Label(new Rect(490, 159, 748, 102), Loc.Get(key), new GUIStyle(label) { wordWrap = true });
                }
                GUI.Box(new Rect(475, 436, 780, 166), "");
                GUI.Label(new Rect(490, 446, 750, 30), trial.Definition.DisplayName + " · " + trial.PatternLabel, label);
                GUI.Label(new Rect(490, 480, 750, 32), trial.Definition.ObjectiveLabel, label);
                GUI.Label(new Rect(490, 516, 750, 40), skills.Progression.Trials.SessionStatus, new GUIStyle(label) { wordWrap = true });
                GUI.Label(new Rect(490, 562, 750, 30), Loc.Get("trial.health", boss.Health.Current, boss.Health.Maximum), label);
            }
            if (support != null && waves == null)
            {
                var directedTrial = BenefactorTrialCatalog.Load().ForSkill(player.BenefactorSkills.InputSnapshot.SupportSkillId);
                GUI.Label(new Rect(475, 326, 780, 56), Loc.Get("input.benefactor_support", BenefactorTrialProgress.Status(skills.Progression.Record, directedTrial)), mapping);
                GUI.Label(new Rect(475, 386, 780, 32), support.Notice ?? "", label);
            }
            if (cassel != null)
            {
                GUI.Box(new Rect(470, 448, 785, 150), "");
                GUI.Label(new Rect(486, 460, 750, 130), cassel.Status, new GUIStyle(label) { wordWrap = true });
            }
            if (bossAI != null && bossAI.Active)
            {
                GUI.Box(new Rect(475, 510, 470, 76), "");
                GUI.Label(new Rect(495, 519, 440, 26), encounter.Label + "     " + boss.Health.Current + " / 12", label);
                Bar(new Rect(495, 554, 430, 12), boss.Health, new Color(0.47f, 0.65f, 0.92f));
                GUI.Label(new Rect(480, 454, 775, 50), Loc.Get("hud.echo_phase", bossAI.Phase, bossAI.PatternLabel),
                    new GUIStyle(label) { wordWrap = true });
            }
            if (player.Health.Dead || won)
            {
                GUI.Box(new Rect(485, 270, 500, 130), "");
                GUI.Label(new Rect(515, 290, 460, 42), won ? Loc.Get("hud.practice_complete") : Loc.Get("hud.retrying"), title);
                GUI.Label(new Rect(515, 342, 460, 35), won ? Loc.Get("input.retry_button") : Loc.Get("hud.returning_checkpoint"), label);
            }
            GUI.matrix = previous;
        }
        private void DrawQuickSkills(GUIStyle label, float y)
        {
            var keyStyle = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            var nameStyle = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 13, wordWrap = true };
            var commandStyle = new GUIStyle(nameStyle) { fontSize = 11 };
            var previous = GUI.color;
            var record = skills.Progression.Record;
            for (int i = 0; i < QuickSkills.Keys.Length; i++)
            {
                string id = QuickSkills.Equipped(shell.Settings.Value, record, i);
                float x = 842 + i * 104;
                GUI.color = new Color(.05f, .075f, .11f, .94f);
                string command = QuickSkills.Command(QuickSkills.StarterIndex(id));
                GUI.DrawTexture(new Rect(x, y, 98, command.Length > 0 ? 68 : 56), Texture2D.whiteTexture);
                GUI.color = new Color(.62f, 1f, .93f);
                var key = QuickSkills.Keys[i].ToString();
                GUI.Label(new Rect(x + 2, y + 1, 94, 23), key, keyStyle);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + 2, y + 22, 94, 32), QuickSkills.Name(skills.Progression.Catalog, id), nameStyle);
                if (command.Length > 0)
                {
                    GUI.color = new Color(.64f, .79f, .89f);
                    GUI.Label(new Rect(x + 2, y + 47, 94, 17), command, commandStyle);
                }
            }
            GUI.color = new Color(.64f, .79f, .89f);
            GUI.Label(new Rect(842, y + 69, 410, 18),
                Loc.Get("quick_skill.wave_charge", GameInput.KeyLabel(GameInput.AttackKey)), commandStyle);
            GUI.color = previous;
        }
        private void OnDestroy()
        {
            if (skills != null) skills.PresentationRequested -= PresentSkill;
        }
        private void OnApplicationPause(bool paused) { if (paused) FlushProgress(); }
        private void OnApplicationQuit() { FlushProgress(); }
        public bool FlushProgress()
        {
            if (!initialized) return true;
            Area?.FlushVictory();
            reward.Flush();
            skills.Save();
            return skills.LastSaveSucceeded;
        }
        internal static void Bar(Rect rect, Health health, Color color)
        {
            GUI.color = new Color(0.12f, 0.16f, 0.2f); GUI.DrawTexture(rect, Texture2D.whiteTexture);
            rect.width *= (float)health.Current / health.Maximum;
            GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = Color.white;
        }
    }
}
