# 사운드 패키지·타격감 추가 보고서

2026-11-17. 의뢰: `../../codex-sound-brief.md`.

WAV 효과음 9종, 루프 BGM 2종, 오디오 연결, 히트스톱·화면 흔들림·흰색 타격 플래시를 저장했다. **정적 검증 392개 통과·오류 0. Unity import/C#·셰이더 컴파일/Play/빌드 및 실제 청취는 미검증이다.**

## 파일과 생성 방식

- 원본: `../../assets/game/audio/sfx/*.wav`, `../../assets/game/audio/bgm/*.wav`.
- 동일 바이트 프로젝트 복사본: `Assets/Resources/Audio/Sfx/`, `Assets/Resources/Audio/Bgm/`.
- [파일별 설명·길이·합성 방식](../../assets/game/audio/AUDIO-LIST.md), [실측/해시 목록](../../assets/game/audio/audio-manifest.json).
- 생성기: `../../tools/generate_audio.py` (Python + NumPy, seed 20261117). 외부 음원·다운로드 샘플 없이 사인파·비정수 배음 벨·chirp·필터 노이즈를 합성했다.
- PCM 16-bit, 44,100 Hz. SFX mono, BGM stereo. SFX 0.16~1.80초, boss-warning 정확히 0.45초. field 20초/96 BPM, boss 16초/120 BPM, 각각 8마디.
- BGM 마지막 노트와 반사음의 잔향을 주기 버퍼 시작으로 감싸 더했다. 전체 fade-in/out이나 무음 패딩 없이 정수 샘플 루프를 만든다. 첫/끝 값의 차이는 일반 파형 변화보다 작으며, 경계 기울기도 검사했다.
- 새 WAV·스크립트·셰이더·폴더의 고정 GUID `.meta` 포함. PCM/DecompressOnLoad/PreserveSampleRate/PreloadAudioData 임포트 설정과 에디터 임포터를 함께 제공한다.

## 신규 코드

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/AudioManager.cs` | Resources 로드, 12개 효과음 소스 풀, 2개 루프 BGM 소스, 0.8초 선형 크로스페이드 |
| `Assets/Scripts/ImpactFeedback.cs` | 실제 시간 기준 히트스톱·배율 복원, 상황별 셰이크 요청 |
| `Assets/Scripts/ImpactCameraShake.cs` | CinemachineExtension의 Finalize 단계에서 PositionCorrection에 감쇠 오프셋 추가 |
| `Assets/Resources/Shaders/HitFlash.shader` | 스프라이트 RGB를 흰색으로 보간하고 원본 알파를 유지하는 Built-in 셰이더 |

오디오 매니저는 씬의 자식이며 별도 수동 연결이 필요 없다. SFX는 빈 소스를 우선 사용하고, 포화되면 가장 오래된 일반 효과음을 교체한다. 예고·사망·처치 소스는 교체 대상에서 제외한다. 음성 핸들에 세대 번호를 두어 예전 예고 취소가 다른 소리를 끊지 않도록 했다. 기본 Master/SFX/Music 볼륨은 0.8/0.7/0.4이며 Play 중 Audio 오브젝트에서 조절할 수 있다.

BGM은 현재 재생 위치와 혼합 비율에서 전환하므로 도중에 필드↔보스를 다시 요청해도 볼륨이 갑자기 초기화되지 않는다. 전환은 `unscaledDeltaTime`을 사용하며 히트스톱 중에도 진행한다. 오디오 누락 시 경고를 기록하고 게임 동작은 계속한다.

## 연결 지점

| 기존 코드 | 연결·동작 |
|---|---|
| `GameBootstrap.Build` | AudioManager 생성, 가상 카메라에 셰이크 확장 부착, ImpactFeedback 생성. 플레이어 Health에 명시적 IsPlayer 설정 |
| `PlayerController2D.Update` | `motor.Attack()`이 true일 때만 swing |
| `PlayerController2D.FixedUpdate` | 실제 점프를 적용한 순간 jump |
| `CombatMotor2D.RefreshGround` → `PlayerController2D.OnLanded` | 공중→지면 전이 시 land. 초기 지면 샘플은 억제, 리셋 시 초기화 |
| `CombatMotor2D.Update` | 검 범위 겹침 + `Health.Hit()` 성공일 때 hit 1회. 보스에 대한 타격에는 SwordHit 연출 |
| `Health.Hit` | 유효 플레이어 피격에 hurt + PlayerHurt 연출. 치명타에는 hurt 대신 player-down |
| `CombatMotor2D.ReceiveHit` | 양쪽 피격 대상에 흰색 플래시. 기존 공격 취소·경직·넉백 유지 |
| `JanhyangBoss.Activate` | 아레나 진입 시 보스 BGM 요청. 중복 Activate는 재시작하지 않음 |
| `JanhyangBoss.FixedUpdate` | 0.45초 windup 시작 시 boss-warning, 실제 공격 시작 성공 시 boss-attack |
| `JanhyangBoss.OnDamaged / OnDefeated` | 예고 취소. 처치 시 boss-defeat·최대 흔들림·필드 BGM 복귀 |
| `JanhyangBoss`의 플레이어 Died 구독 | 플레이어 사망 시 진행 중 예고음 취소 |
| `GameBootstrap.ResetRun` | 히트스톱/셰이크 해제, SFX 정리, 체크포인트 상태에 따라 BGM 전환. R은 필드, 아레나 복귀는 보스 |
| `GameBootstrap`의 낙하 복귀 | 즉시 복귀 흐름을 유지하고 오디오 초기화 뒤 player-down 재생 |
| `SpriteAnimator` | 무적/사망/예고 색과 흰색 플래시를 한 곳에서 합성. ResetAt에서 시각 상태 복원 |

보스 예고음의 시작과 종료는 기존 windup에 연결했다. 예고음만 Time.timeScale에 맞춰 pitch를 조절하며, 보스 피격·플레이어 사망·리셋으로 예고가 취소되면 소리도 중단한다. FixedUpdate/오디오 버퍼 때문에 샘플 단위의 완전한 동기는 보장하지 않는다. 그 외 효과음과 BGM의 pitch는 히트스톱 중에도 유지한다.

## 타격 연출 파라미터

| 상황 | 시간 배율 / 실제 지속 시간 | 셰이크 최대 X / 지속 시간 |
|---|---|---|
| 플레이어 검 타격 성공 | 0.05 / 0.065초 | 0.07 world unit / 0.12초 |
| 플레이어 유효 피격 | 0.20 / 0.045초 | 0.18 world unit / 0.22초 |
| 보스 처치 | 마지막 검의 히트스톱 적용 | 0.32 world unit / 0.45초 |

셰이크 Y 진폭은 X의 65%, X/Y 주파수는 27/39 Hz이며 제곱 감쇠한다. 강한 처치 흔들림을 같은 프레임의 작은 검 타격이 덮어쓰지 않도록 현재 강도·남은 시간을 합친다. 추적 타깃·플레이어 transform은 이동시키지 않는다. 기존 카메라 추적 범위·댐핑·패럴랙스 코드는 유지했다.

히트스톱은 `unscaledTime`으로 만료하며 시작 전 배율을 복원한다. 중복 타격은 종료 시간을 연장한다. `fixedDeltaTime` 설정은 바꾸지 않는다. R/복귀, 컴포넌트 비활성화/파괴, 앱 포커스 해제/일시정지 시 복원한다. 외부 시스템이 배율을 변경한 것이 감지되면 그 값을 덮어쓰지 않는다.

흰색 플래시는 **실제 시간 0.09초**. 우선순위는 **흰색 타격 → 사망 색 → 무적 점멸 → 보스 황금 예고 → 기본 색**이다. Color.white 곱셈만으로 처리하지 않고, 셰이더의 `_HitFlashAmount`를 MaterialPropertyBlock으로 제어한다. 투명한 배경은 투명하게 남으며 치명타도 플래시 후 기존 사망 색으로 돌아간다. 스프라이트/머티리얼은 파괴 시 해제한다.

## 검증 결과와 재현

프로젝트 상위 루트에서 실행:

```sh
python3 tools/generate_audio.py
python3 tools/validate_gamedev.py > unity/RewindNotary/STATIC-VALIDATION.json
```

생성기에는 NumPy, 검증기에는 NumPy/Pillow/PyYAML이 필요하다. 검증기는 입력 파일을 읽고 결과 JSON을 표준 출력에 내보낸다.

- [STATIC-VALIDATION.json](STATIC-VALIDATION.json): **392개 통과, 오류 0, 경고 4**. 기존 보호 파일 SHA-256·PNG·씬·프로젝트 설정·GUID 검사를 포함한다.
- WAV 11종의 실제 헤더/프레임 수/채널/피크/RMS/DC/효과음 무음 끝점/원본↔Unity 복사 해시를 검사했다.
- BGM 2종의 32박 길이, 경계 샘플 변화량·기울기, 앞뒤 0.1초의 유효 신호를 확인했다. PCM 실측 경계 변화량은 field 약 0.003845, boss 약 0.001404이다.
- 격리된 임시 폴더에서 다시 합성한 **11/11 WAV SHA-256이 원본과 일치**했다.
- 두 BGM 모두 `ffmpeg -v error -i <파일> -f null -` 전체 디코드에서 오류가 없었다.
- 신규 코드도 기존 C# 구분자 검사 대상이다. 오디오 경로/연결/배율 복원 경로/셰이더 참조는 **소스 수준 검사**이며 동작 테스트를 뜻하지 않는다.
- 기존 이동 속도·점프·코요테/버퍼·공격 판정·HP·피격 무적·보스 이동/쿨다운 수치는 유지했다. 스토리와 `assets/concept/`는 무변경. GitHub 푸시 없음.

## Unity에서 남은 실행 검증

1. 기존 README 순서로 열어 import/C#·셰이더 컴파일 오류, Missing audio/HitFlash 경고가 없는지 확인한다. 시작 필드곡을 두 루프 이상 듣는다.
2. 헛스윙/공격 입력 연타에는 시작된 공격마다 swing만, 유효 타격에는 hit 1회가 나는지 확인한다. 무적 중 거절된 타격에는 hit/추가 히트스톱이 없어야 한다.
3. 점프·짧은 점프·코요테·점프 버퍼·착지와 낙하 즉시 복귀를 시험한다. 점프/착지음이 매 프레임 반복되지 않아야 한다.
4. x=35 아레나 진입 시 0.8초 크로스페이드, 0.45초 예고/공격 소리, 예고 중 보스 피격 취소를 확인한다. 보스곡도 두 루프 이상 듣는다.
5. 플레이어 피격의 흰색 플래시→무적 점멸, 보스 피격 시 황금 예고와의 우선순위, 이동·공격 복원과 넉백을 확인한다.
6. 플레이어 사망·체크포인트 복귀, 보스 처치 팡파레/최대 흔들림/필드곡 복귀를 확인한다. 처치 후 R로 체력·색·음악·시간 배율이 정상인지 확인한다.
7. 타격 직후 R, 창 포커스 해제, Play 종료 후 다시 Play를 시험한다. Time.timeScale이 느려진 채 남지 않고 카메라가 어긋나지 않아야 한다.
8. SFX/BGM 동시 재생의 음량, 화면 비율별 흔들림 강도, 투명 배경/좌우 flip/패럴랙스를 확인한다.

## 알려진 한계

- 이 환경에 Unity/C# 컴파일러가 없어 import·셰이더 지원·기기별 소리·실제 플레이 감각은 확인하지 못했다. 파형 검사와 디코드 성공은 청취 검증을 대신하지 않는다.
- 기존 프로젝트의 Built-in 렌더 파이프라인용 셰이더다. URP 등으로 옮길 때는 별도 대응이 필요하다.
- BGM은 8마디 프로토타입 루프이며 전환은 박자 정렬 없이 0.8초 볼륨 크로스페이드다. 모든 오디오를 미리 읽으므로 긴 음원으로 교체할 때는 메모리/스트리밍 전략을 다시 검토해야 한다.
- SFX는 12개 동시 재생 제한이며 혼합 전체에 리미터를 넣지는 않았다. 반복 타격·극단적인 볼륨 설정은 Unity에서 청취 조정해야 한다.
- 예고음은 히트스톱 시 pitch도 낮아진다. 미래에 독립적인 pause/slow-motion 시스템을 추가하면 시간 배율 소유권을 통합해야 한다.
- 기존 시트 품질/배경 경계 한계는 [BUILD-REPORT.md](BUILD-REPORT.md)에 남아 있다.

API 확인: 카메라 후처리는 [Cinemachine 3.1 CinemachineExtension](https://docs.unity.cn/Packages/com.unity.cinemachine@3.1/api/Unity.Cinemachine.CinemachineExtension.html)의 파이프라인 콜백을 사용한다. 오디오 임포트 필드는 [Unity 6000.0 AudioImporterSampleSettings](https://docs.unity.cn/6000.0/Documentation/ScriptReference/AudioImporterSampleSettings.html)를 확인했다. 문서 확인은 이 프로젝트의 컴파일 검증과 구분한다.
