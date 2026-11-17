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
        private bool checkpoint, won, initialized;
        private float deathTimer;
        private string failure;

        private void Start()
        {
            try { Build(); initialized = true; }
            catch (System.Exception exception) { failure = exception.Message; Debug.LogException(exception); }
        }
        private void Build()
        {
            Application.targetFrameRate = 60;
            Physics2D.gravity = new Vector2(0f, -9.81f);
            Child("Audio").AddComponent<AudioManager>();
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
            controller = player.gameObject.AddComponent<PlayerController2D>();
            boss = CreateActor("Janhyang - yesterday's Rea", LevelBuilder.BossStart, "janhyang-sheet", 12, 0.35f);
            boss.Face(-1);
            player.SetOpponent(boss.Health); boss.SetOpponent(player.Health);
            Physics2D.IgnoreCollision(player.GetComponent<Collider2D>(), boss.GetComponent<Collider2D>());
            bossAI = boss.gameObject.AddComponent<JanhyangBoss>(); bossAI.Initialize(player);

            cameraTarget = Child("Camera target").AddComponent<CameraTarget2D>();
            cameraTarget.Player = player.transform; cameraTarget.Snap();
            camera.gameObject.AddComponent<CinemachineBrain>();
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
        }
        private GameObject Child(string label)
        {
            var go = new GameObject(label); go.transform.SetParent(transform, false); return go;
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
            if (!initialized) return;
            if (Input.GetKeyDown(KeyCode.R)) { ResetRun(true); return; }
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
                boss.ResetAt(LevelBuilder.BossStart);
            if (boss.Health.Dead && !won) won = true;
        }
        private void ResetRun(bool fromStart)
        {
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
        private void OnGUI()
        {
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2f,
                (Screen.height - 720 * scale) / 2f, 0), Quaternion.identity, Vector3.one * scale);
            var title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            var label = new GUIStyle(GUI.skin.label) { fontSize = 17 };
            if (failure != null)
            {
                GUI.Box(new Rect(220, 210, 840, 220), "");
                GUI.Label(new Rect(245, 235, 800, 40), "Prototype could not start. Check Console.", title);
                GUI.Label(new Rect(245, 285, 780, 130), failure, new GUIStyle(label) { wordWrap = true });
                GUI.matrix = previous; return;
            }
            if (!initialized) { GUI.matrix = previous; return; }
            GUI.Box(new Rect(20, 18, 400, 128), "");
            GUI.Label(new Rect(36, 28, 380, 34), "REWIND NOTARY  /  REA", title);
            GUI.Label(new Rect(36, 64, 370, 25), "Rea    " + player.Health.Current + " / " + player.Health.Maximum, label);
            Bar(new Rect(36, 99, 366, 15), player.Health, new Color(0.18f, 0.82f, 0.73f));
            GUI.Label(new Rect(28, 640, 1160, 30), "A/D or arrows: move   |   Space/W/Up: jump   |   J/Z/Left click: sword   |   R: restart", label);
            GUI.Label(new Rect(28, 673, 1160, 28), checkpoint ? "Arena checkpoint active. Watch for the gold windup." : "Move right. Test the ledges and gap, then enter the arena.", label);
            if (bossAI.Active)
            {
                GUI.Box(new Rect(405, 535, 470, 76), "");
                GUI.Label(new Rect(425, 544, 440, 26), "JANHYANG  /  YESTERDAY'S REA     " + boss.Health.Current + " / 12", label);
                Bar(new Rect(425, 579, 430, 12), boss.Health, new Color(0.47f, 0.65f, 0.92f));
            }
            if (player.Health.Dead || won)
            {
                GUI.Box(new Rect(390, 240, 500, 130), "");
                GUI.Label(new Rect(420, 260, 460, 42), won ? "PROTOTYPE CLEARED" : "RETRYING...", title);
                GUI.Label(new Rect(420, 312, 460, 35), won ? "Press R to test again." : "Returning to the checkpoint.", label);
            }
            GUI.matrix = previous;
        }
        private static void Bar(Rect rect, Health health, Color color)
        {
            GUI.color = new Color(0.12f, 0.16f, 0.2f); GUI.DrawTexture(rect, Texture2D.whiteTexture);
            rect.width *= (float)health.Current / health.Maximum;
            GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = Color.white;
        }
    }
}
