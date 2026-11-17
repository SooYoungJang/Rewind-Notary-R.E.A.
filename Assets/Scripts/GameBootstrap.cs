using UnityEngine;
using Unity.Cinemachine;

namespace RewindNotary
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        private CombatMotor2D player, boss;
        private PlayerController2D controller;
        private JanhyangBoss bossAI;
        private CameraTarget2D cameraTarget;
        private CinemachineCamera virtualCamera;
        private SkillSession skills;
        private GameShell shell;
        private GameObject world;
        private EncounterRewardBridge reward;
        private BenefactorEncounter encounter;
        private SkillPracticeArena2D practice;
        private StitchBinding bossBinding;
        private Font hudFont;
        private bool checkpoint, won, initialized;
        private float deathTimer;
        private string failure;

        private void Start()
        {
            try
            {
                Application.targetFrameRate = 60;
                Physics2D.gravity = new Vector2(0f, -9.81f);
                Time.timeScale = 1f;
                Child("Audio").AddComponent<AudioManager>();
                hudFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial Unicode MS" }, 18);
                shell = gameObject.AddComponent<GameShell>();
                shell.Initialize(this, hudFont);
            }
            catch (System.Exception exception) { failure = exception.Message; Debug.LogException(exception); }
        }
        public void StartStage(SkillSaveStore store, CoreRecord record)
        {
            if (world != null) throw new System.InvalidOperationException("이미 스테이지가 실행 중입니다.");
            world = new GameObject("Playable stage"); world.transform.SetParent(transform, false);
            SkillStroke.WorldRoot = world.transform;
            checkpoint = won = false; deathTimer = 0;
            Build(store, record); initialized = true;
            if (GameShell.DebugAvailable) ApplyStageOverride(shell.Settings.Value.stageOverride);
        }
        private void Build(SkillSaveStore store, CoreRecord record)
        {
            skills = Child("Skill session").AddComponent<SkillSession>();
            encounter = BenefactorEncounter.ForStage(record.lastStageId);
            skills.EncounterId = record.lastStageId == "skill_lab" ? "practice_skill_lab" : encounter.EncounterId;
            skills.Initialize(store, record);
            skills.PresentationRequested += PresentSkill;
            Camera camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera"); go.tag = "MainCamera";
                camera = go.AddComponent<Camera>(); go.AddComponent<AudioListener>();
            }
            camera.orthographic = true; camera.orthographicSize = 5.5f;
            camera.nearClipPlane = 0.1f; camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.53f, 0.70f, 0.85f);
            camera.transform.SetPositionAndRotation(new Vector3(0, 2.7f, -10), Quaternion.identity);
            Child("Level").AddComponent<LevelBuilder>().Build();
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
            bossAI = boss.gameObject.AddComponent<JanhyangBoss>(); bossAI.Initialize(player);
            reward = boss.gameObject.AddComponent<EncounterRewardBridge>();
            reward.EncounterId = encounter.EncounterId; reward.RewardSkillId = encounter.SkillId;
            reward.Initialize(boss.Health, skills.Progression);
            controller.Silver = player.gameObject.AddComponent<SilverStitchSkill>();
            controller.Silver.Initialize(skills, boss);
            player.BenefactorSkills = player.gameObject.AddComponent<BenefactorSkillRunner>();
            player.BenefactorSkills.Initialize(skills, boss, controller.Silver);
            SkillPracticeWorld.Build(Child("Skill practice").transform, player, boss);
            if (record.lastStageId != "prototype")
            {
                practice = Child("Remaining skill practice").AddComponent<SkillPracticeArena2D>();
                practice.Initialize(player, boss);
            }
            if (record.lastStageId == "skill_lab")
            {
                skills.Progression.GrantPracticeSkills();
                player.ResetAt(new Vector2(SkillGeometry.PracticeStart, 0));
            }

            cameraTarget = Child("Camera target").AddComponent<CameraTarget2D>();
            cameraTarget.Player = player.transform; cameraTarget.Snap();
            if (camera.GetComponent<CinemachineBrain>() == null) camera.gameObject.AddComponent<CinemachineBrain>();
            virtualCamera = Child("Cinemachine follow camera").AddComponent<CinemachineCamera>();
            virtualCamera.Follow = cameraTarget.transform;
            var lens = LensSettings.Default;
            lens.OrthographicSize = 5.5f; lens.NearClipPlane = 0.1f; lens.FarClipPlane = 100f;
            virtualCamera.Lens = lens;
            var composer = virtualCamera.gameObject.AddComponent<CinemachinePositionComposer>();
            composer.CameraDistance = 10f;
            composer.TargetOffset = new Vector3(0f, 1.2f, 0f);
            composer.Damping = new Vector3(0.18f, 0.35f, 0f);
            var shake = virtualCamera.gameObject.AddComponent<ImpactCameraShake>();
            Child("Impact feedback").AddComponent<ImpactFeedback>().Initialize(shake);
            Child("Parallax").AddComponent<ParallaxBackground>().Initialize(camera);
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
            return motor;
        }
        private void Update()
        {
            if (!initialized || !shell.Playing || !GameInput.CanPlay) return;
            if (Debug.isDebugBuild || Application.isEditor) UpdateSkillDebug();
            if (GameInput.RetryDown) { ResetRun(true); return; }
            if (player.Health.Dead)
            {
                deathTimer += Time.deltaTime;
                if (deathTimer >= 1.1f) ResetRun(false);
                return;
            }
            if (player.transform.position.y < -7f)
            {
                ResetRun(false);
                AudioManager.Play(AudioManager.Sfx.PlayerDown);
                return;
            }
            if (!checkpoint && player.transform.position.x >= 35f)
            { checkpoint = true; bossAI.Activate(); }
            if (!boss.Health.Dead && boss.transform.position.y < -7f)
            { bossBinding.Break(); boss.ResetAt(LevelBuilder.BossStart); }
            if (boss.Health.Dead && !won) won = true;
        }
        private void ResetRun(bool fromStart)
        {
            // A retry in the lethal-hit frame must deliver the queued reward before invalidating uses.
            reward.Flush();
            skills.Progression.ResetAttempt();
            skills.Save();
            SkillPracticeWorld.ResetAttempt();
            SkillProjectile2D.ClearAll(); practice?.ResetAttempt();
            foreach (var target in ResonanceTarget2D.All) target.ResetAttempt();
            foreach (var lamp in PracticeLamp2D.All) lamp.ResetAttempt();
            foreach (var hazard in PracticeTemporalHazard2D.All) hazard.ResetAttempt();
            bossBinding.Break();
            if (fromStart) checkpoint = false;
            ImpactFeedback.ResetEffects();
            AudioManager.ResetPlayback(checkpoint);
            won = false; deathTimer = 0;
            player.ResetAt(checkpoint ? LevelBuilder.ArenaStart : LevelBuilder.StartPoint);
            controller.ClearInput();
            boss.ResetAt(LevelBuilder.BossStart); boss.Face(-1);
            bossAI.ResetPattern(); if (checkpoint) bossAI.Activate();
            cameraTarget.Snap();
            virtualCamera.PreviousStateIsValid = false;
        }
        private void UpdateSkillDebug()
        {
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
        public void ApplyStageOverride(int stage)
        {
            if (!initialized || !GameShell.DebugAvailable || skills.PreviewStage == stage) return;
            ResetRun(false);
            skills.SetPreview(stage);
        }
        public void SuspendInput() { if (controller != null) controller.SuspendInput(); }
        public void StopStage()
        {
            // Caller has saved successfully; close the session before deferred destruction.
            if (skills != null) { skills.PresentationRequested -= PresentSkill; skills.Close(false); }
            initialized = false; SkillStroke.WorldRoot = null;
            ImpactFeedback.ResetEffects(); AudioManager.ResetPlayback(false);
            if (world != null) { world.SetActive(false); Destroy(world); world = null; }
            player = boss = null; controller = null; skills = null; reward = null; practice = null;
        }
        public void AbortStage()
        {
            if (skills != null) skills.Save();
            StopStage();
        }
        private void PresentSkill(string cue, string skillId)
        {
            if (player == null) return;
            Color color = cue == "doom_transfer" || cue == "doom_restored" ? new Color(1f, 0.3f, 0.4f) : Color.cyan;
            SkillStroke.Show(player.transform.position + Vector3.up * 0.3f,
                player.transform.position + Vector3.up * 2.5f, color, 0.8f);
        }
        private string SkillLabel(string id)
        {
            var progress = skills.Progression.Record.Get(id);
            string name = skills.Progression.Catalog.Get(id).displayName;
            return name + " · " + progress.validUseCount + "회 · " + progress.stage + "단계"
                + (progress.acquired ? "" : " (미보유)");
        }
        private void OnGUI()
        {
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
                GUI.Label(new Rect(245, 235, 800, 40), "시작할 수 없습니다. 콘솔을 확인해 주세요.", title);
                GUI.Label(new Rect(245, 285, 780, 130), failure, new GUIStyle(label) { wordWrap = true });
                GUI.matrix = previous; return;
            }
            if (!initialized || !shell.Playing) { GUI.matrix = previous; return; }
            GUI.Box(new Rect(20, 18, 400, 128), "");
            GUI.Label(new Rect(36, 28, 380, 34), "리와인드 공증록: 레아", title);
            GUI.Label(new Rect(36, 64, 370, 25), "레아    " + player.Health.Current + " / " + player.Health.Maximum, label);
            Bar(new Rect(36, 99, 366, 15), player.Health, new Color(0.18f, 0.82f, 0.73f));
            GUI.Box(new Rect(20, 154, 440, 278), "");
            GUI.Label(new Rect(36, 164, 420, 28), SkillLabel(SkillRules.BasicSword), label);
            GUI.Label(new Rect(36, 194, 420, 28), "1 " + SkillLabel(SkillRules.SilverStitch), label);
            for (int i = 1; i < BenefactorIds.All.Length; i++)
                GUI.Label(new Rect(36, 194 + i * 28, 420, 28), (i + 1) + " " + SkillLabel(BenefactorIds.All[i]), label);
            GUI.Label(new Rect(36, 338, 420, 28), "선택: " + skills.Progression.Catalog.Get(player.BenefactorSkills.SelectedId).displayName, label);
            string mode = skills.Progression.Record.testData ? "검증 저장 (본 저장과 분리)" : "슬롯 " + skills.ActiveSlot + " 저장";
            if (skills.PreviewStage != 0) mode += " · 형태 " + skills.PreviewStage + "단계 미리보기 (집계 제외)";
            GUI.Label(new Rect(36, 372, 420, 52), mode, new GUIStyle(label) { wordWrap = true });
            var defense = player.Defense.State;
            string defenseStatus = defense.Phase == DefensePhase.Dash ? "대시 중"
                : defense.Phase == DefensePhase.ParryWindow ? "패링 판정 중"
                : defense.Phase == DefensePhase.ParryRecovery ? "패링 실패 · 경직"
                : defense.CounterReady(Time.time) ? "반격 가능 · 다음 검술 강화" : "방어 준비";
            GUI.Label(new Rect(36, 438, 420, 26), defenseStatus, label);
            GUI.Label(new Rect(36, 466, 420, 26), "대시 " + Mathf.Max(0f, defense.DashReadyAt - Time.time).ToString("0.0")
                + "초 · 공중 " + (defense.AirDashUsed ? "소진" : "1회"), label);
            if (!string.IsNullOrEmpty(skills.Notice))
            {
                GUI.Box(new Rect(470, 30, 785, 94), "");
                GUI.Label(new Rect(486, 43, 750, 78), skills.Notice, new GUIStyle(label) { wordWrap = true });
            }
            if (!string.IsNullOrEmpty(skills.StorageError))
                GUI.Label(new Rect(470, 424, 760, 65), skills.StorageError, new GUIStyle(label) { wordWrap = true });
            GUI.Label(new Rect(28, 640, 1220, 30), "이동 A/D·방향키·스틱 | 점프 Space/W/↑·A | 검술 J/Z/좌클릭·X | 기술 Q·Y | 대시 Shift/K·LB | 패링 L·RB", label);
            GUI.Label(new Rect(28, 673, 1220, 28), (checkpoint ? "투기장 복귀 지점 활성 · 금빛 예고를 살피세요." : "오른쪽 발판과 틈을 건너 대련장으로 향하세요.")
                + "  R·보기: 재시작 | Esc·메뉴: 일시정지", label);
            if (Debug.isDebugBuild || Application.isEditor)
                GUI.Label(new Rect(28, 606, 1220, 28), "기술 1~5 / E·B 선택 · 화폭 벽 S | F1~F3 전체 형태 | F4 실제 단계 | F5 경계 150/29 | F6 경계 399/79 (검증 저장)", label);
            if (bossAI.Active)
            {
                GUI.Box(new Rect(405, 510, 470, 76), "");
                GUI.Label(new Rect(425, 519, 440, 26), encounter.Label + "     " + boss.Health.Current + " / 12", label);
                Bar(new Rect(425, 554, 430, 12), boss.Health, new Color(0.47f, 0.65f, 0.92f));
            }
            if (player.Health.Dead || won)
            {
                GUI.Box(new Rect(485, 270, 500, 130), "");
                GUI.Label(new Rect(515, 290, 460, 42), won ? "테스트 대련 완료" : "다시 도전합니다…", title);
                GUI.Label(new Rect(515, 342, 460, 35), won ? "R / 패드 보기 버튼으로 다시 도전하세요." : "복귀 지점으로 돌아갑니다.", label);
            }
            GUI.matrix = previous;
        }
        private void OnDestroy()
        {
            if (skills != null) skills.PresentationRequested -= PresentSkill;
            if (hudFont != null) Destroy(hudFont);
        }
        private void OnApplicationPause(bool paused) { if (paused) FlushProgress(); }
        private void OnApplicationQuit() { FlushProgress(); }
        public bool FlushProgress()
        {
            if (!initialized) return true;
            reward.Flush();
            skills.Save();
            return skills.LastSaveSucceeded;
        }
        private static void Bar(Rect rect, Health health, Color color)
        {
            GUI.color = new Color(0.12f, 0.16f, 0.2f); GUI.DrawTexture(rect, Texture2D.whiteTexture);
            rect.width *= (float)health.Current / health.Maximum;
            GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = Color.white;
        }
    }
}
