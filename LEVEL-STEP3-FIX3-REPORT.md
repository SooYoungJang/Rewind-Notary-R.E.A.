# 시계탑 02방 파수병 교전 정지 수정 · 11차 실행

**파수병은 한 층 아래에 있었고, 봇은 올바른 층을 찾고도 윗판에서 이동 키를 놓았다.** `shell_sentry_2`의 배치는 (23, 9.85), 지지면은 상단 y=9.80의 `cradle`이다. 11차의 발 좌표에서 기존 봇이 계산한 하강 목적지까지는 0.04602였고 정지 허용치는 0.14였다. 고정면 연결을 찾아 실제 이탈·착지까지 입력을 이어가도록 고쳤다. 같은 좌표의 정적 재생 세 조건에서 세 마디 소탕은 **0/3 → 3/3**, 전체 정적 검증은 **7,837건 통과·0건 실패**다. 수정 후 Unity 완주는 미측정이다.

## 11차에서 멈춘 분기

| 실측 시점 | 상태 |
|---|---|
| 구역 경과 30.1초 | 02방, 발 (16.99, 11.56), 잔여 4개체. 서쪽 걸쇠 `sword_latch_1` 1마디를 추적 중이었다. |
| 구역 경과 51.043초 | 같은 방, 발 (22.953978, 11.559998), 접지·안전 바닥 참. 걸쇠 둘을 끊고 파수병 3개체가 남았다. 첫 파수병은 3마디 그대로였다. |
| 실패 판정 | 위치·상태 무진전 18초 초과. 종료 로그의 `idle=19.9s`는 마지막 구역 진전 이후의 별도 시계다. 구역 누적 기본검 발동·유효 사용은 각 3회, 피격 2회, HP 4/6, 사망 0회였다. |

화면 하단의 ◆는 파수병과 수평 좌표가 거의 같음을 나타낸다. 웨이브 생성기는 걸쇠 2개 뒤에 파수병 3개를 배치한다. HUD와 봇은 활성 여부와 관계없이 첫 미소탕 개체를 선택한다. 파수병의 활성 영역은 x=18.5~27.5, y=9.56~10.60이다. 실패 좌표는 x만 이 범위에 든다. 이때 파수병의 그림과 타격 대상 등록이 함께 꺼져도 봇은 해당 개체의 Transform을 읽을 수 있다. [11차 로그](../../testresults/run11/run.log#L2266) · [종료 상태](../../testresults/run11/report.json#L1599) · [실패 화면](../../testresults/run11/screenshots/018-ClockDistrict-failure.png) · [배치·활성 영역](Assets/Resources/Story/clock.json#L386) · [생성 순서](Assets/Scripts/Story/RepresentativeRoomLayout.cs#L162) · [활성 처리](Assets/Scripts/Story/StoryTerrain.cs#L151) · [타격 등록](Assets/Scripts/Waves/WaveTarget2D.cs#L103)

1. 11차의 `ClockFight`는 비활성 파수병의 (23, 9.85)를 `FaceAt`에 넘겼다. x=18~33, 상단 9.80인 `cradle`이 높이 차 0.05로 선택된다. 봇이 서 있던 `pendulum_walk`의 작성 범위는 x=6~22, 상단 11.45다. `StandingOn("cradle")`가 거짓이므로 매 프레임 `ClockPosition`으로 들어갔다. 여기에는 표적이 활성화되기를 기다리는 분기가 없다. [11차 교전 코드](https://github.com/SooYoungJang/Rewind-Notary-R.E.A./blob/65787ebf/Assets/Scripts/AutoTest/ClockTraversal.cs#L69) · [두 고정면](Assets/Resources/Story/clock.json#L308)
2. 기존 이동기는 지형 배열의 인덱스 3에서 2로 내려갈 때 바로 앞 항목만 검사했다. 아래판이 오른쪽으로 더 길어서 목적지를 윗판의 작성 오른쪽 끝에 1을 더한 **x=23**으로 잡았다. `MoveTo(23)`은 거리 0.04602에서 키를 내지 않고 성공을 반환한다. 호출자는 그 성공 여부를 버리고 다음 프레임에도 같은 계산을 했다. 프레임마다 키 상태를 비우므로 D가 계속 해제되고 발판을 벗어날 수 없었다. 접지를 잃을 때까지 입력을 유지하는 단계가 빠져 있었다. 11차 커밋과 작업 전 사본의 이동 코드·방 데이터 Git blob도 일치했다. [11차 하강 분기](https://github.com/SooYoungJang/Rewind-Notary-R.E.A./blob/65787ebf/Assets/Scripts/AutoTest/ClockTraversal.cs#L39) · [이동 허용치](Assets/Scripts/AutoTest/VelvetTraversal.cs#L14) · [키 갱신](Assets/Scripts/AutoTest/BotKeyboard.cs#L10)
3. **사람은 도달할 수 있다.** 윗판에서 D를 계속 누르면 오른쪽으로 돌출된 아래판에 떨어진다. 작성 지형의 겹침 중앙 x=20에서 같은 입력을 주면 일반 이동만으로 0.70초에 착지했다. 그 뒤 파수병 쪽으로 돌아서 기본검을 쓸 수 있다. 장갑형 `armor`의 세 마디에는 진자나 패링 선행 조건이 없다. 서로 다른 검 발동으로 세 번 베어야 한다. [파수병 설정](Assets/Resources/Waves/test-stage.json#L20) · [검 발동별 마디 판정](Assets/Scripts/Waves/WaveTargetState.cs#L54)

11차 자료에는 접촉 콜라이더의 이름·실제 끝 좌표가 없다. 해당 높이의 작성 고정면은 하나다. 접지 샘플에서 면 이름을 찾지 못했더라도, 당시의 예비 분기는 표적 x로 바로 `MoveTo(x)`를 호출하므로 같은 x=23에서 같은 무입력이 나온다. 화면 폭 2,560픽셀, 높이 1,440픽셀, 카메라 수직 범위 11을 쓰면 화면 중앙 부근 레아에서 오른쪽 가시 끝까지 약 500픽셀은 약 3.8월드 단위다. 이로부터 **보이는 끝**을 x≈26.8로 추정했다. 다음 실행의 구역 로그와 실패 사유에는 접촉 중인 작성 발판의 이름, 실제 콜라이더 좌우 끝과 상단 높이도 기록된다. [카메라 설정](Assets/Scripts/GameBootstrap.cs#L185) · [지지면 관측](Assets/Scripts/AutoTest/PlaytestObservations.cs#L31) · [구역 로그](Assets/Scripts/AutoTest/AutoTestDriver.cs#L192)

## 고정면 사이의 이동과 교전

- 새 방의 실제 콜라이더 범위로 고정면 사이의 방향별 연결을 계산한다. 겹치는 단방향 발판에는 제자리 점프, 열린 턱에는 걸어 내려가기, 떨어진 발판에는 수평 조작을 곁들인 점프를 시험한다. 플레이어의 캡슐 폭·높이, 점프 초속 12, 이동 속도 6.5, 공중 가속·제동, 방의 중력과 물리 틱으로 첫 착지면을 구한다. 벽·단단한 천장에 막히거나 다른 판이 먼저 받는 연결을 제외한다. 방 양 끝 벽 너머로 돌출된 면도 그쪽으로 하강할 수 없다. [연결과 도달 판정](Assets/Scripts/AutoTest/FixedSurfaceRoute.cs#L78)
- 너비 우선 탐색으로 현재 지지면에서 목표 지지면까지의 첫 동작을 고른다. 하강 중에는 출발면과의 접촉이 끝날 때까지 A/D를 유지하고, 공중에서는 아래판의 착지점으로 조향한다. 점프는 착지까지 Space를 유지한다. 다른 고정면이 받으면 그 면에서 다시 경로를 찾는다. 공용 전투 봇과 시계탑 교전은 목표면에 실제로 접촉한 뒤 조준·검·패링·기술 입력을 시작한다. 연결 계산에 방 ID, 표적 ID, 표적별 좌표는 들어가지 않는다. [경로 탐색](Assets/Scripts/AutoTest/FixedSurfaceRoute.cs#L56) · [키 입력과 착지](Assets/Scripts/AutoTest/ClockTraversal.cs#L51) · [공용 교전](Assets/Scripts/AutoTest/FirstDepartureBot.cs#L129)
- 방을 다시 생성하면 관측한 지형과 진행 중인 이동을 갱신한다. 작성 지형 외에 방 생성기가 만든 기본 고정 바닥도 사용할 수 있다. 경사로에 서면 끝까지 걷고, 선택 이동판에 우연히 탑승하면 그 판에서 걸어 나와 고정 경로에 복귀한다. 문 접근, 복귀점, 정거장 층 전환도 같은 고정면 이동기를 쓴다. [방 지형 관측](Assets/Scripts/AutoTest/PlaytestObservations.cs#L44) · [층별 행동 호출](Assets/Scripts/AutoTest/ClockDistrictProbe.cs#L202)

## 02방 수정 전후 재현

종료 좌표와 윗판 접촉을 고정하고 0.02초 간격으로 비교했다. 실제 동쪽 끝은 기록되지 않아 세 값을 대입했다. 수정 전에는 작성 좌표로 구한 x=23에서 20초간 멈췄다. 두 버전의 실제 하강 키 선택식을 종료 좌표에 별도로 대입해도 세 조건 모두 수평 입력이 0 → +1이었고, 수정본이 고른 첫 착지면은 모두 `cradle`이었다. 수정 후에는 착지한 뒤 기본검의 첫 타격 시점 0.16초, 전진량, 판정 상자, 활성 영역, 이전 발동의 연계 종료를 적용해 매 발동에서 한 번만 마디를 셌다. 표는 입력·물리 정적 재생 시간이다. 다른 적의 공격, 피격 경직, Unity 접촉 솔버와 렌더 프레임 지연은 이 재생에 포함되지 않는다. [재현 코드](tools/validate_fixed_surface_route.py#L117) · [검 충돌 상자](Assets/Scripts/CombatMotor2D.cs#L233) · [검 사용 종료](Assets/Scripts/CombatMotor2D.cs#L294)

| 입력 조건 | 첫 수평 입력 | 아래판 착지 | 활성 영역 진입 | 첫 마디 / 마지막 마디 절단 | 검 발동 / 유효 절단 / 잔여 마디 |
|---|---|---:|---:|---:|---:|
| 11차 분기, 세 가지 끝 좌표 공통 | 없음 | 20초간 0회 | 0회 | — | 0 / 0 / 3 |
| 수정 후, 끝 x=23.0 | D | 0.40초 | 0.32초 | 0.68 / 1.88초 | 3 / 3 / 0 |
| 수정 후, 끝 x=24.5 | D | 0.64초 | 0.56초 | 1.14 / 2.32초 | 3 / 3 / 0 |
| 수정 후, 끝 x=26.8 | D | 0.98초 | 1.18초 | 1.84 / 3.04초 | 3 / 3 / 0 |

x=23.0 조건에서는 공중에서 활성 영역에 들어가지만 검은 아래층 착지 후에만 발동했다. x=26.8 조건에서는 활성 영역 동쪽에 착지해 파수병을 향해 걸어 들어갔다. 작성 치수 그대로인 별도 재생에서도 x=20에서 시작해 2.06초에 세 번째 마디를 끊었다. [수치 원본의 `level_step3_metrics.hallSentry`](STATIC-VALIDATION.json)

## 02~10방 동선 감사

표의 분수는 작성된 고정면에서 서로 다른 출발·도착면을 순서 있게 짝지어, 점프 또는 하강 경로가 있는 쌍을 센 값이다. 방별 행과 probe의 입력을 함께 대조했다. 접촉 순서는 **01 → 02 → 03 → 04 → 05 → 06 → 07 → 08 → 10 → 09**다. [접촉·저장 판정](Assets/Scripts/AutoTest/ClockDistrictProbe.cs#L92) · [행별 분기](Assets/Scripts/AutoTest/ClockDistrictProbe.cs#L209)

| 방 | 예정 동선과 교전 | 경로 판정과 남은 제약 |
|---|---|---|
| 02 진자실 | 동·서 걸쇠 뒤 `cradle → pendulum_walk → axle_head`의 파수병을 순서대로 상대한다. 전소탕 후 x≥35, 최상층 복귀점과 03행 문으로 간다. | **30/30**. `swing_carrier`는 선택 지름길이다. 초기 위상에서 x=8로 점프하면 0.60초에 탑승 가능한 계산이다. 필수 걸쇠·접촉·문은 고정면 경로로 닿는다. [02 배치](Assets/Resources/Story/clock.json#L289) |
| 03 역류교 | 아래에서 `backstop → outer_lip → reverse_rack → idler → bridge_head` 순으로 표적층을 찾는다. 걸쇠 2마디, 장갑 3마디, 반사상 8마디를 처리한 뒤 x≥35와 상층 복귀점·문으로 간다. | **30/30**. 반사상은 공격 예고 끝에 L로 패링해 개방한 뒤 J로 벤다. `reverse_bridge`도 초기 위상 x=35 점프로 0.62초에 탑승 가능하다. 두 이동판 모두 전용 탑승·대기 입력이나 탑승 영수증이 없고 고정면 우회가 가능하다. [03 배치](Assets/Resources/Story/clock.json#L427) · [이동판 위상](Assets/Scripts/Story/StoryMovingSurface.cs#L21) · [방어 개방](Assets/Scripts/Waves/WaveTarget2D.cs#L86) |
| 04 탈진기 | `pallet → escape_wheel → lock_face → west_pallet`에서 걸쇠 둘, 반사상, 파수병을 상대한다. 전소탕·x≥35 뒤 `lift_head`에서 접촉하고 조사 전 닫힌 승강기에 F를 누른다. | **30/30**. 조사 뒤 01↔04 왕복도 지정 고정면과 실제 문 범위를 사용한다. [04 배치](Assets/Resources/Story/clock.json#L668) · [승강기 판정](Assets/Scripts/AutoTest/ClockDistrictProbe.cs#L251) |
| 05 전실 | 입장 바닥에서 `door_ring` x=27 장치에 F, `crown` x=15 접촉 뒤 x=18에서 06으로 간다. 일반 전투 표적은 0개다. | **30/30**. 고정 단차로 상승 가능하다. [05 장치·문](Assets/Resources/Story/clock.json#L899) |
| 06 대련장 | 슬롯 3 초행은 조사 전 분기다. `court_ring`에서 접촉하고 `drum_floor`로 내려가 `win:julius`로 체력 14 대련을 수행한다. | **12/12**. 조사 후 별도 분기의 시간 셔터는 6초 중 4초 열리며, x=31 받침판에 안전하게 서면 `timed:`가 완료된다. 단, 앞선 **`interlude:julius`에 봇 공격 분기가 없다**. 일반 표적도 없어 체력 7까지 줄이는 입력이 없으므로 이 분기 전체에는 경로가 없다. 초행의 대련 생존은 미측정이다. [06 행](Assets/Resources/Story/clock.json#L2838) · [셔터와 받침판](Assets/Scripts/Story/DistrictRoomLayout.cs#L209) · [중간 대련 조건](Assets/Scripts/Story/RepresentativeArea.cs#L293) |
| 07 상부 귀환로 | `turn_0` x=5.5에서 접촉, x=8 첫 정거장 발동·같은 발동 즉시 복귀, `cross:floor` 뒤 7층×6개 정거장을 왕복한다. 층 사이 턱의 경비·걸쇠는 진자로 준비한 뒤 검으로 벤다. 42곳 사용과 전소탕 뒤 08행 문을 연다. | 작성 치수 **156/156**, 30회 기준은 이 방이다. 첫 층의 양 끝이 벽 뒤까지 연장되는 경우에는 첫 하강부터 막힌다. 아래 폭 검사를 참조한다. [07 배치](Assets/Resources/Story/clock.json#L1169) · [정거장·층 경비 입력](Assets/Scripts/AutoTest/ClockDistrictProbe.cs#L122) |
| 08 하부 귀환로 | `lower_gallery_0` x=39에서 접촉한다. 긴 층 6개에 7개씩 정거장이 있고 짧은 턱에 준비형 적이 있다. 정거장만 세면 07의 42개와 이 방의 38개로 80회다. 유효한 전투 준비가 있으면 더 일찍 도달한다. 첫 양도·안전 귀환이 주행을 이어받는다. | 작성 치수 **110/110**. 08의 42개 전부를 첫 양도의 전제로 계산하지 않았다. 폭 검사에서는 07과 같은 하강 제약이 나타났다. [08 배치](Assets/Resources/Story/clock.json#L1605) · [실제 유효 사용 분류](Assets/Scripts/AutoTest/ClockDistrictProbe.cs#L41) |
| 10 재회선 | 조사 후 재회 경유 시 `rail_head` x=20 접촉, `drum_floor`의 x=18 받침판에서 시간 조건을 맞추고 상층 문으로 간다. | **20/20**. 재회 행은 시간 받침판을 검사한다. 세 층의 중갑병은 각각 진자 준비 3회와 검 1회가 필요하며, 이들의 전소탕은 09에서 넘어오는 별도 연계 행의 조건이다. [10 배치](Assets/Resources/Story/clock.json#L2292) · [재회 경유 선택](Assets/Scripts/Story/DistrictRoomLayout.cs#L55) |
| 09 연계 승강로 | 승강기 왕복·조사·은실 획득 후 02에서 들어온다. `switchback` x=20 접촉과 저장을 확인하면 probe가 종료된다. 정예는 진자 경비 둘, 반사상 하나, 검 걸쇠 둘이다. `sprocket` x=33 경비와 x=31 반사상은 활성 구간을 공유한다. | **20/20**. 교전 지지면에는 모두 닿는다. **09 정예 전소탕은 이 probe의 측정 범위 밖**이다. 별도 연계 행은 정거장 시전 → 해당 은실 솔기 설치 → 고정 바닥 횡단을 요구한다. `ReturnSwitch`는 07·08에서만 호출되고 09 전용 솔기 설치 입력도 없어 이 연계 행 전체에는 봇 경로가 없다. [09 배치](Assets/Resources/Story/clock.json#L2040) · [연계 행](Assets/Resources/Story/clock.json#L3073) · [종료 조건](Assets/Scripts/AutoTest/ClockDistrictProbe.cs#L209) |

**07·08의 물리 폭은 후속 실측에서 판정할 항목이다.** 02 화면에서 추정한 가시 끝 x=26.8은 폭 16인 판을 중심 x=14에서 1.6배 넓힌 경우와 일치한다. 이 배율을 충돌체에도 가정해 방 끝 벽까지 포함하는 별도 민감도 검사를 했다. 07의 첫 판 `turn_0`는 x=−8.5~47.5, 08의 `lower_gallery_0`는 −4.1~55.1이 된다. 두 경우 모두 중심 x=−4와 46인 벽 사이를 덮는다. 이 가정에서는 07의 정거장 층 전환 **0/6**, 08은 **0/5**이며, 전체 면 쌍도 각각 **84/156**, **60/110**이다. 플레이어에게 단방향 판을 수직으로 통과해 내려가는 입력은 없다. 이 수치는 두 방에서 측정한 충돌 폭이 아니므로 게임 지형 변경의 근거로 쓰지 않았다. [방 벽 생성](Assets/Scripts/Story/RepresentativeRoomLayout.cs#L103) · [플레이어 물리 입력](Assets/Scripts/PlayerController2D.cs#L100) · [넓은 판 검사](tools/validate_fixed_surface_route.py#L249)

## 검증과 변경 파일

작업공간 루트에서 `python3 -B tools/validate_gamedev.py --output unity/RewindNotary/STATIC-VALIDATION.json`을 실행했다. 기존 7,809개 검사명과 각 중복 횟수를 모두 보존했고, 신규 28건을 포함해 **7,837건 통과·0건 실패**였다. 기존 시계탑 변조 검출은 **130/130**이다. 별도 입력 재생에서는 C#의 연결 선택·착지 예측·키 분기를 JavaScript로 옮겨 고정면 중앙에서 다른 면 중앙까지 진행했다. 작성 치수에서 50Hz·60Hz 입력 각각 **458/458쌍**이 도착했다. 이 재생은 고정면 이동을 분리한 것으로 이동판 위상·경사 접촉·적 AI까지 포함한 구역 주행 시간은 산출하지 않는다. [통합 결과](STATIC-VALIDATION.json) · [이전 걸쇠 회귀와 이번 검사 호출](tools/validate_level_step3.py#L430)

접촉 순서, 진자 30·80회, 행의 전소탕, 무진전 18초·120초 기준을 그대로 사용한다. 스토리 JSON 6개의 작업 전후 SHA-256이 각각 일치했고 조건·보상을 포함하는 기존 321행 검사가 통과했다. 환경에 Unity와 C# 컴파일러가 없어 수정 후 에디터 컴파일, 실제 파수병 소탕 및 슬롯 3 완주는 미측정이다. [전소탕 판정](Assets/Scripts/Story/DistrictRoomLayout.cs#L263) · [파일 재독출 판정](Assets/Scripts/AutoTest/AutoTestRuntime.cs#L220)

| Unity 저장소 상대 경로 | 변경 내용 |
|---|---|
| `Assets/Scripts/AutoTest/FixedSurfaceRoute.cs` | 실제 콜라이더로 이동 연결 생성, 첫 착지 판정, 방향별 경로 탐색 |
| `Assets/Scripts/AutoTest/FixedSurfaceRoute.cs.meta` | 새 C# 파일의 Unity 메타데이터 |
| `Assets/Scripts/AutoTest/ClockTraversal.cs` | 지지면 식별, 이탈·공중 조향·착지 입력, 교전 전환 |
| `Assets/Scripts/AutoTest/FirstDepartureBot.cs` | 공용 표적 접근에도 같은 지지면 이동 적용 |
| `Assets/Scripts/AutoTest/PlaytestObservations.cs` | 작성·생성 지형 및 접촉 콜라이더 읽기 |
| `Assets/Scripts/AutoTest/AutoTestDriver.cs` | 시계탑 주기 로그·실패 사유에 지지면 이름과 범위 추가 |
| `tools/validate_fixed_surface_route.py` | 11차 재현, 방별 연결 및 벽·단차·간격 검사 |
| `tools/validate_level_step3.py` | 9·10차 회귀를 새 호출 경로에 연결하고 11차 검사 통합 |
| `STATIC-VALIDATION.json` | 전체 검증 결과와 재현 수치 |
| `LEVEL-STEP3-FIX3-REPORT.md` | 원인·재현·후속 방 감사 |

작업공간 루트의 `push-paths-level-step3-fix3.bin`은 위 10개 경로를 담는다. 각 경로는 Unity 저장소 루트 기준이며 NUL로 구분하고 마지막 경로 뒤에도 NUL을 둔다. 390바이트를 다시 읽어 중복·빈 항목·매니페스트 자체 포함이 모두 0건임을 확인했다.
