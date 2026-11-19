# QA 30차 벨벳 구역 프로브 수정

## 목표

슬롯 2의 벨벳 구역 프로브가 조사 후 05번 수로에서 멈춘 원인을 찾고, 원래 판정으로 04번 경기장까지 돌아오는지 검증했다. 수정 프로브는 Unity 플레이어 단독 실행에서 429.50초에 여섯 바닥 접촉과 은실 80회를 기록하고 경기장 발코니에 착지했다.

## 변경

- 봇은 점프 위치에 접근하다가 0.2초 이상 같은 지형 옆면에 막히면 판자 상면과 일반 점프의 도달 높이를 비교한다. 머리보다 높은 판자가 목적 발판을 덮고 있으면, 발이 판자 높이에 닿는 데 걸리는 물리 틱 수와 이동 속도로 도약 지점을 다시 잡는다. 수로에서는 x=15.30까지 물러나 판자 중심 x=19.00 방향으로 뛴다. 기존 봇은 머리보다 높은 판자를 후보에서 제외했다. [이동 입력과 접촉 판정](Assets/Scripts/AutoTest/FirstDepartureBot.cs) · [도약과 공중 조향](Assets/Scripts/AutoTest/ClockTraversal.cs)
- 슬롯 2는 짧은 솔기가 없는 방에서 상부 지지 경로를 끝까지 이용한다. 최초 실측에서 짧은 솔기·경비병 준비·상부 지지의 구성은 16·40·24회였다. 프로브의 완료 조건에는 상부 지지 48회가 필요하다. 메인 봇은 기존 경비병 경로를 사용한다. [은실 동선](Assets/Scripts/AutoTest/VelvetTraversal.cs) · [슬롯 2 선택과 완료 조건](Assets/Scripts/AutoTest/VelvetDistrictProbe.cs)
- 상부 지지로 80회에 도달하면 첫 귀환 시 04번 방의 서쪽 발코니가 저장된다. 조사 후 그곳에 복귀하는 봇이 아래층 문까지 고정 발판 경로를 따라 내려가도록 기존 문 접근 함수를 사용했다. [문 선택과 입력](Assets/Scripts/AutoTest/FirstDepartureBot.cs) · [저장 바닥 선택](Assets/Scripts/Story/RepresentativeArea.cs)
- 영구 회귀 검사 6건이 30차 실패 위치에서의 봇 입력, 독립 키 입력, 도달 불가능한 턱, 원위치 점프의 모서리 충돌, 여섯 바닥 접촉, 은실 구성을 검사한다. 정적 재생기는 캡슐 머리와 판자 아래 모서리의 충돌도 처리한다. 16차 오페라 검사의 머리 높이 조건 문자열은 리프트 착지 재생으로 교체했다. 일부 레벨 검증 전용 모듈은 해당 함수 안에서 가져오도록 옮겼다. [검증 진입점](tools/validate_safe_return.py) · [프로브 재생](tools/replay_velvet_probe.js) · [실측 시작 상태](tools/run30-velvet-probe.json) · [오페라 회귀](tools/validate_opera_wave.py)

## 검증

| 확인 항목 | 결과 |
| --- | --- |
| 30차 원본 | 메인 봇 9단계 통과. 슬롯 1 SafeReturn은 258.86초에 은실 80회로 작업실에 도착했다. 슬롯 2는 301.2초에 04번 방 잔여 대상 0개였고, 335.1초에 05번 방에서 18초 무진전으로 실패했다. |
| 20ms 고정 틱 단독 재생 | 원본은 (17.010, −1.700)에서 18.20초 무진전으로 실패했다. 수정 프로브는 x=15.320에서 뛰어 226틱(4.52초), 독립 입력은 202틱(4.04초)에 01번 문을 통과했다. 둘 다 은실 80회를 유지했고 대상 활성화는 0건이었다. |
| 모서리와 높은 턱 대조 | 제자리에서 0.24초 동안 점프를 입력하면 머리가 판자 아래 모서리에 닿아 퇴적 바닥에 다시 착지했다. 높이 상한만 고친 중간 버전은 정적 재생에서 모서리 충돌 141회 후 120.12초 구역 정체로 실패했고, Unity 플레이어에서도 120.2초 정체로 실패했다. 최종본은 실제 높이의 판자를 넘었고, 상면을 일반 점프 정점보다 높인 대조군에서는 접촉 샘플 909개 동안 점프 입력이 0회였다. |
| 고정 틱 전체 재생 | 19,998틱, 모델 시간 399.96초. 여섯 바닥을 순서대로 접촉하고 02→06→04를 지나 경기장 발코니 (4, 4.05)에 착지했다. 수집 방 05→01→02→03→04의 누계는 16→40→52→64→80회였다. |
| Unity 플레이어 직접 입력 | 원본 코드가 실제로 막힌 (17.00302, −1.69500)에서 슬롯 2·체력 6·접촉 5·은실 80·활성 대상 0·잔여 7을 맞췄다. A로 x=15.443까지 물러난 뒤 D와 Space를 유지해 판자에 착지했다. 점검 부두, 방류로 바닥, 방류 부두를 거쳐 4.03초에 문 앞 (37.957, 0.005)에 섰고 F로 01번 방에 진입했다. 좌표 이동이나 진행 플래그 쓰기 없이 플레이어 입력 인터페이스에 키를 보냈다. |
| Unity 플레이어 수정 프로브 | 같은 조사 후 세이브에서 수로 진입부터 봇이 움직였다. x=17.003에서 목표 도약점이 17.20→15.30으로 바뀌었고, 0.94초에 발 (17.767, 0.607)이 판자 모서리를 넘었다. 1.11초에 (18.677, 0.325)에서 지지 접촉을 확인했고 4.633초에 01번 방으로 들어갔다. |
| Unity 플레이어 새 슬롯 2 전체 | `VelvetDistrict` 단독 실행 429.503초, `pass`, 사망 0회. 첫 방문에서 상부 문이 닫혀 있었고, 조사 후 05→01을 지난 뒤 02→06→04로 복귀했다. 여섯 번째 저장 바닥은 `suspension_clasp_floor`였다. 상부 다리·경기장 도착은 모두 참, 마지막 발은 (4.000, 4.055), 접지, 체력 6/6이었다. 은실은 짧은 솔기 16·경비병 준비 16·상부 지지 48회였다. |

정적 재생은 벨벳 첫 방부터 이동·점프·전투·은실·문 입력을 처리한다. 첫 대련 완료와 허브 조사 완료는 장면 체크포인트로 공급한다. 모델 시간에는 이 두 장면과 메뉴 조작 시간이 포함되지 않는다. [고정 틱 재생기](tools/replay_safe_return.js)

실기에는 프로젝트 지정 버전의 Windows 플레이어 6000.0.65f1을 사용했다. 원본 QA 30차 에디터는 6000.6.9f1이다. 로컬 플레이어를 임시 폴더에 복사하고 이 작업 폴더의 C#으로 게임 어셈블리를 빌드했다. 진단 실행기는 슬롯 2 프로브를 선택하고, 작업 폴더의 아트 PNG와 JSON을 읽으며, 결과를 기록한다. 패키징된 리소스에서도 현재 JSON 24개의 원문 바이트를 확인했다. 05번 단독 대조에는 원본 프로브가 자연 진행으로 만든 조사 후 저장과 30차의 누적 접촉·은실 기록을 복원했다.

**실행한 명령과 결과.** 아래에서 `$py`는 번들 Python 실행 파일, `$qa`는 이번 실행의 임시 폴더 `rea-qa30-1hyvrf`다.

| 명령 | 출력 |
| --- | --- |
| `rtk proxy $py -X utf8 -B tools/validate_safe_return.py --bot-dir "$qa/AutoTest-before"` | 원본 C#에서 19건 중 15건 통과, 4건 실패, 종료 코드 1. 같은 시작점의 독립 입력은 통과했다. |
| `rtk proxy $py -X utf8 -B tools/validate_safe_return.py` | 수정본 19건 통과, 오류 0건, 종료 코드 0. 기존 SafeReturn 검사 13건과 신규 6건이다. |
| `rtk proxy $py -X utf8 -B "$qa/run-project-levels.py" . "$qa/levels-qa30-final.json" .` | 작업 폴더의 레벨 검증 7개 묶음에서 변경 전 3,771건, 변경 후 3,777건 통과, 오류 0건. 위의 19건을 포함한다. 이 진입점은 기존 `Temp/art-fix-levels.py`가 읽고 쓰는 경로를 이번 검증에 맞게 지정한다. |
| `rtk proxy node --check tools/replay_safe_return.js`, `rtk proxy node --check tools/replay_velvet_probe.js` | 각각 종료 코드 0. |
| `rtk proxy unity open <임시 프로젝트> --editor-version 6000.6.9f1 --accept-upgrades --batchmode --until-ready` | 준비 전 종료, 코드 6. 뒤이은 `rtk proxy unity close <임시 프로젝트> --json`은 코드 0, `not_open`을 반환했다. |
| `RewindNotary.exe -batchmode -nographics -autotest -autotestProjectPath <임시 프로젝트> -logFile <실행 로그>` | 원본·수정 C# 컴파일 성공. 직접 입력, 수정 프로브 05 단독, 새 슬롯 2 전체 실행의 프로세스 종료 코드는 각각 0이었다. |

전체 실행 로그에는 `AUTOTEST pass VelvetDistrict`와 `contacts=6 doors=velvet_02_loom/velvet_06_shortcut/velvet_04_arena receipts=16/16/48`가 기록됐다. 이 실행기는 벨벳 판정 직후 종료한다. 단독 실행의 최종 JSON은 `%TEMP%/rea-qa30-1hyvrf/project/TestResults/autotest-20261119-204620/qa30-result.json`이다. `$qa/complete2-player.log`는 전체 동선, `$qa/keys-player.log`는 독립 입력, `$qa/runup-player.log`는 수정 도약 궤적을 기록했다.

## 판단 근거

정지 당시 `VelvetDistrictProbe.Drive`가 호출한 `FirstDepartureBot.Drive`는 최초 전이 플래그 때문에 은실 수집을 건너뛰었다. `Area.RouteTarget`이 비어 다음 문을 골랐고, `ClockDoor`는 `ClockPosition("outfall_quay", 38)`을 호출했다. 그 첫 오르막은 `silt_bed → inspection_dock`이었다. 프로브가 실제로 시도한 입력은 도약점으로 향하는 D였다. 상부 다리 문 탐색은 이후 02번 방에서 시작한다. [봇의 행동 선택](Assets/Scripts/AutoTest/FirstDepartureBot.cs) · [발판과 문 접근](Assets/Scripts/AutoTest/ClockTraversal.cs) · [상부 다리 진입](Assets/Scripts/AutoTest/VelvetDistrictProbe.cs)

| 가설 | 측정값과 코드 판정 |
| --- | --- |
| (a) 수로 재설계와 봇 동선 | 변경 전후 SHA-256으로 복원한 방·생성 코드에서 지형 54→16개, 카메라 폭 182→70을 확인했다. x=19의 재회 판자와 x=38의 01번 문은 같은 위치였다. 판자 서쪽의 연속 바닥 상면 y=0이 퇴적 바닥 y=−1.70으로 낮아졌다. 새 경로는 x=17.20에서 점검 부두로 수직 도약하려 했다. 판자가 x=17.003에서 접근을 막아 도약 허용치 0.14 밖에 있었다(차이 0.197). 머리는 y=0.205, 판자 상면은 y=0.32여서 원본은 측면 접촉을 제외했다. 점프 속도 12, 중력 29.43, 틱 0.02초에서 최대 상승량은 2.328, 최고 발 높이는 y=0.633이다. 판자에 닿기 전까지 발이 올라가도록 도약점을 물린 뒤 통과했다. |
| (b) 남은 7개와 활성 구역 | 경비병 4명과 반사상 3개의 활성 사각형 중 정지점을 포함한 것은 0개다. 가장 가까운 구역까지의 거리는 3.799유닛이었다. 로그의 잔여 수는 비활성 오브젝트도 센다. `RouteTarget`은 최초 전이 이후 은실 준비 대상을 선택하지 않으며 01번 방 문에는 웨이브 처치 조건이 없다. 직접 입력으로 대상을 처치하지 않고 문을 통과했다. |
| (c) 아트 교체 | 실패 좌표에서 렌더러 61개를 끄는 실험과 다른 타일로 스프라이트를 교체하는 실험을 별도로 했다. 두 실험 모두 콜라이더 27개의 경계·활성 상태 변화가 0건이었다. 실제 판자 경계는 x=17.30~20.70, y=0~0.32였다. 교체 전후 활성 대상은 0→0, 작성 데이터와 실제 지형 크기 16개·고정 지형 위치 15개의 오차는 모두 0.005유닛 이내였다. |
| (d) 메인 봇과의 상태 차이 | 최초 05번 방에서는 은실을 모으므로 솔기 시전과 경비병 교전, x=64 체크포인트 방문을 거친다. 30차 슬롯 2도 첫 방문에서는 이 바닥에 닿았다. 실패한 재방문에는 `firstTransferPending`, `investigation_complete`, `luka_lift_complete`가 참이었다. 즉시 선택한 행동은 05→01 문에 이르는 이동이다. 조사로 열리는 06번 상부 다리 입구는 02번 방에 있다. 05의 지형과 일반 적 7명은 두 방문에 공통이다. |

원본 로그의 335.1초 시점에 표시된 `idle=31.0s`는 구역 진행 타이머다. 실패 사유인 위치·상태 무진전 18초와 별도로 측정한다. 위치·상태 18초, 구역 진행 120초, 구역 제한 1,500초, 은실 요구량은 기존 코드를 그대로 사용했다. [원본 보고서](qa30-evidence/report.json) · [원본 로그](qa30-evidence/run.log) · [실패 스크린샷](qa30-evidence/017-VelvetDistrict-failure.png) · [두 진행 타이머](Assets/Scripts/AutoTest/AutoTestDriver.cs)

방 수치와 분기는 다음 코드에서 확인할 수 있다. [재설계 이력](tools/level-design-pass2-extensions.json) · [방·배치·문 데이터](Assets/Resources/Story/representative.json) · [활성 사각형](Assets/Scripts/Story/StoryTerrain.cs) · [남은 대상과 경로 대상](Assets/Scripts/AutoTest/PlaytestObservations.cs) · [다음 문 선택](Assets/Scripts/Story/FirstDeparture.cs) · [판자와 이미지 생성](Assets/Scripts/Story/RepresentativeRoomLayout.cs)

반영 매니페스트는 상대경로 12개를 NUL로 구분하고 마지막 경로 뒤에도 NUL을 넣었다. 경로 중복 0개, 누락 파일 0개, `Packages`·`ProjectSettings` 경로 0개였다. 패키지 매니페스트와 잠금 파일의 SHA-256은 작업 전후 일치했다. [반영 매니페스트](push-paths-qa30-fix.bin)

## 남은 일

전체 정적 기준 파일에는 12,285건 통과, 오류 0건, 경고 25건이 기록되어 있다. 이 복사본에는 상위 저장소의 `tools/localization_followup.py`, `tools/story-link-extensions.json`, `assets/game/source/protected-files.sha256.json`이 빠져 전체 명령을 끝까지 재실행할 수 없었다. [제공된 정적 기준 파일](STATIC-VALIDATION.json)

배치 에디터는 준비 전에 종료했다. 프로젝트 지정 버전 6000.0.65f1 에디터가 설치되어 있지 않아 임시 프로젝트에서 설치된 6000.6.9f1로 열었다. 로그에 배치 계정의 에디터 권한 부재와 Package Manager IPC 시작 실패가 기록되었다. `unity close` 결과는 `not_open`이었다. 같은 에디터 버전에서의 수정본 실측이 남아 있다.
