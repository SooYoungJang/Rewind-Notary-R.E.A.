# 시계탑 02방 정지 수정 · 10차 실행

**동쪽 걸쇠로 올라가던 봇이 착지 전에 점프 키를 놓았다.** 10차 실행(`343565d0`)에서 가리킨 표적은 `clock_02_pendulum:sword_latch_0` (38, 8.25)이다. 사람은 아래 고정 바닥에서 일반 점프 한 번으로 닿는다. 봇의 전투 전환 조건을 목표 발판의 실제 접촉으로 바꾼 뒤, 같은 출발점의 정적 재생에서 착지와 절단이 각각 **0/1 → 1/1**이 됐다. 통합 정적 검증은 **7,809건 통과·0건 실패**다. 수정 후 Unity 실행은 이 환경에 에디터·플레이어가 없어 미측정이다.

## 실패 화면이 가리킨 표적

10차 결과는 시계탑 진입 10.700초에 01방 복귀점을 기록했다. 마지막 방은 02, 행은 `story.clock.route.02`, 행동은 `clear:wave`였다. 1,500.009초·89,992프레임 뒤 발 좌표는 (36.35964, 7.54123), 접지와 안전 바닥은 거짓이었다. HP 6, 사망·피격 0회, 기본검 발동 2회·유효 사용 1회, 진자 0회였다. [10차 결과](../../testresults/run10/report.json#L1599) · [시계탑 시작과 실패 로그](../../testresults/run10/run.log#L2191) · [실패 화면](../../testresults/run10/screenshots/018-ClockDistrict-failure.png)

| 02방 걸쇠 | 배치 좌표 | 지지면과 발 높이 | 종료 x에서 HUD 방향·거리 |
|---|---:|---|---|
| `sword_latch_0` | (38, 8.25) | 입장 바닥 바로 위의 고정 단방향 발판 `east_shoe`, x=29~42, 상단 8.20 | 오른쪽 1.64036을 올림해 **▶ 2** |
| `sword_latch_1` | (12, 11.50) | 고정 단방향 발판 `pendulum_walk`, x=6~22, 상단 11.45 | 왼쪽 24.35964를 올림해 **◀ 25** |

화면의 **‘남은 1’은 선택된 걸쇠의 남은 마디**다. HUD와 봇은 생성 순서대로 `roomTargets`에서 처음 만나는 미소탕 표적을 고른다. 02방의 생성 순서는 동쪽 걸쇠, 서쪽 걸쇠, 파수병 셋이다. 웨이브 시작 시 총 5개체·11마디이며, 걸쇠 둘은 각각 1마디다. 슬롯 3의 유일한 기본검 유효 사용은 01방의 필수 걸쇠를 여는 데 쓰였고, 02방에서는 첫 걸쇠부터 막혔다. 기존 종료 덤프의 빈 `target`은 공용 `RouteTarget`만 조회한 결과였다. [배치·활성 영역](Assets/Resources/Story/clock.json#L364) · [생성 순서](Assets/Scripts/Story/RepresentativeRoomLayout.cs#L162) · [HUD 표적 선택](Assets/Scripts/Story/DistrictRoomLayout.cs#L336) · [마디 표시](Assets/Scripts/Waves/WaveTarget2D.cs#L12) · [방향·올림](Assets/Scripts/Story/RepresentativeRoomLayout.cs#L301) · [01방 필수 걸쇠와 02방 행](Assets/Resources/Story/clock.json#L2557)

표적 탐색에는 활성 여부나 층 필터가 없다. 동쪽 걸쇠는 발이 x=33.5~42.5, y=7.96~9.00에 들어오면 활성화되고, 그 밖에서는 검 충돌 목록에서 빠진다. 종료 좌표에서는 y가 낮아 걸쇠 그림이 꺼져도 봇과 HUD가 계속 그 ID를 찾는다. 게임은 `roomTargets.TrueForAll(t => t.State.Cleared)`와 안전 상태를 요구하고, 이 행에서는 x≥35도 검사한다. [탐색](Assets/Scripts/AutoTest/PlaytestObservations.cs#L18) · [활성 영역 적용](Assets/Scripts/Story/StoryTerrain.cs#L151) · [검 충돌 등록](Assets/Scripts/Waves/WaveTarget2D.cs#L103) · [소탕 완료 조건](Assets/Scripts/Story/DistrictRoomLayout.cs#L263)

## 점프가 끊긴 경로와 사람의 도달 가능성

봇은 입장점 (20, 6.65)에서 `drum_floor`와 `east_shoe`가 겹치는 구간 [29, 42]의 중앙 x=35.50으로 걸어가 Space를 누른다. 수정 전 `ClockFight`는 `!AtClockHeight(face)`일 때만 이동을 계속했다. 발이 목표면 상단의 ±0.45 안에 들어오면 공중에서도 `FightTarget`으로 넘어갔다. 이 함수는 x=38을 향해 걷거나 J를 누른다. 공중에서는 Space를 유지하는 코드가 없어 키 해제가 발생하고, 컨트롤러가 다음 물리 틱에 양의 수직 속도를 절반으로 줄였다. 다시 아래 바닥에 떨어지면 봇은 같은 겹침 구간으로 돌아갔다. [발판 선택·도약](Assets/Scripts/AutoTest/ClockTraversal.cs#L60) · [전투 접근](Assets/Scripts/AutoTest/FirstDepartureBot.cs#L205) · [프레임 사이의 키 해제](Assets/Scripts/AutoTest/BotKeyboard.cs#L34) · [짧은 점프 처리](Assets/Scripts/PlayerController2D.cs#L142)

초속 12, 중력 29.43에서 점프를 유지한 사람의 연속 모델 상승량은 12²/(2×29.43) = **2.44648**이다. 프로젝트의 0.02초 물리 틱에서 속도에 중력을 먼저 적용하면 상승량은 **2.32788**이다. 필요한 단차는 **1.60**이다. 입장 바닥부터 축머리까지의 상단은 차례로 6.60, 8.20, 9.80, 11.45, 13.15, 14.85이며, 인접 고정면끼리 모두 겹친다. 서쪽 걸쇠까지도 x=35.50 → 31 → 20에서 세 번 뛰면 된다. 3.6초 주기의 `swing_carrier`가 차지하는 전체 범위는 x=5~12.2, 상단 y=8.1~9.5다. 동쪽 걸쇠로 가는 경로는 어느 위상에서도 이 판을 지나지 않는다. [고정면과 이동판 궤도 데이터](Assets/Resources/Story/clock.json#L289) · [이동판 왕복식](Assets/Scripts/Story/StoryMovingSurface.cs#L21) · [점프 초속](Assets/Scripts/PlayerController2D.cs#L12) · [물리 틱](ProjectSettings/TimeManager.asset#L6)

수정한 `ClockFight`는 도약 입력이 끝나고 `Area.StandingOn(face)`가 참이 될 때 전투 입력을 시작한다. 이 검사는 접지, 지정된 고정 콜라이더와의 접촉, 발과 상단의 높이 차 ≤0.12를 모두 확인한다. 따라서 상승 중에는 기존 이동기가 Space를 유지하고, 착지 후에는 일반 조준·검·패링·기술 입력을 그대로 사용한다. 시계탑 전투 표적 공통 경로에 적용했다. [전투 전환](Assets/Scripts/AutoTest/ClockTraversal.cs#L79) · [실제 지지면 검사](Assets/Scripts/Story/StoryTerrain.cs#L116)

## 수정 전후 재현

통합 검사에는 같은 x=35.50, 발 y=6.60, 수평 속도 0에서 시작하는 한 번의 도약을 넣었다. 0.02초마다 입력과 물리를 갱신한다. 배치 순서·발판·활성 영역은 방 JSON에서, 전투 전환 분기·검의 지연·범위는 코드에서 읽는다. 검은 0.16초 뒤 실제로 겹치는 위치에서만 절단으로 센다. 아래 시간과 좌표는 정적 모델의 값이다. [재현 코드](tools/validate_level_step3.py#L260) · [검 판정 상자](Assets/Scripts/CombatMotor2D.cs#L233) · [재현 결과](STATIC-VALIDATION.json)

| 동쪽 걸쇠 재현 항목 | 수정 전 | 수정 후 |
|---|---:|---:|
| 상승 중 Space 해제 | 0.12초, 발 y=7.792788, 수직 속도 8.4684 → 4.2342 | 0회 |
| 최고 발 높이 | 8.05596 (상단보다 0.14404 낮음) | 8.92788 |
| `east_shoe` 착지 | 0회 | 1회, 도약 후 0.64초 |
| 기본검 유효 절단 / 남은 마디 | 0 / 1 | 1 / 0, 타격 때 발 (36.93, 8.20) |

출발·착지의 발 오프셋을 +0.01과 +0.05로 바꿔도 두 경우 모두 절단 0 → 1이었다. 서쪽 걸쇠의 마지막 단차도 별도로 검사했다. x=20에서 `cradle` 상단 9.80을 떠났을 때, 이전 분기의 최고점은 11.37596으로 상단 11.45에 못 미쳤다. 수정 후 0.62초에 착지하고 발 (13.11, 11.45)에서 서향 검으로 마디 하나를 끊었다.

입장점 x=20부터는 수정 전후 C# 이동·전투 메서드의 분기를 JavaScript로 옮겨 입력 프레임과 50Hz 물리 틱을 따로 재생했다. 고정면 상단에 발을 놓고 입력을 60Hz로 샘플링한 경우, 기존은 **120초간 점프 155회·상층 착지 0회·검 입력 1회·절단 0회**, 수정 후는 **3.367초에 점프 1회·착지 1회·검 입력 1회·절단 1회**였다. 50Hz 입력 및 두 발 오프셋을 조합한 총 6조건에서도 절단 성공은 0/6 → 6/6이었다. 이 재생은 동쪽 걸쇠까지의 단방향 고정면과 첫 검을 다룬다. 10차 로그에 개별 입력 프레임은 없으며, 수정 후 02방 전체 전투 및 시계탑 완주 시간은 Unity 실측이 필요하다.

## 두 구역 probe의 관측과 조기 실패

벨벳과 시계탑 모두 약 **30초마다** `run.log`에 구역 경과·무진전 시간, 방, 발 좌표, 접지, 현재 행·행동, 남은 웨이브 개체 수, 조회한 웨이브 표적 ID·그 표적의 마디 수, 누적 복귀점 접촉 수를 기록한다. 개체 수는 방의 전체 전투 배치와 벨벳 입구 대기 웨이브에서 소탕된 개체를 뺀 값이다. 진자 정거장 사용은 별도 `stationUses`로 집계한다. 실패 사유에도 같은 상태가 들어가므로 18초 판정이 첫 주기 로그보다 먼저 발생해도 `run.log`와 `report.json`에 상태가 남는다. [진단 상태와 타이머](Assets/Scripts/AutoTest/AutoTestDriver.cs#L173) · [웨이브 집계](Assets/Scripts/AutoTest/PlaytestObservations.cs#L21) · [실패 기록](Assets/Scripts/AutoTest/AutoTestDriver.cs#L305)

복귀점 접촉 누적 증가, 같은 방의 처치 수 증가, 실제 표적 상태를 바꾼 `valid_use`가 타이머를 갱신한다. **120초**가 지나면 다음 상태 샘플에서 기존 실패 처리로 들어가 스크린샷·종료 상태·실패 이유를 저장한다. 위치 왕복이나 헛검은 이 시간을 연장하지 않는다. 두 구역의 기존 18초 위치·상태 검사와 각각의 1,500초 전체 제한도 유지했다. [이벤트 승인 조건](Assets/Scripts/Progression/SkillProgressionService.cs#L72) · [두 시간 제한](Assets/Scripts/AutoTest/AutoTestDriver.cs#L60)

두 구역의 시간 분기에 0.25초 간격으로 합성 상태를 입력했다. 위치를 계속 바꾸고 59초에 거절된 사용, 119초에 방 변경을 주면 양쪽 모두 30·60·90초에 로그를 내고 **120초에 실패**했다. 별도 3조건에서는 100초에 접촉·처치·유효 사용 중 하나를 주었고, 양쪽 모두 마지막 진전으로부터 120초 뒤인 **220초에 실패**했다.

## 검증과 변경 파일

작업공간 루트에서 `python3 -B tools/validate_gamedev.py --output unity/RewindNotary/STATIC-VALIDATION.json`을 실행했다. 수정 전 기준선 7,798건의 검사명과 중복 횟수를 전부 보존했다. 11건을 추가해 **7,809건 통과·실패 0건**, 기존 시계탑 변조 검출 **130/130건**이었다. 스토리 JSON 6개의 수정 전후 SHA-256이 모두 같고, 321개 원본 행 검사가 통과했다. 접촉 순서 01→02→03→04→05→06→07→08→10→09, 진자 30회·80회, 웨이브 전소탕 조건은 기존 코드를 사용한다. [접촉·진자 판정](Assets/Scripts/AutoTest/ClockDistrictProbe.cs#L27) · [전체 정적 결과](STATIC-VALIDATION.json)

| Unity 저장소 상대 경로 | 변경 내용 |
|---|---|
| `Assets/Scripts/AutoTest/ClockTraversal.cs` | 목표 고정면 착지 후 전투 입력 전환 |
| `Assets/Scripts/AutoTest/PlaytestObservations.cs` | 구역에 맞는 웨이브 표적 조회, 전체·소탕 개체 수 조회 |
| `Assets/Scripts/AutoTest/FirstDepartureBot.cs` | 종료 상태에 시계탑 웨이브의 표적 ID와 마디 기록 |
| `Assets/Scripts/AutoTest/AutoTestDriver.cs` | 두 구역의 주기 로그, 120초 조기 실패, 실패 시 상태 기록 |
| `tools/validate_level_step3.py` | 두 걸쇠의 착지·절단 재현 및 진단 경로 검사 |
| `STATIC-VALIDATION.json` | 통합 검사 목록·통과 수와 걸쇠 재현 수치 |
| `LEVEL-STEP3-FIX2-REPORT.md` | 10차 증거, 원인, 수정 및 검증 범위 |

작업공간 루트의 `push-paths-level-step3-fix2.bin`에는 위 7개 상대 경로만 담았다. 구분자와 마지막 바이트는 NUL이다. 작업공간의 `tools/validate_level_step3.py`는 표의 도구와 하드링크를 공유한다.
