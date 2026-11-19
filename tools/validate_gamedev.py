#!/usr/bin/env python3
"""Artifact checks with an optional isolated Unity terrain audit.

Run from any directory: python3 tools/validate_gamedev.py
Dependencies already present in the authoring environment: Pillow, PyYAML, NumPy.
"""
from pathlib import Path
import argparse
import hashlib
import json
import os
import re
import sys
import wave
import yaml
import numpy as np
from PIL import Image
from story_link_baseline import prior_document_bytes

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "unity/RewindNotary"
ASSETS = PROJECT / "Assets"
errors, warnings, checks = [], [], []


def check(condition, message):
    (checks if condition else errors).append(message)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def unity_yaml(path):
    text = path.read_text()
    text = re.sub(r"^%.*\n", "", text, flags=re.M)
    text = re.sub(r"--- !u!\d+ &\d+", "---", text)
    return list(yaml.safe_load_all(text))


snapshot = json.loads((ROOT / "assets/game/source/protected-files.sha256.json").read_text())
for name, expected in snapshot.items():
    path = ROOT / name
    check(path.exists() and hashlib.sha256(prior_document_bytes(path)).hexdigest() == expected, "Protected unchanged: " + name)

manifest = json.loads((PROJECT / "Packages/manifest.json").read_text())
for package in ["com.unity.2d.sprite", "com.unity.cinemachine", "com.unity.modules.physics2d", "com.unity.modules.imgui", "com.unity.modules.audio"]:
    check(package in manifest["dependencies"], "Package present: " + package)

guid_paths = {}
for path in sorted(ASSETS.rglob("*")):
    if path.suffix == ".meta":
        continue
    meta = Path(str(path) + ".meta")
    check(meta.exists(), "Metadata present: " + str(path.relative_to(PROJECT)))
    if meta.exists():
        data = yaml.safe_load(meta.read_text())
        guid = data["guid"]
        check(guid not in guid_paths, "Unique GUID: " + str(path.relative_to(PROJECT)))
        guid_paths[guid] = path

scene_path = ASSETS / "Scenes/Main.unity"
scene_text = scene_path.read_text()
scene = unity_yaml(scene_path)
names = [item["GameObject"]["m_Name"] for item in scene if "GameObject" in item]
check(sorted(names) == ["GameBootstrap", "Main Camera"], "Scene has exactly camera and bootstrap")
ids = set(re.findall(r"^--- !u!\d+ &(\d+)", scene_text, re.M))
for match in re.finditer(r"\{fileID: (\d+)([^}]*)\}", scene_text):
    file_id, rest = match.groups()
    if "guid:" not in rest and file_id != "0":
        check(file_id in ids, "Scene local fileID resolves: " + file_id)
for guid in re.findall(r"guid: ([a-f0-9]{32})", scene_text):
    check(guid in guid_paths and guid_paths[guid].name == "GameBootstrap.cs", "Bootstrap script GUID resolves")

for path in (PROJECT / "ProjectSettings").glob("*.asset"):
    check(bool(unity_yaml(path)), "Settings YAML parses: " + path.name)
build = unity_yaml(PROJECT / "ProjectSettings/EditorBuildSettings.asset")[0]["EditorBuildSettings"]["m_Scenes"]
check(len(build) == 1 and build[0]["enabled"] == 1 and guid_paths.get(build[0]["guid"]) == scene_path,
      "Main is enabled build scene and GUID resolves")
tags = unity_yaml(PROJECT / "ProjectSettings/TagManager.asset")[0]["TagManager"]["layers"]
check(len(tags) == 32 and tags[8] == "Ground", "Ground layer 8 matches physics mask")
settings = unity_yaml(PROJECT / "ProjectSettings/ProjectSettings.asset")[0]["PlayerSettings"]
check(settings["activeInputHandler"] == 0, "Legacy keyboard/mouse input enabled")

png_count = 0
for source in sorted((ROOT / "assets/game").glob("*/*.png")):
    if source.parent.name not in ("sprites", "backgrounds"):
        continue
    png_count += 1
    relative = source.relative_to(ROOT / "assets/game")
    copy = ASSETS / "Resources/GameArt" / relative
    check(copy.exists() and digest(copy) == digest(source), "PNG copy exact: " + str(relative))
    with Image.open(source) as im:
        im.load()
        check(im.format == "PNG", "PNG decodes: " + str(relative))
        imported = yaml.safe_load(Path(str(copy) + ".meta").read_text())["TextureImporter"]
        check(imported["nPOTScale"] == 0 and imported["maxTextureSize"] >= max(im.size), "Texture dimensions preserved: " + source.name)
        check(imported["mipmaps"]["enableMipMap"] == 0 and imported["spriteMode"] == 0,
              "No importer slicing or mipmaps: " + source.name)
        if "sheet" in source.name:
            check(im.mode == "RGBA" and im.getchannel("A").getextrema()[0] == 0, "Sprite real transparency: " + source.name)
            check(im.size == (2048, 768) and im.width % 8 == 0 and im.height % 4 == 0,
                  "Exact 8x4 integer cells, 256x192: " + source.name)
            layout = json.loads(copy.with_name(copy.stem + "-layout.json").read_text())["frames"]
            expected = {(row, col) for row, count in enumerate([4, 8, 3, 4]) for col in range(count)}
            check({(p["row"], p["col"]) for p in layout} == expected and len(layout) == 19, "All 19 frame pivots: " + source.name)
            check(all(0 <= p["x"] <= 1 and 0 <= p["y"] <= 1 for p in layout), "Frame pivots inside cells: " + source.name)
            for row, col in expected:
                bounds = (round(col * im.width / 8), round(row * im.height / 4),
                          round((col + 1) * im.width / 8), round((row + 1) * im.height / 4))
                alpha = im.getchannel("A").crop(bounds)
                check(alpha.getextrema()[1] > 200, f"Occupied frame {source.name} row={row} col={col}")
        if source.name == "stage1-mid.png":
            check(im.mode == "RGBA" and im.getchannel("A").getextrema()[0] == 0, "Midground has transparent sky")
check(png_count == 5, "Exactly five deliverable PNGs")

# Validate actual PCM samples, not just the generator's own declared metrics.
audio_root = ROOT / "assets/game/audio"
audio_manifest = json.loads((audio_root / "audio-manifest.json").read_text())
expected_sfx = {"swing", "hit", "hurt", "jump", "land-thud", "boss-warning", "boss-attack", "boss-defeat", "player-down", "parry", "evolution"}
expected_audio = {f"sfx/{name}.wav" for name in expected_sfx} | {"bgm/field.wav", "bgm/boss.wav"}
check({str(p.relative_to(audio_root)) for p in audio_root.glob("*/*.wav")} == expected_audio,
      "Exactly eleven SFX and two BGM WAV originals")
check({item["file"] for item in audio_manifest["files"]} == expected_audio and len(audio_manifest["files"]) == 13,
      "Audio inventory covers all thirteen files exactly once")
audio_metrics = []
sound_character = {}
for item in audio_manifest["files"]:
    name = item["file"]
    source = audio_root / name
    music = name.startswith("bgm/")
    copy = ASSETS / "Resources/Audio" / ("Bgm" if music else "Sfx") / source.name
    check(source.exists(), "WAV exists: " + name)
    if not source.exists(): continue
    check(digest(source) == item["sha256"], "WAV inventory hash matches: " + name)
    check(copy.exists() and digest(copy) == digest(source), "WAV Unity copy exact: " + name)
    with wave.open(str(source), "rb") as wav:
        channels, rate, width, frames = wav.getnchannels(), wav.getframerate(), wav.getsampwidth(), wav.getnframes()
        check(rate == 44100 and width == 2 and wav.getcomptype() == "NONE", "PCM16 44100 Hz: " + name)
        check(channels == (2 if music else 1), "WAV channel count: " + name)
        check(frames == item["frames"] and abs(frames / rate - item["duration_seconds"]) < 1 / rate,
              "WAV duration matches inventory: " + name)
        pcm = np.frombuffer(wav.readframes(frames), dtype="<i2").reshape(-1, channels).astype(np.float64) / 32768
    peak, rms = float(np.abs(pcm).max()), float(np.sqrt(np.mean(pcm ** 2)))
    seam = float(np.max(np.abs(pcm[0] - pcm[-1])))
    check(0.1 < peak <= 0.81 and 0.01 < rms < 0.5, "Audible PCM with unclipped headroom: " + name)
    check(np.max(np.abs(pcm.mean(axis=0))) < .01, "No significant DC offset: " + name)
    if music:
        check(frames == round(32 * 60 / item["bpm"] * rate), "BGM is exactly 32 beats: " + name)
        derivative = np.diff(pcm, axis=0)
        normal_step = float(np.quantile(np.abs(derivative), .995))
        seam_curve = float(np.max(np.abs((pcm[0] - pcm[-1]) - (pcm[-1] - pcm[-2]))))
        normal_curve = float(np.quantile(np.abs(np.diff(derivative, axis=0)), .999))
        check(seam < .025 and seam <= normal_step * 1.25,
              "Loop seam step within ordinary waveform changes: " + name)
        check(seam_curve <= max(normal_curve * 1.25, 2 / 32768),
              "Loop seam slope continuity: " + name)
        edge_rms = [float(np.sqrt(np.mean(edge ** 2))) for edge in [pcm[:4410], pcm[-4410:]]]
        check(min(edge_rms) > .002, "BGM tail wraps without silent edge padding: " + name)
    else:
        check(np.all(pcm[0] == 0) and np.all(pcm[-1] == 0), "SFX starts and ends at zero: " + name)
    if source.stem == "boss-warning": check(frames == 19845, "Boss warning is exactly 0.45 seconds")
    if source.stem == "boss-defeat": check(1 <= frames / rate <= 2, "Boss defeat fanfare is 1-2 seconds")
    meta = Path(str(copy) + ".meta")
    if meta.exists():
        importer = yaml.safe_load(meta.read_text())["AudioImporter"]
        settings = importer["defaultSettings"]
        check(settings["loadType"] == 0 and settings["compressionFormat"] == 0 and settings["sampleRateSetting"] == 0
              and settings["preloadAudioData"] == 1 and importer["forceToMono"] == 0,
              "Audio importer preserves PCM, rate and channels: " + name)
    audio_metrics.append({"file": name, "seconds": frames / rate, "peak": peak, "rms": rms, "seam_delta": seam})
    if source.stem in ("swing", "land-thud"):
        mono = pcm[:, 0]
        power = np.abs(np.fft.rfft(mono)) ** 2
        frequencies = np.fft.rfftfreq(len(mono), 1 / rate)
        sound_character[source.stem] = {
            "energy_below_300_hz": float(power[frequencies < 300].sum() / power.sum()),
            "power_centroid_hz": float((frequencies * power).sum() / power.sum()),
            "peak_seconds": float(np.argmax(np.abs(mono)) / rate),
        }

landing, swing = sound_character.get("land-thud", {}), sound_character.get("swing", {})
check(landing.get("energy_below_300_hz", 0) > .85 and swing.get("energy_below_300_hz", 1) < .3,
      "Landing is bass-dominant and spectrally distinct from swing (PCM, not listening)")
check(landing.get("peak_seconds", 1) < .015 and swing.get("peak_seconds", 0) > .05,
      "Landing has immediate impact; swing has a delayed wind envelope (PCM)")

# Wiring assertions are source-level checks only; they do not execute Unity callbacks.
scripts = {p.stem: p.read_text() for p in (ASSETS / "Scripts").rglob("*.cs")}
audio_code = scripts["AudioManager"]
names_match = re.search(r"Names\s*=\s*\{([^}]+)\}", audio_code)
check(names_match is not None and set(re.findall(r'"([a-z-]+)"', names_match[1])) == expected_sfx,
      "All SFX Resources names resolve to supplied WAVs")
for path in re.findall(r'Load\("(Audio/Bgm/[^"\n]+)"\)', audio_code):
    check((ASSETS / "Resources" / (path + ".wav")).exists(), "BGM Resources path resolves: " + path)
for owner, calls in {
    "PlayerController2D": ["Sfx.Swing", "Sfx.Jump", "Sfx.Land", "motor.Landed += OnLanded", "motor.Landed -= OnLanded",
                           "motor.AttackStarted += OnAttackStarted", "motor.AttackStarted -= OnAttackStarted"],
    "CombatMotor2D": ["&& opponent.Hit(", "Sfx.Hit", "ImpactFeedback.SwordHit()", "ImpactFeedback.ComboFinisherHit()",
                      "Animation.FlashHit()", "Animation.ResetVisual()"],
    "Health": ["Sfx.PlayerDown", "Sfx.Hurt", "ImpactFeedback.PlayerHurt()"],
    "JanhyangBoss": ["Sfx.BossWarning", "Sfx.BossAttack", "Sfx.BossDefeat", "Music.Boss", "Music.Field", "AudioManager.Stop(warningVoice)"],
    "GameBootstrap": ["AddComponent<AudioManager>()", "AddComponent<ImpactFeedback>()", "AddComponent<ImpactCameraShake>()", "AudioManager.ResetPlayback(checkpoint)", "ImpactFeedback.ResetEffects()"],
}.items():
    check(all(call in scripts[owner] for call in calls), "Sound/feedback source wiring present (not runtime): " + owner)
check("Time.unscaledDeltaTime / CrossfadeSeconds" in audio_code and "new AudioSource[PoolSize]" in audio_code,
      "Source-level pooled SFX and unscaled crossfade present")
feedback = scripts["ImpactFeedback"]
check(all(token in feedback for token in ["Time.unscaledTime", "Time.timeScale = restoreScale", "OnDisable()", "OnDestroy()", "OnApplicationFocus("]),
      "Source-level realtime hit-stop restoration paths present")
check("Time.fixedDeltaTime =" not in feedback, "Feedback does not alter the physics timestep setting")
shake = scripts["ImpactCameraShake"]
check("CinemachineExtension" in shake and "state.PositionCorrection +=" in shake and "transform.position =" not in shake,
      "Shake corrects Cinemachine state without moving the follow target")
shader = ASSETS / "Resources/Shaders/HitFlash.shader"
check(shader.exists(), "Hit flash Resources shader exists")
if shader.exists():
    shader_source = shader.read_text()
    check(all(token in shader_source for token in ["_HitFlashAmount", "color.rgb = lerp", "color.rgb *= color.a", "Blend One OneMinusSrcAlpha"]),
          "Shader white flash retains sprite alpha (source inspection, not compile)")
check('Resources.Load<Shader>("Shaders/HitFlash")' in scripts["SpriteAnimator"] and "MaterialPropertyBlock" in scripts["SpriteAnimator"],
      "SpriteAnimator resolves shader and sets per-renderer flash")

# Combat contracts inspect the actual C# source and numeric profiles, not a Python port of gameplay.
# These checks detect regressions in wiring/tuning; only Unity can validate callback/physics behavior.
def body(source, signature, *, braced=False):
    if braced:
        match = re.search(re.escape(signature) + r'\s*\{', source)
        if not match: return ""
        start = match.end() - 1
    else:
        start = source.find(signature)
        if start < 0: return ""
        start = source.find("{", start)
        if start < 0: return ""
    depth = 1
    for end in range(start + 1, len(source)):
        if source[end] == "{": depth += 1
        elif source[end] == "}": depth -= 1
        if depth == 0: return source[start + 1:end]
    return ""


motor, player, animator, health = (scripts[name] for name in
                                  ("CombatMotor2D", "PlayerController2D", "SpriteAnimator", "Health"))
air = body(motor, "if (Health.IsPlayer && !Grounded)")
check(all(token in air for token in ["Mathf.MoveTowards(Body.linearVelocity.x, speed,", "Time.fixedDeltaTime",
                                    "new Vector2(horizontal, Body.linearVelocity.y)", "Face(speed)", "return;"])
      and "Mathf.Abs(speed) > 0.01f ? AirAcceleration : releaseDrag" in air
      and "linearVelocity = Vector2.zero" not in air,
      "Source contract: player air movement preserves velocity and steers during attacks")
check(0 <= motor.find("if (Health.IsPlayer && !Grounded)") < motor.find("Attacking ? attackMove : speed"),
      "Source contract: air movement returns before grounded attack lock")
player_fixed = body(player, "private void FixedUpdate()")
check(player_fixed.find("motor.Jump(JumpSpeed)") < player_fixed.find("motor.Move(axis * MoveSpeed)"),
      "Source contract: simultaneous jump/attack applies jump before air movement")
check("AirAcceleration = 45f, AirDrag = 6f" in motor and "MoveSpeed = 6.5f" in player,
      "Air tuning: 45 units/s2 steering, 6 units/s2 coast drag, existing 6.5 units/s target")
check(all(token in player for token in ["CoyoteTime = 0.1f", "JumpBuffer = 0.1f", "JumpSpeed = 12f"]),
      "Existing jump speed, coyote time and jump buffer preserved")

combo_match = re.search(r"PlayerCombo\s*=\s*\{([^}]+)\}", motor)
profiles = []
if combo_match:
    for values in re.findall(r"new AttackSpec\(([^)]+)\)", combo_match[1]):
        profiles.append([float(value.strip().removesuffix("f")) for value in values.split(",")])
check(len(profiles) == 4 and all(len(p) == 8 for p in profiles), "Exactly four player combo profiles")
combat_metrics = []
if len(profiles) == 4 and all(len(p) == 8 for p in profiles):
    for step, (duration, strike, window, reach, width, height, damage, knockback) in enumerate(profiles, 1):
        check(0 < strike < duration and 0 < width and 0 < height and reach >= width / 2,
              f"Combo {step}: valid timing and forward attack box")
        check((0 <= window < strike if step < 4 else window == duration),
              f"Combo {step}: input window opens before contact, final step has no buffer")
        combat_metrics.append({"step": step, "duration": duration, "strike_time": strike,
                               "buffer_open": window if step < 4 else None,
                               "buffer_close_exclusive": duration if step < 4 else None,
                               "center_x": reach, "width": width, "height": height,
                               "damage_multiplier": damage, "knockback_multiplier": knockback})
    check(len({p[0] for p in profiles}) == 4 and len({tuple(p[3:6]) for p in profiles}) == 4,
          "Combo durations and hitboxes are distinct at all four steps")
    check([p[6] for p in profiles] == [1, 2, 3, 4] and [p[7] for p in profiles] == [1, 1, 1, 1.7],
          "Combo damage rises 1/2/3/4; only finisher boosts knockback to 1.7x")
    boss_setup = re.search(r'"janhyang-sheet",\s*12,\s*([0-9.]+)f', scripts["GameBootstrap"])
    immunity = float(boss_setup[1]) if boss_setup else float("inf")
    intervals = [profiles[i][0] - profiles[i][1] + profiles[i + 1][1] for i in range(3)]
    check(immunity == .35 and all(gap >= immunity for gap in intervals),
          "Nominal contact intervals 0.38/0.44/0.50s exceed unchanged boss 0.35s immunity")

attack = body(motor, "public bool Attack()")
check(all(token in attack for token in ["Health.Dead || Stunned", "!Health.IsPlayer", "comboStep == 0",
                                       "comboStep >= ComboLimit || queuedAttack", "elapsed < activeAttack.BufferOpen",
                                       "elapsed >= activeAttack.Duration", "queuedAttack = true"]),
      "Source contract: one buffered input, bounded window, no boss/finisher/dead/stunned queue")
check("BeginAttack(0)" in attack and "AttackDuration = 0.32f" in motor
      and "new AttackSpec(AttackDuration, 0.16f, AttackDuration, 0.9f, 1.7f, 1.55f, 1, 1f)" in motor,
      "Boss retains single 0.32s attack, 0.16s contact, original damage and hitbox")
check("Time.time > comboExpiresAt" in attack and "ComboGraceSeconds = 0.18f" in motor
      and "comboExpiresAt = attackStartedAt + activeAttack.Duration + ComboGraceSeconds" in motor,
      "Source contract: combo expires 0.18 game seconds after recovery")
reset_combo = body(motor, "private void ResetCombo()")
check(all(token in reset_combo for token in ["comboStep = 0", "queuedAttack = false", "float.NegativeInfinity"]),
      "Source contract: combo reset clears step, buffered input and expiry")
land_transition = body(motor, "if (Grounded && !wasGrounded)")
check("if (groundSampled && Health.IsPlayer) ResetCombo()" in land_transition
      and "if (groundSampled && !Health.Dead) Landed?.Invoke()" in land_transition
      and "CancelAttack()" not in land_transition,
      "Source contract: real landing resets chain and plays once; current swing completes")
for signature in ["private void ReceiveHit(", "private void Die()", "public void ResetAt(", "private void OnDisable()"]:
    check("CancelAttack()" in body(motor, signature), "Source contract: cancel attack/queue on " + signature)
check("Attacking = false" in body(motor, "private void CancelAttack()")
      and "ResetCombo()" in body(motor, "private void CancelAttack()"),
      "Source contract: attack cancellation also clears combo")
tick = body(motor, "private void Update()")
check("[DefaultExecutionOrder(-50)]" in motor and "Time.time - attackStartedAt" in tick
      and "if (!strikeDelivered && elapsed >= activeAttack.StrikeTime) DeliverStrike()" in tick,
      "Source contract: shared game clock, one strike, motor updates before player input")
check("queuedAttack && comboStep > 0 && comboStep < ComboLimit" in tick
      and "BeginAttack(comboStep + 1)" in tick and "else ResetCombo()" in tick,
      "Source contract: queued steps chain at recovery; finisher resets")
strike = body(motor, "private void DeliverStrike()")
check(strike.find("strikeDelivered = true") < strike.find("if (opponent == null")
      and "opponent.Hit(strikeDamage, transform.position, activeAttack.Knockback)" in strike
      and "strikeDamage = Mathf.CeilToInt(damage * activeAttack.DamageMultiplier * counter)" in body(motor, "private void BeginAttack("),
      "Source contract: whiff/immunity cannot repeat hit query; per-step damage and knockback routed")
check("float knockbackMultiplier = 1f" in health and health.index("KnockbackMultiplier = knockbackMultiplier")
      < health.index("Damaged?.Invoke(source)") and "new Vector2(away * 5f, 4f) * Health.KnockbackMultiplier" in motor
      and "stunUntil = Time.time + 0.18f" in motor,
      "Source contract: default 5/4 knockback and 0.18s stun preserved; finisher multiplier precedes damage event")
begin = body(motor, "private void BeginAttack(")
check("AttackStarted?.Invoke()" in begin and motor.count("AttackStarted?.Invoke()") == 1
      and "Sfx.Swing" not in body(player, "private void Update()")
      and "Sfx.Swing" in body(player, "private void OnAttackStarted()"),
      "Source contract: swing audio plays on actual start, never on queued/ignored input")
check("Sfx.Land" in body(player, "private void OnLanded()")
      and names_match is not None and re.findall(r'"([a-z-]+)"', names_match[1])[4] == "land-thud",
      "Source contract: player landing event resolves Land slot to new land-thud.wav")
check("Animation.PlayAttack(elapsed, activeAttack.StrikeTime, activeAttack.Duration)" in tick
      and "if (!ready || state == Pose.Attack) return" in animator
      and "clips[Pose.Attack] = Slice(3, 0, 4)" in animator,
      "Source contract: motor drives existing four attack frames with no independent animation clock")
check("attackElapsed < strikeTime" in body(animator, "public void PlayAttack(")
      and "? 2 : 3" in body(animator, "public void PlayAttack("),
      "Source contract: slash frame begins at contact time and then recovers")
check(all(token in feedback for token in ["SwordStopScale = 0.05f, SwordStopSeconds = 0.065f",
      "HurtStopScale = 0.2f, HurtStopSeconds = 0.045f", "SwordShake = 0.07f, HurtShake = 0.18f, DefeatShake = 0.32f",
      "Shake(SwordShake, 0.12f)", "Shake(HurtShake, 0.22f)", "Shake(DefeatShake, 0.45f)"])
      and "HitFlashSeconds = 0.09f" in animator, "Original ordinary hit-stop, shake and flash tuning preserved")
check("FinisherStopScale = 0.03f, FinisherStopSeconds = 0.11f" in feedback
      and "HitStop(FinisherStopScale, FinisherStopSeconds)" in body(feedback, "public static void ComboFinisherHit()")
      and "attackStep == PlayerCombo.Length" in strike,
      "Source contract: only successful fourth hit requests stronger 0.03x/0.11s hit-stop")

from validate_skills import validate_skills
skill_metrics = validate_skills(check, ASSETS, scripts, body)
from validate_skeleton import validate_skeleton
skeleton_metrics = validate_skeleton(check, PROJECT, scripts, body, unity_yaml)
from validate_dash_parry import validate_dash_parry
dash_parry_metrics = validate_dash_parry(check, PROJECT, scripts, body)
from validate_remaining_skills import validate_remaining_skills
remaining_skill_metrics = validate_remaining_skills(check, PROJECT, scripts, body)
from validate_evolution_ui import validate_evolution_ui
evolution_ui_metrics = validate_evolution_ui(check, PROJECT, scripts, body)
from validate_waves import validate_waves
wave_metrics = validate_waves(check, PROJECT, scripts, body)
from validate_janhyang import validate_janhyang
janhyang_metrics = validate_janhyang(check, PROJECT, scripts, body)
from validate_benefactor_trials import validate_benefactor_trials
benefactor_trial_metrics = validate_benefactor_trials(check, PROJECT, scripts, body)

from validate_investigation import validate_investigation
investigation_metrics = validate_investigation(check, PROJECT, scripts, body)
from validate_cassel import validate_cassel
cassel_metrics = validate_cassel(check, PROJECT, scripts, body)
from validate_save_integrity import validate_save_integrity
save_integrity_metrics = validate_save_integrity(check, PROJECT, scripts, body)
from validate_build_pipeline import validate_build_pipeline
build_pipeline_metrics = validate_build_pipeline(check, PROJECT, body, unity_yaml)
from validate_localization import validate_localization
localization_metrics = validate_localization(check, PROJECT, scripts, body)
from validate_input_scheme import validate_input_scheme
input_scheme_metrics = validate_input_scheme(check, PROJECT, scripts, body,
    {name: value for name, value in list(globals().items()) if name.endswith('_metrics')})

from validate_scene_links import validate_scene_links
scene_link_metrics = validate_scene_links(check, PROJECT, scripts, body)
from validate_area_links import validate_area_links
area_link_metrics = validate_area_links(check, PROJECT, scripts)
from validate_finale_links import validate_finale_links
finale_link_metrics = validate_finale_links(check, PROJECT, scripts, body)
from validate_story_links import validate_story_links
story_link_metrics = validate_story_links(check, PROJECT, scripts, body)

from validate_flow_assembly import validate_flow_assembly
flow_assembly_metrics = validate_flow_assembly(check, PROJECT, scripts, body)

from validate_autotest import validate_autotest
autotest_metrics = validate_autotest(check, PROJECT, scripts, body)
from validate_feel_pass import validate_feel_pass
feel_metrics = validate_feel_pass(check, PROJECT, scripts, body)

from validate_level_step1 import validate_level_step1
level_step1_metrics = validate_level_step1(check, PROJECT, scripts)
from validate_level_step2 import validate_level_step2
level_step2_metrics = validate_level_step2(check, PROJECT, scripts)

from validate_level_step3 import validate_level_step3
level_step3_metrics = validate_level_step3(check, PROJECT, scripts)

from validate_level_step4 import validate_level_step4
level_step4_metrics = validate_level_step4(check, PROJECT, scripts)

from validate_level_step5 import validate_level_step5
level_step5_metrics = validate_level_step5(check, PROJECT, scripts)
from validate_level_step6 import validate_level_step6
level_step6_metrics = validate_level_step6(check, PROJECT, scripts)

# A lexical delimiter check catches accidental patch truncation, NOT C# compilation.
for path in ASSETS.rglob("*.cs"):
    source = path.read_text()
    scrubbed = re.sub(r'''//[^\n]*|/\*[\s\S]*?\*/|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])' ''', "", source, flags=re.X)
    stack = []
    pairs = {')': '(', ']': '[', '}': '{'}
    balanced = True
    for char in scrubbed:
        if char in "([{": stack.append(char)
        elif char in ")]}":
            if not stack or stack.pop() != pairs[char]: balanced = False; break
    check(balanced and not stack, "C# delimiters balanced (not compile): " + path.name)

warnings.extend([
    "Bell step 5: fixed-tick mobility, silent refuge margins and per-station opportunities are offline checks. Slot-5 contact and width measurements require Unity.",
    "Opera step 4: fixed-tick paths and station opportunities are checked offline. Actual collider widths, slot-4 contacts, shortcut time and Game view require Unity.",
    "Clock step 3: fixed routes, first-visit supply and gate conditions checked statically. Third-slot contacts, timed recall and lift trips require Unity.",
    "Velvet step 2: ordinary-jump trajectories, seam budget and shortcut gates are static checks. The six checkpoint intervals and updated playthrough require Unity.",
    "Finale scenes: authored text, source wiring, checkpoint walks and 120 return-order projections are static checks. Hybrid captions, physics, menu input and real save/resume require Unity Play Mode.",
    "Directional input: source contracts, extracted decision expressions and Python references only. Press-time facing/ground, dash buffering, canvas attack-hold, HUD fit and physical controller axes require Unity Play Mode.",
    "Localization: nine tables, placeholders, exact Korean-literal allowlist, immutable migration hashes and Python settings/fallback references only. Native speaker review, CJK/Cyrillic font assets, Unity compilation, live switching, text fit and target-OS persistence remain unverified.",
    "Build pipeline: source contracts and Python format/failure fixtures only; PowerShell, C# compilation, Unity batchmode, Standalone artifacts and target-device launch were not executed.",
    "Save integrity: strict JSON/source contracts and full-field Python disk/fault references only. C# parser, JsonUtility, target-OS File.Replace/Flush, power loss, Korean notices and actual stage lifecycle require Unity 6000.0.65f1 verification.",
    "Cassel: 120 permutations are source/data contracts plus Python finite-state references, not executed C#/Unity. Match timing, 3-second collider fade, no-fall physics, v4 JsonUtility migration, and 10 representative order fairness playtests remain unverified.",
    "Representative area and investigation: source/data checks and Python route, reading and persistence references. Unity room transitions, blade/plank contact, the first 80-use return, terminal layout, silent stills, controller scrolling and real-slot recovery require Play Mode.",
    "Benefactor trials use source/data contracts and Python persisted-state references only; five opponent patterns, all-stage objectives, support effects, controller input, Korean panel fit and save-failure behavior require Unity Play Mode.",
    "Janhyang uses source/data contracts and Python reference scenarios only; enemy imitation silhouettes, phase/counter timing, physics, audio and F7/preview entry require Unity Play Mode. The recent window observes the current session's JSONL source events; historical JSONL files are not replayed.",
    "Wave supply is a data-only finite-sequence projection with source contracts and Python references; target reachability, parry/dash openings, combat pacing and the limited 1.8x reinforcement delay need Unity Play Mode measurement.",
    "Evolution/UI checks cover source wiring, recovery-time hashes and Python boundary/panel/lifecycle references only; Unity rendering, pause timing, Korean font fit, controller navigation and evolution sound require Play Mode.",
    "Remaining skills use source/data contracts and Python reference timelines; local freeze, swept return collision, air-step contacts, counter timing, paint support and echo replay require Unity Play Mode.",
    "Dash/parry source contracts and Python state/boundary reference scenarios passed only if listed in checks; Unity physics, execution order, input, audio and combat feel remain untested.",
    "Menu transitions, slots/migration/deletion and settings are source contracts plus Python disk reference scenarios; Unity/C# IO and physical Xbox input remain untested.",
    "Unity Editor, C# compiler and package restore not available: no compile, Play Mode or build verification.",
    "Source ground is not mathematically seamless; runtime mirror repetition joins matching edge pixels.",
    "Sprite pose/scale consistency and platform/combat feel require visual Play Mode review.",
    "WAVs and loop seams checked numerically; listening, Unity audio/Shader import, hit-stop/camera timing and device mix remain unverified.",
    "Combo checks inspect C# contracts and numeric profiles only; buffering/reset boundaries, air steering and sprite continuity require Unity Play Mode.",
    "Skill boundaries use catalog data; accounting and JSON round trips use a Python reference model tied to source contracts, not executed C#/JsonUtility/File.Replace.",
    "Korean HUD uses OS font fallback; glyph coverage, binding/floor contacts, save permissions and presentation require Unity Play Mode on target OS.",
])
report = {"passed": len(checks), "errors": errors, "warnings": warnings, "checks": checks,
          "audio_metrics": audio_metrics, "sound_character": sound_character, "combat_metrics": combat_metrics,
          "skill_metrics": skill_metrics, "skeleton_metrics": skeleton_metrics, "dash_parry_metrics": dash_parry_metrics,
          "remaining_skill_metrics": remaining_skill_metrics,
          "evolution_ui_metrics": evolution_ui_metrics,
          "wave_metrics": wave_metrics,
          "janhyang_metrics": janhyang_metrics,
          "benefactor_trial_metrics": benefactor_trial_metrics,
          "investigation_metrics": investigation_metrics,
          "cassel_metrics": cassel_metrics,
          "save_integrity_metrics": save_integrity_metrics,
          "build_pipeline_metrics": build_pipeline_metrics,
          "localization_metrics": localization_metrics,
          "input_scheme_metrics": input_scheme_metrics,
          "story_link_metrics": story_link_metrics,
          "scene_link_metrics": scene_link_metrics,
          "area_link_metrics": area_link_metrics,
          "finale_link_metrics": finale_link_metrics,
          "flow_assembly_metrics": flow_assembly_metrics,
          "autotest_metrics": autotest_metrics,
          "feel_metrics": feel_metrics, "level_step1_metrics": level_step1_metrics,
          "level_step2_metrics": level_step2_metrics, "level_step3_metrics": level_step3_metrics,
          "level_step4_metrics": level_step4_metrics, "level_step5_metrics": level_step5_metrics, "level_step6_metrics": level_step6_metrics, "unity_executed": False, "csharp_compiled": False}
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output', type=Path, help='Write UTF-8 JSON atomically and print a short summary.')
parser.add_argument('--unity-editor', default=os.environ.get('UNITY_EDITOR'),
                    help='Unity executable to compile and measure all authored terrain in Edit Mode (default: UNITY_EDITOR).')
args = parser.parse_args()
if args.unity_editor:
    from validate_terrain_bounds import measure_editor_tiles
    terrain = level_step6_metrics['terrainBounds']
    try:
        terrain['editorReadings'] = measure_editor_tiles(args.unity_editor, PROJECT, terrain, check)
        report.update(unity_executed=True, csharp_compiled=True)
    except (OSError, RuntimeError, ValueError) as e:
        check(False, f'Unity terrain audit failed: {e}')
    report['passed'] = len(checks)
serialized = json.dumps(report, ensure_ascii=False, indent=2) + '\n'
if args.output:
    temporary = args.output.with_name(args.output.name + '.tmp')
    temporary.write_text(serialized, encoding='utf-8')
    temporary.replace(args.output)
    print(json.dumps({'passed': len(checks), 'errors': errors, 'output': str(args.output)}, ensure_ascii=False))
else:
    print(serialized, end='')
sys.exit(bool(errors))
