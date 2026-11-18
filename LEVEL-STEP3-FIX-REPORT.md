# 시계탑 구역 probe 9차 회귀 수정

**원인은 봇의 문 접근 방식 선택이었다.** 슬롯 3의 봇은 `hub_lift`에서 x=27의 시계탑행 문을 찾은 뒤, 기본 바닥을 쓰는 거점에서 시계탑의 층별 발판을 검색했다. `Array.Find`에 전달된 null은 현재 방의 `StoryRoom.terrain`이었다. 수정 후 같은 좌표에서 봇은 오른쪽 이동 입력을 내고, 문 앞 고정 바닥에 착지했을 때 조사 입력을 낸다. 첫 문 정적 재현은 **0/1 → 1/1**, 전체 정적 검증은 **7,798건 통과·0건 실패**다. 수정 후 Unity 완주는 미측정이다.

## 9차 실패 경로

9차 실행(`002c7573`, Unity 6000.6.9f1)에서 본편 9/9단계, 조작감 3/3종, 벨벳 파일럿 두 방의 고정·벽 경로, 벨벳 구역 접촉 6곳이 통과했다. 시계탑 probe는 5.357047초·311프레임에 중단됐다. 마지막 상태는 슬롯 3, `hub_lift`, 진행 중인 행 없음, 발 좌표 (4.0, 0.088228), 접지·안전 바닥 모두 거짓이었다. 시계탑 접촉과 진자 유효 사용은 모두 0이었다. [실패 스택](../../testresults/run9/report.json#L914) · [종료 상태](../../testresults/run9/report.json#L1596) · [시작·실패 시각](../../testresults/run9/run.log#L2191)

1. **문 조회는 성공했다.** `clock.json`의 첫 구역 링크는 `source="hub_lift"`, `room="clock_01_entry"`, `x=27`, `gate="depart"`다. `RepresentativeScript.Load`는 이 문을 출발 방의 `exits`에 추가한다. `ClockDistrictProbe.Drive`가 조사 전 첫 출발에서 `Door(bot, keys, Entry)`를 호출했고, `Door`는 `if (door == null || !bot.ClockDoor(door)) return false;`를 실행했다. 스택이 `ClockDoor` 안까지 이어졌으므로 `exits` 배열과 목적지 문이 모두 존재했다. [구역 링크](Assets/Resources/Story/clock.json#L3225) · [링크 병합](Assets/Scripts/Story/RepresentativeScript.cs#L107) · [목적지 선택](Assets/Scripts/AutoTest/ClockDistrictProbe.cs#L181) · [문 조회와 F 입력](Assets/Scripts/AutoTest/ClockDistrictProbe.cs#L305)
2. **실패한 검색 대상은 출발 방의 지형이었다.** 수정 전 `ClockDoor`는 아래 분기를 썼다. `FaceAt`의 첫 두 문장은 `var room = Area.Room;`과 `var face = Array.Find(room.terrain, …)`이었다. 따라서 스택의 `Parameter name: array`는 `hub_lift.terrain`을 가리킨다. 봇의 점프 상태나 목적지 방의 지형은 이 배열을 공급하지 않는다. 스택으로는 당시 `box != null`까지 확정할 수 있다. 실행 로그에는 사각형의 폭·높이가 없어, 재현에는 JSON에 생략된 사각형을 폭·높이 0의 객체로 주었다. [봇이 읽는 현재 방](Assets/Scripts/AutoTest/FirstDepartureBot.cs#L25) · [발판 검색](Assets/Scripts/AutoTest/ClockTraversal.cs#L69)

   ```csharp
   var box = door.approach;
   if (box == null) return Walk(door.x, .35f) && Area.OnReturnFloor;
   return ClockPosition(FaceAt(door.x, box.y + box.height * .5f), door.x) &&
       Area.OnReturnFloor && door.InReach(Player.Body.position);
   ```

3. **게임은 이 데이터를 기본 바닥과 수평 문으로 처리한다.** `hub_lift`의 JSON에는 `terrain`이 없고, 시계탑행 링크에는 `approach`가 없다. 방 생성기는 거점에 상단 y=0의 고정 바닥을 만들고, `BuildSurfaces`는 `Room.terrain == null`이면 종료한다. `StoryDoor.InReach`는 폭·높이가 모두 양수인 사각형에서만 발 좌표를 검사한다. 그 밖에는 `Mathf.Abs(feet.x - x) <= 1.25f`를 쓴다. 두 필드의 생략은 게임이 지원하는 데이터 형식이다. [거점 원본](Assets/Resources/Story/representative.json#L213) · [기본 바닥 생성](Assets/Scripts/Story/RepresentativeRoomLayout.cs#L81) · [지형 생성](Assets/Scripts/Story/StoryTerrain.cs#L74) · [문 도달 판정](Assets/Scripts/Story/RepresentativeScript.cs#L35)

## 수정과 유사 경로 점검

- `ClockDoor`는 유효한 접근 사각형과 출발 방의 `drum_floor`가 모두 있을 때만 `FaceAt`과 `ClockPosition`으로 해당 층을 찾아간다. 그 밖의 문으로는 `Walk(door.x, .35f)`로 접근한다. 두 경로 모두 목표 위치·안전한 고정 바닥·실제 `door.InReach`를 확인해야 F 입력을 낸다. 잠금 조건과 실제 방 전환은 게임의 `Gate`와 `NearestDoor`가 처리한다. [문 접근](Assets/Scripts/AutoTest/ClockTraversal.cs#L120) · [문 선택](Assets/Scripts/Story/RepresentativeRoomLayout.cs#L207)
- `FaceAt` 직접 호출은 문과 전투 두 곳이다. 전투의 두 진입점은 시계탑 내부의 일반 교전과 귀환 정거장 층의 경비병이다. 10개 방의 전투 배치 **31/31개**가 검색 허용 범위 내의 고정면에 있었다. 접촉·행 목표·대련·정거장에도 쓰이는 `ClockPosition`은 목표 발판을 찾은 뒤에만 배열을 순회한다. 두 함수 모두 지형이 없는 명시적 발판 요청에는 방 ID와 목표가 담긴 예외를 낸다. [전투와 위치 지정](Assets/Scripts/AutoTest/ClockTraversal.cs#L11) · [귀환 경비병과 행 처리](Assets/Scripts/AutoTest/ClockDistrictProbe.cs#L144)
- 공용 `FirstDepartureBot.Drive`에서도 높은 문이면 벨벳 계단의 `tread_1`·`tread_2`를 바로 읽는 경로를 발견했다. 시계탑 문 30개 중 24개가 이 높이 조건에 해당한다. 공용 봇도 시계탑에서는 `ClockDoor`를 사용하고, 벨벳의 유효한 상층 문 6개에서만 `ReachBalcony`를 호출하도록 수정했다. 벨벳 구역 probe와 파일럿이 직접 사용하는 계단·발코니는 각 호출 대상 방에 존재한다. 시계탑 probe의 벨벳 수로→거점 문은 이미 수평 이동·고정 바닥·도달 범위를 검사한다. [공용 봇](Assets/Scripts/AutoTest/FirstDepartureBot.cs#L131) · [벨벳 구역 probe](Assets/Scripts/AutoTest/VelvetDistrictProbe.cs#L161) · [파일럿의 방 범위](Assets/Scripts/AutoTest/PilotProbe.cs#L98) · [거점 복귀](Assets/Scripts/AutoTest/ClockDistrictProbe.cs#L192)
- 구역·피날레 링크 병합 후 **56개 방·방향별 문 166개**를 점검했다. 56개 방 모두 `exits`와 `floors` 배열이 있었다. 지형을 생략한 39개 방에 문 115개가 있고, 이 중 `hub_lift`의 문이 9개다. 시계탑 30개와 벨벳 17개 문에는 유효한 접근 사각형이 있다. 시계탑 probe의 첫 출발, 조사 후 벨벳행, 은실 획득 후 시계탑 재출발이 모두 이번 거점 문 수정의 적용을 받는다.

## 재현 수치와 검증 범위

통합 검사에서는 실제 방·링크를 읽고 `ClockDoor` 소스에서 수평 이동을 선택하는 조건을 뽑아 같은 입력을 대입했다. 추가로 수정 전후의 `Door`, `ClockDoor`, `FaceAt`, `Walk`, `InReach` 분기를 JavaScript로 재생해 좌표별 키 호출 수를 대조했다. 재생에서는 각 행의 발 좌표와 고정 바닥 접촉 상태를 직접 주었다. [정적 재현 검사](tools/validate_level_step3.py#L190)

| 입력과 관측 항목 | 수정 전 | 수정 후 |
|---|---:|---:|
| 9차 실패 좌표, 시계탑행 첫 문, 기본 사각형 객체 | 발판 검색 1회, 배열 예외 1회, D·F 0회 | 발판 검색 0회, D 1회, F 0회 |
| 같은 문 x=27, 공중 → 고정 바닥 착지, 두 상태의 F 횟수 | 두 상태 모두 배열 예외 | 공중 0회 → 착지 1회 |
| 거점 문 9개 × 접근 사각형 2종(null·기본 객체), 각 문 앞 고정 바닥 | F 9/18, 배열 예외 9회 | F 18/18, 예외 0회 |
| 실제 시계탑 문이 접근 범위 안의 고정면을 찾은 건수 | 30/30 | 30/30 |
| 추가한 정적 검사 38건 | 31 통과·7 실패 | 38 통과·0 실패 |

평지에 유효한 사각형을 따로 준 경우에도 수정 후 x=27에서 범위 밖 높이의 F는 0회, 범위 안 높이의 F는 1회였다. 표의 F 횟수는 봇의 조사 시도 수다. 수정 후 Unity 에디터·플레이어와 C# 컴파일러를 이 환경에서 찾을 수 없어 실제 문 전환, 시계탑 접촉 10곳, 진자 30·80회 도달 및 완주 시간은 미측정이다.

작업공간 루트에서 `python3 -B tools/validate_gamedev.py --output unity/RewindNotary/STATIC-VALIDATION.json`을 실행했다. 기존 검사명과 중복 횟수를 대조해 **7,760건 전부 유지**, 추가 38건을 포함해 **7,798건 통과·0건 실패**를 확인했다. 기존 시계탑 변조 검사도 130/130건을 검출했다. 18초 무진행 판정, 진자 30·80회 기준, 접촉 순서 01→02→03→04→05→06→07→08→10→09를 보존했다. 스토리 JSON 6개는 작업 전후 SHA-256이 각각 일치했고, 전체 321행의 기존 해시 검사도 통과했다. [전체 결과](STATIC-VALIDATION.json) · [무진행 판정](Assets/Scripts/AutoTest/AutoTestDriver.cs#L55) · [접촉 순서와 진자 기준](Assets/Scripts/AutoTest/ClockDistrictProbe.cs#L27)

## 변경 파일과 푸시 매니페스트

| Unity 저장소 상대 경로 | 변경 내용 |
|---|---|
| `Assets/Scripts/AutoTest/ClockTraversal.cs` | 문 접근 방식 선택, 발판 검색의 null 처리 |
| `Assets/Scripts/AutoTest/FirstDepartureBot.cs` | 시계탑 문과 벨벳 상층 문에 맞는 이동 선택 |
| `tools/validate_level_step3.py` | 첫 문 회귀와 실제 시계탑 문 30개 등 38건 추가 |
| `STATIC-VALIDATION.json` | 전체 정적 검증 결과 |
| `LEVEL-STEP3-FIX-REPORT.md` | 원인, 수정, 전수 점검 및 재현 결과 |

작업공간 루트의 `tools/validate_level_step3.py`와 Unity 저장소의 동명 도구는 같은 파일을 가리킨다. 작업공간 루트에 생성한 `push-paths-level-step3-fix.bin`에는 위 상대 경로 5개만 들어 있다. 구분자와 마지막 바이트는 NUL이며, 경로 중복과 매니페스트 자체 항목은 각각 0개다.
