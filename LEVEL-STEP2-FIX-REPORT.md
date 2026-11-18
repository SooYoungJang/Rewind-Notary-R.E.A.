# 레벨 패스 2단계 · SafeReturn 은실 1회 정지 수정

**판정은 봇의 이동·방향 전환 회귀다.** 2단계 하층 수로의 두 번째 솔기는 x=8.60이다. 기존 봇은 x=6.90 부근에 서서 동쪽을 봐야 Q를 눌렀다. 7차 실패 위치에서는 동쪽으로 돌아서는 한 걸음이 그 허용 범위를 벗어났고, 다음 입력이 곧바로 서쪽으로 바뀌었다. 이번 게임 실행 코드의 수정 범위는 자동 플레이의 `VelvetTraversal.cs` 한 파일이다.

## 7차 실측과 판정 경로

PC QA 6차에서는 SafeReturn이 은실 유효 사용 80회로 `hub_workroom`에 도착했다. 7차(`23e96e8f`, Unity 6000.6.9f1)에서는 `Silver` 종료 시 x=2.0·0회, `SafeReturn` 종료 시 x=6.9064002, y=0.005·1회였다. 발동 1회와 유효 사용 1회가 일치했고, 종료 시 체력은 6/6, 지면 접촉과 안전 바닥은 모두 참이었다. 시작·실패 스크린샷의 HUD는 각각 0/80과 1/80이며, 실패 화면에서 레아는 왼쪽을 본다. 근거는 작업공간의 `testresults/run6/report.json`, `testresults/run7/report.json`, `testresults/run7/run.log` 631~637행, 같은 실행의 `screenshots/012-SafeReturn-start.png`와 `013-SafeReturn-failure.png`다.

1. **솔기 선택.** `FirstDepartureBot.Drive`는 은실을 획득했고 첫 양도가 대기 중이 아니면 `SewReturnRoute()`를 먼저 호출한다. 하층 16개는 첫 `seamRuns`의 `x = 3.8 + i × 4.8`, y=0.24에 생성된다. `seam_0`은 x=3.80, `seam_1`은 x=8.60이다. 시작 바닥 x=2.0에서 첫 솔기까지 거리는 약 1.82로 사거리 2.6 안이고, 폭 0.9는 1단계 허용 폭 1.0 안이다. `SilverStitchSkill.Activate`가 전방 범위와 겹치는 가장 가까운 가용 솔기를 설치하면 `StitchSurface2D.Install`의 1단계 분기가 즉시 `Apply(...TargetId, true)`를 호출한다. 봇은 이 `valid_use`의 표적 ID가 현재 솔기와 일치할 때 인덱스를 올린다. [방 데이터](Assets/Resources/Story/representative.json#L1644) · [솔기 생성](Assets/Scripts/Story/RepresentativeRoomLayout.cs#L116) · [봇 호출 순서](Assets/Scripts/AutoTest/FirstDepartureBot.cs#L122) · [유효 사용 관측과 인덱스](Assets/Scripts/AutoTest/VelvetTraversal.cs#L59) · [설치·성장 판정](Assets/Scripts/Skills/StitchSurface2D.cs#L39)

2. **두 번째 Q가 사라진 지점.** 수정 전 `SewReturnRoute`는 하층에서 `Walk(x - 1.70f, .12f)`가 성공한 뒤에만 방향을 검사했다. 목표 x=6.90의 허용 범위는 [6.78, 7.02]다. 아래 좌표 중 6.9064는 실측이고, 다음 위치는 지상 속도 6.5와 고정 물리 틱 0.02초를 적용한 계산값이다. `Face(speed)`와 수평 이동이 같은 물리 틱에 일어난다. [Walk](Assets/Scripts/AutoTest/FirstDepartureBot.cs#L145) · [이동 입력](Assets/Scripts/PlayerController2D.cs#L139) · [속도와 방향 적용](Assets/Scripts/CombatMotor2D.cs#L137) · [물리 틱](ProjectSettings/TimeManager.asset#L6)

   | 판정 시 x·방향 | 수정 전 선택한 입력 | 다음 물리 틱 |
   |---|---|---|
   | 6.9064 · 서쪽 | 위치 허용. 방향이 반대여서 D를 누르고 반환 | 7.0364 · 동쪽 |
   | 7.0364 · 동쪽 | 허용 상한 7.02 초과. `Walk`가 A를 누르고 반환 | 6.9064 · 서쪽 |

   두 분기 모두 `keys.Cast()` 앞에서 반환한다. 이 간격의 변위 0.13은 무진행 감시기가 쓰는 0.5보다 작고, 방·행·유효 사용 수·적의 남은 마디도 그대로다. 로그의 종료 사유는 `no position or state progress for 18 seconds`다. [감시기 샘플과 종료 조건](Assets/Scripts/AutoTest/AutoTestDriver.cs#L125)

3. **보고된 표적과 화면의 기둥.** `Snapshot.target`은 `Area.RouteTarget`을 기록한다. 은실 수집 중에는 준비가 필요한 첫 미처리 적을 반환하므로 x=83의 첫 중갑병 `notary_guard_0`과 남은 마디 1이 나온다. 하층 솔기가 남아 있으면 봇은 솔기 분기에서 이미 반환한다. 화면 왼쪽 x=1의 청록 기둥은 `velvet_04_arena`로 가는 출입문이며, 솔기 표식은 바닥 가까이 놓인 가로 띠다. [관측용 표적](Assets/Scripts/AutoTest/PlaytestObservations.cs#L30) · [종료 상태 기록](Assets/Scripts/AutoTest/FirstDepartureBot.cs#L228) · [출입문과 적 생성](Assets/Scripts/Story/RepresentativeRoomLayout.cs#L99) · [적 배치](Assets/Resources/Story/representative.json#L1659)

## 사람이 같은 자리에서 진행할 수 있는 근거

실패 지점에서 D 한 틱 뒤 키를 놓고 Q를 누르면 동쪽을 본 채 x≈7.0364에서 시전한다. 이때 전방 범위 [7.0364, 9.6364]와 `seam_1`의 가로 범위 [8.15, 9.05]가 겹친다. 발에서 솔기 중심까지의 거리도 약 1.58로 2.6 이내다. 첫 솔기의 설치 수명은 4초이고, 이 위치는 첫 솔기를 계속 지지하는 범위 밖이다. 18초 정지 시점에는 설치 한도 한 칸이 비어 있다. 중립 Q는 은실로 해석되고, 폭 0.9의 두 번째 솔기도 첫 솔기와 같은 1단계 유효 사용 판정을 받는다. [키 매핑](Assets/Scripts/Shell/GameInput.cs#L35) · [전방 범위와 설치 한도·거리 검사](Assets/Scripts/Skills/SilverStitchSkill.cs#L25) · [은실 수치](Assets/Resources/Skills/catalog.json#L46) · [설치 만료](Assets/Scripts/Skills/StitchSurface2D.cs#L75)

한 방문의 80회 경로는 하층 짧은 솔기 16회, 중갑병 8명의 관절 준비 각 2회, 상층 긴 솔기 48개에 설치 후 착지 48회다. 30회에 은실 2단계가 열린다. 상층은 각 틈의 고정 천에서 시전할 수 있고, 설치 발판 위에서 접지와 실제 콜라이더 접촉이 확인되어야 `:support`가 기록된다. 그중 48개면 80회에 닿고 두 곳의 여유가 있다. 이후 인접 고정 바닥에서 귀환 호출을 조사한다. [방별 경로와 공급](LEVEL-STEP2-REPORT.md) · [중갑병 준비·절단](Assets/Scripts/Waves/WaveTargetState.cs#L28) · [발판 접촉](Assets/Scripts/Skills/StitchSurface2D.cs#L78) · [고정면 귀환 입력](Assets/Scripts/Story/RepresentativeArea.cs#L234)

## 수정과 검증

봇은 `run.stepX`의 부호로 진행 방향을 정하고, 그 방향으로 잰 솔기 중심까지의 거리가 **솔기 반폭보다 크고 1.85보다 작은 구간**에서 접지·방향을 확인해 중립 Q를 누른다. 너무 가깝거나 반대쪽을 보면 뒤로 물러났다가 다시 솔기를 향해 걷는다. 상층에서도 이 구간은 고정 천 위에 있다. 솔기 간격 3.4에서 1.85를 빼면 직전 설치 중심과 1.55 이상 떨어져, 이전 발판이 만료 후에도 유지되는 반경 1.50 밖에서 다음 설치를 기다린다. [수정 분기](Assets/Scripts/AutoTest/VelvetTraversal.cs#L83)

- **실패 좌표 재현.** 지상 입력 분기를 단정밀도로 재생했을 때 기존 분기는 1,155프레임 동안 위 두 좌표 안에서 Q를 0회 냈다. 수정 분기는 입력당 물리 틱 1회 조건에서 네 번째 입력에 x=6.7764·동쪽으로 Q를 냈다. 통합 검증에는 이 실패 좌표, 하층 16개·상층 50개의 양방향 시작 자세, 목표를 지나친 위치, 입력당 물리 틱 1~5회의 접지 이동을 포함했다. [접근 재현 검사](tools/validate_level_step2.py#L142)
- **정적 검증 7,516건 통과·0건 실패.** 수정 전 7,502개 검사가 모두 통과했고 14개를 추가했다. 2단계 변조 69/69건을 검출했다. 이전의 ±0.12 경유 조건 복원, 물리 한 걸음보다 좁은 상층 시전 구간, 직전 발판 유지 범위 안에서 기다리는 변조도 검출했다. 실행 명령은 전체 작업공간 루트의 `python3 tools/validate_gamedev.py --output unity/RewindNotary/STATIC-VALIDATION.json`이다. [변조 검사](tools/validate_level_step2.py#L306) · [통합 결과](STATIC-VALIDATION.json)
- **통과 조건과 보호 범위.** 18초 감시기가 계속 실행되며, SafeReturn 통과에는 은실 유효 사용 80회 이상·3단계, 고정 바닥 귀환 입력, 양도 및 귀환 플래그, `hub_workroom` 도착이 모두 필요하다. 키 입력 이후의 설치·성장·저장은 일반 플레이 경로를 거친다. 스토리 텍스트·대사·행 데이터와 저장 스키마는 원본과 동일하며 v4 저장 검사를 통과했다. [단계 통과 조건](Assets/Scripts/AutoTest/FirstDepartureBot.cs#L56) · [사용 횟수 및 첫 양도 반영](Assets/Scripts/Progression/SkillProgressionService.cs#L68)
- **실측 범위.** 7차 PC에서 feel 3종, 직조실·회랑의 고정 및 벽 경로, 실제 세이브 해시 보존과 임시 폴더 정리가 통과했다. 이번 수정 후 해당 정적 검사도 전부 통과했다. 이 작업환경에는 Unity 에디터·플레이어가 없어 수정본의 9단계 자동 플레이, 별도 구역 주행, feel·파일럿의 PC 재실측은 미실행이다.
