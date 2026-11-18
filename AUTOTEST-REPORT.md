# 자동 플레이테스트 하네스

빈 슬롯에서 시작해 제2구역 첫 방까지 가는 9단계를 에디터와 스탠드얼론이 공유한다. 봇은 화면에 표시할 대상, 방 상태, 전투 예고를 읽고 가상 키를 누른다. 마지막 판정에는 임시 슬롯에 실제로 저장된 제2구역 도착 기록도 포함한다. **정적 검증은 7,107건 통과, 실패 0건이다. Unity 실기 결과와 FPS 측정값은 아직 없다.**

## 실행

Unity 버전은 **6000.0.65f1**이다. 아래 `<클론>`은 `Assets`, `Packages`, `ProjectSettings`가 들어 있는 디렉터리다. 이 작업공간에서는 `unity/RewindNotary`이고, Unity 프로젝트 자체를 클론한 PC에서는 그 클론의 최상위다. 에디터 실행 중 같은 프로젝트를 열고 있다면 닫는다. PNG를 얻으려면 SSH가 시작하는 Unity 프로세스가 그래픽 장치를 사용하고 화면을 렌더링할 수 있어야 한다.

### 에디터에서 Main 한 판

```text
Unity -batchmode -projectPath "<클론>" -executeMethod AutoTestRunner.Run -quit
```

Windows PowerShell에서 공백이 있는 경로와 GUI 프로세스의 종료 코드를 처리하는 예다.

```powershell
$project = (Resolve-Path '.').Path  # Unity 프로젝트 루트에서 실행
$unity = "$env:ProgramFiles\Unity\Hub\Editor\6000.0.65f1\Editor\Unity.exe"
$cli = '-batchmode -projectPath "{0}" -executeMethod AutoTestRunner.Run -quit' -f $project
$run = Start-Process -FilePath $unity -ArgumentList $cli -Wait -PassThru
exit $run.ExitCode
```

`-quit`가 비동기 Play 진입보다 먼저 에디터를 닫을 수 있다. 이를 처리하기 위해 호출한 에디터가 결과 디렉터리를 먼저 예약하고, 임시 프로젝트에 `Assets`, `Packages`, `ProjectSettings`를 복사한다. 같은 Unity 실행 파일로 연 두 번째 에디터가 Game 뷰에서 Main을 Play한다. 부모는 자식 종료와 최종 JSON을 확인하고 같은 성공·실패 코드를 반환한다. 임포트와 Play를 합쳐 45분이 지나면 자식을 종료하고 원본 프로젝트의 결과 디렉터리에 실패를 기록한다. 에디터 두 개분의 메모리와 임시 임포트 공간이 필요하다. [에디터 진입점](Assets/Editor/AutoTestRunner.cs) · [Unity 6.0 명령줄과 quit 동작](https://docs.unity.com/en-us/engine/6000.0/manual/unity-editor/command-line-arguments/editor)

### 스탠드얼론 성능 측정

기존 Windows64 또는 macOS 빌드의 플레이어에 `-autotest`를 전달한다. `StandaloneBuild`의 `build-result.json`에는 실행 파일인 `playerPath`와 테스트 인자 배열인 `autotestArguments`가 함께 기록된다. 일반 빌드 옵션과 Main 씬을 그대로 사용한다. [빌드 진입점](Assets/Editor/StandaloneBuild.cs) · [기존 빌드 러너](BUILD-PIPELINE-REPORT.md)

Unity 프로젝트 클론에서 Windows64를 직접 빌드하고 한 판을 실행하는 PowerShell 예다. `$project`와 `$unity`는 위와 같이 지정한다. 빌드 진입점은 프로젝트의 통과한 `STATIC-VALIDATION.json`을 요구한다.

```powershell
$id = 'playtest-' + [guid]::NewGuid().ToString('N')
$cli = '-batchmode -quit -projectPath "{0}" -buildTarget win64 -executeMethod RewindNotary.Editor.StandaloneBuild.BuildWindows64 -rewindBuildId {1}' -f $project, $id
$build = Start-Process -FilePath $unity -ArgumentList $cli -Wait -PassThru
if ($build.ExitCode -ne 0) { exit $build.ExitCode }
$receipt = Get-Content -Raw -Encoding UTF8 "$project\Builds\Windows64\$id\build-result.json" | ConvertFrom-Json
$cli = ($receipt.autotestArguments | ForEach-Object { '"{0}"' -f $_ }) -join ' '
$run = Start-Process -FilePath $receipt.playerPath -ArgumentList $cli -Wait -PassThru
exit $run.ExitCode
```

이미 빌드된 게임은 `RewindNotary.exe -autotest`로 실행할 수 있다. macOS에서는 `RewindNotary.app/Contents/MacOS/` 안의 플레이어 실행 파일에 같은 인자를 준다. 플레이어가 프로젝트의 `Builds` 아래에 있으면 프로젝트 루트를 자동으로 찾는다. 빌드를 옮긴 경우 `-autotestProjectPath "<클론>"`으로 결과를 받을 프로젝트를 지정한다.

## 상태 기계와 판정

| 단계 | 통과 증거 | 절대 제한 |
|---|---|---:|
| NewSlot | 새 게임 메뉴에서 실제로 빈 1번 슬롯 선택. 다락방, 기본검 유효 120회, 은실 미보유 | 45초 |
| Attic | 사탕을 주워 접시에 놓음, 위탁 사본 단서 취득, 여섯째 자리 날인 | 100초 |
| Velvet | 대장간과 승강장을 지나 초입 웨이브 완료. 직조틀·화랑 다리 개방 후 벨벳 4번 방 도착 | 240초 |
| Mikhail | 활성 보스 등장과 실제 시도 관측, 승리 후 `mikhail_defeated` 저장 성공 | 180초 |
| Silver | `story:mikhail_bout` 영수증과 은실 획득 출처 일치. 획득 대사 완료 및 5번 방 귀환로 도착 | 45초 |
| SafeReturn | 중갑병에 중립 Q와 검격을 쓰며 방을 순환. 은실 유효 80회 이상·3단계, 첫 전이 확인. 고정 바닥에서 F로 호출에 응해 작업실 도착 | 600초 |
| Investigation | 장부·파형 단서 2건, 계측 4단계, `investigation_complete`로 I=1 확인 | 180초 |
| Lift | 구호소 첫 지원, 루카 신호와 발판 탑승, 도착 바닥 저장. 같은 슬롯의 선택 화면에서 커서가 `clock`에 위치 | 150초 |
| DistrictTwo | Enter로 진입한 슬롯 1의 구역이 `clock`, 실제 방이 `clock_01_entry`. 조사·승강장 영수증 유지 | 45초 |

`FirstDepartureBot`은 다중 접점 행의 현재 동작, 실제 문의 해금 조건, 현재 웨이브 표적, 보스의 남은 예고 시간을 읽는다. 방 이동·부활·기술 횟수·플래그는 게임의 입력 처리와 저장 코드가 결정한다. 메뉴가 시간을 멈춘 동안에도 감시는 실시간으로 계속된다. 0.25초마다 위치와 진행 상태를 관측해 18초간 변화가 없으면 현재 단계를 실패로 닫는다. 제자리 왕복처럼 움직임만 계속되는 경우에는 표의 절대 제한이 적용된다. 이후 단계는 앞 단계의 저장 증거에 의존하므로 `skipped`로 남긴다. [한 판 흐름 원본](FLOW-ASSEMBLY-REPORT.md) · [봇과 단계 판정](Assets/Scripts/AutoTest/FirstDepartureBot.cs) · [측정과 막힘 감지](Assets/Scripts/AutoTest/AutoTestDriver.cs)

## 결과 읽기

출력은 `<클론>/TestResults/autotest-yyyyMMdd-HHmmss/`다. UTC 초 단위로 이름을 예약하며 같은 초의 실행이 겹치면 다음 빈 초를 사용한다. 자동 분석은 프로세스 종료 후 `report.json`의 `status`와 `exitCode`를 읽는다. 실행 중에도 단계 경계에서 JSON을 원자적으로 교체한다.

- **종료 0**은 전 단계 통과, 필수 PNG 기록, 오류 0건, 실제 세이브 지문 일치, 임시 디스크 슬롯의 최종 도착 확인 및 임시 세이브 삭제를 뜻한다.
- **종료 1**이면 `steps`의 첫 `fail`에서 `reason`과 `endState`를 확인한다. 엔진 오류·캡처 누락·세이브 감사·에디터 시작 실패는 `errors`에서도 확인한다.
- **run.log**에는 하네스 실행 중 Unity가 전달한 모든 로그 유형, 스택, 단계 시작과 판정을 기록한다. 에디터 모드는 임포트부터 종료까지의 자식 에디터 원본 로그도 뒤에 붙인다.
- **screenshots/*.png**는 단계 시작, 막힌 프레임, 보스 시도별 시작과 종료 시점의 Game 화면이다. 같은 프레임의 체크포인트는 한 파일을 공유한다. 마지막에는 최대 10초 동안 PNG 헤더와 끝 청크 기록을 기다린다. [Unity ScreenCapture](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ScreenCapture.CaptureScreenshot.html)

### report.json 스키마 v1

| 위치 | 타입과 의미 |
|---|---|
| `schemaVersion`, `mode`, `unityVersion` | 정수 1, `editor` 또는 `standalone`, 실행 엔진 버전 |
| `status`, `exitCode`, `startedUtc`, `endedUtc` | `running/pass/fail`, 정수 0 또는 1, ISO 8601 UTC 시각. 실행 중에는 종료 시각이 비어 있고 코드는 1 |
| `steps[]` | 순서가 있는 9개 객체. `id`, `expected`, `status`(`running/pass/fail/skipped`), `reason`, `seconds`(실시간 구간 소요), `deaths`, `hits`(실제 피격 이벤트 수) |
| `steps[].skills[]` | `id`, `activations`(승인된 사용 컨텍스트 생성), `validUses`(실제 유효 집계 이벤트). 기본검 한 연격은 한 컨텍스트다. 사용 기록이 없는 기술은 생략 |
| `steps[].performance` | `frames`, `meanMs`, `meanFps`, `slowest1PercentMeanMs`, `onePercentLowFps`. 연속 Update 시작 간 실시간 간격을 정렬해 가장 긴 ⌈N×0.01⌉개를 평균한다. 각각의 FPS는 1000을 해당 평균 ms로 나눈 값. 표본이 없으면 0 |
| `steps[].endState` | 구간 종료·실패 덤프. `page/menuIndex/message`, `slot/stage/room/row/action`, `x/y/grounded/safeFloor`, `hp/bossHp`, `target/targetSegments`, `silverOwned/silverUses`, `flags[]/encounters[]/saveError`. 없는 대상의 HP·잔여 칸은 −1. 시작 전 단계에는 종료 관측값이 없다 |
| `errors[]` | `utc/type/message/stack`. Unity의 `Error/Exception/Assert`와 하네스의 저장·캡처·실행 오류. 일반 로그·경고는 run.log에 기록 |
| `checkpoints[]` | `step/kind/png/seconds`. 종류는 `start`, `failure`, `boss-start`, `boss-end-<결과>`. PNG 경로는 실행 디렉터리 기준. 시간은 드라이버 시작 후 경과 초 |
| `loads[]` | `kind/id/ms`. `scene`은 첫 씬 로드 직전 훅부터 `sceneLoaded`까지, `stage`는 StartStage의 월드 생성 구간, `room`은 Restore의 체크포인트 해석·방 조립·배치 구간이다. 스테이지 측정에 첫 방 조립이 포함되므로 각 값을 따로 비교한다 |
| `bossAttemptCount/bossSeconds/bossAttempts[]` | 실제 시도 수와 전투 구간 합계 초. 시도마다 `startSeconds/seconds/outcome`을 기록한다. 결과는 `running/victory/retry/interrupted/aborted` |
| `isolation` | `realRoot/temporaryRoot/observedSessionRoot`, `beforeSha256/afterSha256`, 불리언 `observedSlotWrite/verifiedFinalSlot/unchanged/tempRemoved` |

단계 시간에는 대사와 해당 구간의 캡처·저장 비용이 포함된다. 보스 시간은 활성 대련 상대가 관측된 시점부터 승리·재시도·중단까지다. 에디터 임포트 시간은 포함되지 않는다. 프레임 통계는 렌더링을 켠 스탠드얼론끼리 비교한다. [직렬화 필드와 프레임 계산](Assets/Scripts/AutoTest/AutoTestReport.cs)

## 세이브 격리

첫 씬이 열리기 전에 OS 임시 디렉터리 아래에 실행별 GUID 디렉터리를 만든다. 테스트가 요청됐는데 격리 루트를 만들지 못하면 초기화가 실패한다. 타이틀의 설정 로드·복구, 구형 슬롯 이관, 슬롯 조회, 세이브 v4 본문·백업·디버그·엔딩 파일, 기술 사용 로그가 전부 이 루트를 사용한다. `observedSessionRoot`에는 게임 세션이 실제로 연 `<temporaryRoot>/SkillSystem` 경로를 기록한다.

시작과 종료 시 실제 `SkillSystem` 파일들과 `settings.json*`을 읽어 상대 이름·최종 쓰기 시각·내용 해시의 SHA-256 지문을 비교한다. 기본 Unity 로그는 이 지문의 범위 밖이다. 종료 전에 남은 변경 사항을 저장하고 세션을 닫는다. 성공한 시나리오는 임시 `slot-1.json`을 다시 읽어 v4·슬롯 1·제2구역·은실 80회·영수증을 확인한 뒤 임시 루트를 삭제한다. 네이티브 프로세스가 강제로 종료된 경우에는 마지막으로 발행된 JSON만 남을 수 있으며, `tempRemoved`와 `unchanged`로 감사 완료 여부를 판별한다. [부트스트랩과 세이브 감사](Assets/Scripts/AutoTest/AutoTestRuntime.cs) · [셸의 저장 루트](Assets/Scripts/Shell/GameShell.cs)

프로젝트의 `.gitignore`에는 `TestResults` 제외 규칙이 없다. 추가 제안은 루트 기준 **`/TestResults/`** 한 줄이다. 이번 변경에서 `.gitignore`를 편집하지 않았다.

## 시나리오 확장

1. `PlaytestStep`에 순서를 추가하고 `Expectations`, `Limits`, `Passed`에 기대 상태와 제한 시간을 함께 추가한다. 저장 성공, 실제 방 ID, 영수증처럼 입력 결과를 확인할 수 있는 값을 판정에 쓴다.
2. 기존 행의 조사·검격·발판 접촉은 현재 행과 좌표를 재사용한다. 새 동작이 필요하면 `Drive`에 추가한다. `BotKeyboard.Hold/Tap`은 프레임별 유지·누름·해제를 제공한다. 예를 들어 `Cast(vertical: -1)`은 아래 방향과 Q를 같은 프레임에 공급하고 기존 방향 판정기가 접지 여부에 따라 기술을 선택한다. [입력 장치](Assets/Scripts/Shell/GameInputDevice.cs) · [가상 키보드](Assets/Scripts/AutoTest/BotKeyboard.cs)
3. 새로운 관측값은 읽기 전용 속성이나 기존 이벤트로 연결한다. 구간 완료 검사가 요구하는 상태를 봇이 직접 쓰면 검증 대상이 사라진다. 검사기의 단계 목록·목표·시간 제한도 함께 갱신한다. [읽기 전용 관측](Assets/Scripts/AutoTest/PlaytestObservations.cs) · [하네스 정적 검사](../../tools/validate_autotest.py)

## 검증과 전달

전체 작업공간 루트에서 다음 명령을 실행했다.

```bash
python3 -B tools/validate_gamedev.py --output unity/RewindNotary/STATIC-VALIDATION.json
```

| 검사 | 결과 |
|---|---:|
| 수정 전 기준선 | 7,037 통과, 0 실패 |
| 수정 후 전체 | 7,107 통과, 0 실패, 종료 코드 0 |
| 하네스 전용 | 44/44 |
| 기준선 대조 | 기존 통과 문구 7,037건과 기존 검사군의 지표 모두 일치 |
| 기존 입력·세이브·흐름 검사 | 373/373 · 1,589/1,589 · 25/25 |

나머지 신규 26건은 Unity 메타·GUID와 C# 구분자 검사다. 별도 메모리 변조에서는 Q 매핑, 물리 입력 폴백, 실제 설정 경로 사용, 80회 안전 바닥 조건, 정체 감시, 보스 PNG, 느린 프레임 정렬, 최종 슬롯 읽기, 봇의 직접 좌표 변경에 해당하는 9개 오류를 모두 검출했다. 이 환경에는 Unity 에디터와 C# 컴파일러가 없어 컴파일·Play Mode·스탠드얼론 완주·실제 PNG·보스 조작 타이밍은 PC 실행에서 검증해야 한다. [전체 정적 결과](STATIC-VALIDATION.json)

푸시 매니페스트에 포함된 33개 항목은 모두 **Unity 프로젝트 루트 기준 상대 경로**이며 각 항목 끝에 NUL 한 바이트가 있다. 프로젝트 안의 파일은 `Assets/…`, `Packages/…` 또는 루트 파일명이다. 프로젝트 밖의 기존 검사 도구와 목록 자체도 포함하므로 해당 항목은 `../../tools/…`와 `../../push-paths-autotest.bin`이다. Unity 프로젝트만 있는 원격 저장소에 반영할 때는 이 외부 항목의 기준 위치가 전체 작업공간이라는 점에 유의한다. [전체 NUL 매니페스트](../../push-paths-autotest.bin)
