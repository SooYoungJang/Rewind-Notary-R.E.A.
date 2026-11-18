# 시계탑 07·08방 귀환로 하강 수정 · 13차 실행 회귀

**게임 변경 — PO 검토 필요.** 13차의 07방에서는 사람도 `turn_0`에서 내려갈 수 없다. 발판 양끝이 방의 벽을 지나고, 아래 입력으로 발판을 통과하는 조작도 없다. 같은 충돌 폭을 08방에 적용하면 첫 층부터 똑같이 막힌다. 시계탑 지형의 충돌 상자를 작성 치수로 생성하도록 수정한 뒤, 기존 걷기 입력의 정적 재생에서 07방 **6/6**, 08방 **5/5** 층간 하강이 모두 착지했다. 수정본의 Unity 실행과 슬롯 3 완주는 미측정이다. [정적 검증 결과](STATIC-VALIDATION.json)

## 13차에서 멈춘 위치

커밋 `891a0a8a`, Unity 6000.6.9f1에서 슬롯 3은 01→02→03→04→05→06→07 순서로 **7/10**개 복귀점에 접촉했다. 02·03·04 웨이브 소탕, 율리우스 대련 승리, 첫 귀환 행 완료와 07방 정거장 6회 사용이 기록됐다. 구역 시작 185.09초에 발은 **(34.1599, 39.7100)**, 지지면은 `turn_0`, 목표는 `turn_1`이었다. 다음 행 `story.clock.return.02`의 남은 표적은 4개였다. 이때 경로 탐색이 예외를 던져 `errors`에 하네스 오류가 기록됐다. 08방 수치는 이 실행의 관측 범위 밖이다. [13차 결과](../../testresults/run13/report.json) · [13차 로그](../../testresults/run13/run.log#L2318)

## 1.6배가 생긴 경로

`clock.json`의 `terrain[].rect`를 읽은 `BuildSurfaces`는 사각형의 중심과 폭·높이를 그대로 `Block`에 넘긴다. 13차의 `Block`은 같은 오브젝트에 황동 `SpriteRenderer`를 먼저 붙이고 `Tiled` 모드와 작성 크기를 설정한 뒤, `BoxCollider2D`를 추가해 같은 폭·높이를 지정했다. **1.6이라는 수의 출처는 황동 타일 한 장의 월드 폭**이다. `BrassSprite`는 64×64 텍스처에 `plate.width / 1.6f`를 지정하므로 40픽셀/유닛, 타일 한 변은 64/40 = 1.6유닛이다. 타일 중심과 발판 중심은 정중앙이다. [13차 Block 코드](https://github.com/SooYoungJang/Rewind-Notary-R.E.A./blob/891a0a8afdc601b0fb40b1ee6c5dffef8ef2dda8/Assets/Scripts/Story/RepresentativeRoomLayout.cs#L29-L48) · [황동 타일과 지형 생성](Assets/Scripts/Story/StoryTerrain.cs#L15) · [Unity의 Sprite.Create 단위](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Sprite.Create.html)

| 13차 지지면 | 작성 x 구간 | 접촉 중인 콜라이더의 월드 x 구간 | 실측 폭 ÷ 작성 폭 |
|---|---:|---:|---:|
| 03 `idler` | [26, 40] | [21.80, 44.20] | 22.4 / 14 = 1.6 |
| 04 `drum_floor` | [−3, 46] | [−17.70, 60.70] | 78.4 / 49 = 1.6 |
| 07 `turn_0` | [2, 37] | [−8.50, 47.50] | 56 / 35 = 1.6 |

세 면 모두 중심은 같고, 작성 높이 0.35도 중심을 기준으로 1.6배 키우면 실제 상단 19.66·23.21·39.71과 각각 표시 정밀도까지 일치한다. 의도한 보정은 황동 무늬의 반복 간격이다. 게임 코드에는 물리 폭을 1.6배 하는 대입이 없고 시계탑 블록과 그 부모는 단위 스케일로 생성된다. 작성 지형이 있는 시계탑 10개 방이 이 황동 분기를 공유한다. 벨벳은 32픽셀짜리 직조 타일에 32픽셀/유닛을 지정해 한 변이 1유닛이고, 일반 블록은 단위 사각형을 트랜스폼으로 늘린다. [실측 지지면 로그](../../testresults/run13/run.log#L2276) · [구역별 블록 생성](Assets/Scripts/Story/RepresentativeRoomLayout.cs#L29)

프로젝트에서 시각 치수와 물리 형상이 만나는 곳은 위의 동일 오브젝트 생성 경로다. Unity는 타일 렌더러에서 박스 형상을 갱신하는 `autoTiling`을 제공한다. 13차에는 최종 `bounds`만 남았으므로 당시 `autoTiling` 값이나 엔진 내부에서 형상을 갱신한 시점까지 특정할 수는 없다. 수정본은 이 결합을 없애고, 접촉 로그에 작성 폭·로컬 박스 폭·월드 스케일·`autoTiling`을 실제 양끝과 함께 남긴다. [Unity의 BoxCollider2D.autoTiling 명세](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/BoxCollider2D-autoTiling.html) · [접촉 로그](Assets/Scripts/AutoTest/PlaytestObservations.cs#L31)

## 사람이 내려갈 수 있는가

`PlayerController2D`는 수평축과 점프를 읽고, 지형은 `PlatformEffector2D.useOneWay = true`, `surfaceArc = 150°`로 위에서 내려오는 몸을 받는다. S·↓·패드 아래는 기술 방향 입력에 쓰인다. 아래+점프로 발판 충돌을 잠시 해제하는 분기는 없다. 일반 대시와 첫 귀환 때 보유한 진자 돌진은 수평 이동이고, 진자 복귀는 경로 충돌을 검사한 뒤 발동 원점으로 돌려보낸다. [플레이어 입력](Assets/Scripts/PlayerController2D.cs#L36) · [방향 입력](Assets/Scripts/Shell/GameInput.cs#L29) · [단방향 지형](Assets/Scripts/Story/StoryTerrain.cs#L106) · [진자 복귀](Assets/Scripts/Skills/PendulumRewindSkill.cs#L69)

| 수정 전, 13차 배율 기준 | 07 상부 귀환로 | 08 하부 귀환로 |
|---|---:|---:|
| 전체 지형 | 긴 층 7개·중간 턱 6개 | 긴 층 6개·중간 턱 5개 |
| 첫 긴 층의 x 구간 | [−8.50, 47.50] 실측 | [−4.10, 55.10] 재구성 |
| 나가는 모든 긴 층에서 벽 안쪽 면을 넘어선 최소 길이 | 1.82유닛 | 0.42유닛 |
| 벽 상단 / 최상층에서 일반 점프 시 발 높이 상한 | 61.53 / 42.16 미만 | 35.79 / 22.36 미만 |
| 정거장 순서로 다음 층에 도달 | 0/6 | 0/5 |

두 방의 벽 중심은 x=−4와 46이다. 13차 배율에서 벽의 안쪽 면은 −3.68과 45.68, 폭 0.62인 플레이어의 중심이 움직일 수 있는 범위는 **[−3.37, 45.37]**이다. 내려가야 하는 11개 긴 층 모두가 벽 사이를 빈틈없이 덮는다. 중간 턱은 그 아래에 있고, 두 방의 이동판·경사로·벽 박차기 가능한 면·추가 디딤판은 각각 0개다. 경계벽은 박차기용 `AuthoredSurface`를 갖지 않는다. 방 밖을 도는 문도 첫 방문 하강을 대신하지 못한다. 07→08 문은 발 높이 19.65~20.55, 08의 전진 출구들은 −0.15~0.75에서만 닿는다. 05·06→07과 04→08 문도 도착점이 각각 해당 방 최상층이다. 08→07 문만 07 중간층으로 돌아가지만, 07에서 진행 중인 귀환 행은 07 전소탕 후 하단의 08행 문에서 조사 입력을 받아야 전진한다. 상층에서 그 문으로 우회할 수 없다. [두 방과 문 배치](Assets/Resources/Story/clock.json#L1169) · [벽 생성](Assets/Scripts/Story/RepresentativeRoomLayout.cs#L111) · [벽 박차기 조건](Assets/Scripts/PlayerController2D.cs#L83) · [귀환 행 진행](Assets/Scripts/Story/DistrictRoomLayout.cs#L263)

## 수정 내용

**게임 변경 — PO 검토 필요.** 황동 지형의 충돌 상자와 단방향 이펙터는 작성 크기의 루트에 두고, 타일 렌더러는 단위 스케일 자식으로 옮겼다. 루트 박스는 `autoTiling = false`로 명시한다. 경사로의 렌더 길이와 시간제 발판의 색도 실제 렌더러에 적용한다. 이 수정은 시계탑의 황동 고체 블록 전체에 적용되므로 07·08 외의 고정면·경사·이동판·경계벽도 PO 검토 범위에 들어간다. 06 대련 분기와 타이머의 로직은 기존 것을 사용한다. [블록 생성](Assets/Scripts/Story/RepresentativeRoomLayout.cs#L29) · [경사로](Assets/Scripts/Story/StoryTerrain.cs#L88) · [시간제 발판](Assets/Scripts/Story/DistrictRoomLayout.cs#L209)

**봇과 실패 기록.** 기존 고정면 탐색은 실제 `Collider2D.bounds`로 걸어 떨어질 가장자리와 첫 착지면을 찾는다. 충돌 폭을 복원하면 기존 A/D 이동으로 두 방을 내려간다. 방·발판별 이동 예외나 새 조작은 필요하지 않다. 경로 단절·목표면 누락·좌표 지지면 누락은 이제 `FailRoute → AutoTestDriver.Stop`으로 활성 probe를 실패 종료한다. 이미 저장한 순차 접촉과 정거장 횟수에 방·발 좌표·지지면·행·행동·남은 표적·마디를 더해 기록하고, 실패 화면과 종료 상태를 저장한다. 경로 단절 자체가 하네스 예외로 추가되지 않는다. [고정면 탐색](Assets/Scripts/AutoTest/FixedSurfaceRoute.cs#L56) · [이동 실패 처리](Assets/Scripts/AutoTest/ClockTraversal.cs#L18) · [실패 상태 저장](Assets/Scripts/AutoTest/AutoTestDriver.cs#L296)

## 수정 전후 재현

같은 지형·벽·캡슐·입력 조건에서 13차의 중심 고정 1.6배 상자와 수정본의 작성 크기 상자를 비교했다. 정적 재생은 20ms 간격으로 보행 속도 6.5, 중력 29.43, 공중 가속 45·제동 28을 사용한다. 시간은 걷기 시작부터의 누계다. 07의 시작 x는 13차 종료 좌표이고, 08은 첫 층 마지막 정거장의 작성 x다. [재현 코드](tools/validate_fixed_surface_route.py#L174) · [착지별 원자료](STATIC-VALIDATION.json)

| 첫 하강 | 수정 전 | 수정 후 중간 턱 착지 (x, 상단 y) | 수정 후 다음 정거장 층 착지 (x, 상단 y) |
|---|---|---|---|
| 07 `turn_0 → turn_1`, x=34.1599에서 시작 | 경로 0개 | D로 x=37 끝을 나가 `pawl_0` (38.814, 37.95), 0.84초 | A로 x=34 끝을 나가 `turn_1` (32.098, 36.30), **1.98초** |
| 08 `lower_gallery_0 → lower_gallery_1`, x=9.7에서 시작 | 경로 0개 | A로 x=7 끝을 나가 `lower_pawl_0` (5.224, 17.82), 0.84초 | D로 x=12 끝을 나가 `lower_gallery_1` (13.853, 15.84), **2.30초** |

| 정적 검사 | 07 수정 전 → 후 | 08 수정 전 → 후 |
|---|---:|---:|
| 정거장 층간 하강, 마지막 바닥 포함 | 0/6 → **6/6** | 0/5 → **5/5** |
| 출발·도착 방향을 구분한 모든 고정면 쌍 | 84/156 → **156/156** | 60/110 → **110/110** |

수정 후 시계탑 10개 방의 고정면 쌍은 총 **458/458**이다. 두 변경의 효과도 따로 검사했다. 13차 블록 생성 코드만 검사 입력에 되돌리면 하강·실패 기록 관련 검사 21건 중 9건이 통과하고 하강은 다시 0/6·0/5가 된다. 13차 이동 실패 코드만 되돌리면 하강 11건은 통과하고 실패 기록 검사 한 건만 실패한다. 현재 코드는 21/21이다. 이 재생은 고정면 간 보행과 착지를 검증한다. 전투·정거장 사용 시간이나 Unity 접촉 솔버가 포함된 실측 시간은 아니다.

작업공간 루트에서 `python3 -B tools/validate_gamedev.py --output /tmp/clock-fix4-resume-verify.json`으로 재실행했다. **7,859건 통과·0건 실패**, 시계탑 변형 **130/130** 검출이다. 결과 전체의 SHA-256은 `05815d410badd1765de79dd1dce60aab4f44014b78d734e7df4198b7690c136e`로 이전 실행이 저장한 파일과 바이트 단위로 같다. 13차 7,840건과 검사명을 중복 횟수까지 대조하면 19건이 추가됐고, 봇 생성자에 실패 콜백을 전달하는 기존 변형 검사 한 건은 호출식에 따라 이름이 바뀌었다. 접촉 순서 01→02→03→04→05→06→07→08→10→09, 진자 30·80회, 웨이브 전소탕, 무진전 18초·120초 기준을 유지했다. 스토리·대사·행 데이터는 13차 커밋과 일치한다. 이 환경에는 Unity 에디터와 C# 컴파일러가 없어 수정본의 컴파일·Play Mode 구역 완주 결과는 없다. [통합 검증 결과](STATIC-VALIDATION.json) · [probe 통과 조건](Assets/Scripts/AutoTest/ClockDistrictProbe.cs#L28)

## 변경 파일과 푸시 매니페스트

| Unity 저장소 상대 경로 | 변경 내용 |
|---|---|
| `Assets/Scripts/Story/RepresentativeRoomLayout.cs` | 황동 렌더러와 물리 박스 분리 |
| `Assets/Scripts/Story/StoryTerrain.cs` | 경사로 렌더 길이 적용 |
| `Assets/Scripts/Story/DistrictRoomLayout.cs` | 시간제 발판 렌더러 조회 |
| `Assets/Scripts/AutoTest/ClockTraversal.cs` | 고정면 경로 실패 이유 전달 |
| `Assets/Scripts/AutoTest/FirstDepartureBot.cs` | 실패 콜백 수신 |
| `Assets/Scripts/AutoTest/AutoTestDriver.cs` | 실행 중인 probe의 실패 기록에 콜백 연결 |
| `Assets/Scripts/AutoTest/PlaytestObservations.cs` | 접촉 상자의 작성 폭·로컬 폭·스케일·타일 설정 기록 |
| `tools/validate_fixed_surface_route.py` | 13차 경계 복원, 07·08 전 층 입력 재생, 실패 기록 검사 |
| `tools/validate_level_step2.py` | 기존 봇 생성자 변형 검사를 새 호출식에 연결 |
| `tools/validate_level_step3.py` | 문 접근과 지지면 누락 시 실패 처리 검사 |
| `STATIC-VALIDATION.json` | 통합 검증과 하강별 경로·착지 수치 |
| `LEVEL-STEP3-FIX4-REPORT.md` | 판정 근거와 재현 결과 |

작업공간 루트의 `push-paths-level-step3-fix4.bin`은 위 12개 파일을 담는다. Unity 저장소 루트 기준 상대 경로이며 총 454바이트다. 끝 NUL, 경로 중복 0개, 매니페스트 자체 포함 0개, 대상 파일 12개 존재를 파일에서 다시 확인했다. 작업공간과 Unity 디렉터리의 검증 스크립트 3쌍은 바이트가 같다.
