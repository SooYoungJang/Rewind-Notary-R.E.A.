# 레벨 6단계 수정 5 · 화랑 07 → 08 이동 정지

## 목표

QA 27차의 화랑 07 마지막 웨이브 정리 직후를 재생해 08행 문 앞에서 멈춘 원인을 가리고, 같은 전환을 영구 검사로 남긴다. **원인은 봇의 문 접근 처리였다.** 봇은 올바른 문까지 갔지만, 발밑의 전투 물감에서 내려올 입력을 만들지 못했다. [27차 로그 5217·5222행](../../testresults/run27/run.log) · [실패 상태](../../testresults/run27/report.json) · [당시 화면](../../testresults/run27/screenshots/048-GalleryDistrict-failure.png)

## 변경

화랑 봇의 `GalleryDoor`에서 문 중심을 곧바로 조준하던 경로 요청을 교체했다. 문 접근 범위에 수평 물감이 걸쳐 있으면 물감 양 끝에서 출구 바닥 양 끝까지의 여유 폭을 비교한다. 몸 반폭·물감 지지 허용 거리·이동 목표 오차를 확보할 수 있는 넓은 쪽의 중점을 `ClockPosition`에 먼저 넘긴다. 물감 콜라이더가 사라지면 문 중심으로 걸어와 고정 바닥 접촉과 문 범위를 확인한 뒤 F를 누른다. 봇은 실제 콜라이더 목록을 읽으므로 수명이 끝나도 발밑을 받치고 있는 물감을 인식한다. 게임 코드·스토리 데이터·주석의 변경은 0건이다. [화랑 문 접근](Assets/Scripts/AutoTest/GalleryTraversal.cs) · [물감의 퇴장 조건](Assets/Scripts/Skills/PaintSurface2D.cs)

27차 발 좌표·플래그·실측 바닥·마지막 정거장 접촉을 발췌하고, 구역별 `advance`와 물감 위 문 접근 재생을 6단계 영구 검사에 연결했다. [27차 입력](tools/run27-gallery-advance.json) · [구역별 경로 재생](tools/validate_district_advance.py) · [문 선택·입력·전환 재생](tools/replay_district_advance.js) · [통합 진입점](tools/validate_level_step6.py)

## 검증

작업공간 루트에서 Python 3.12.3과 Node.js 24.22.1로 실행했다. 두 단독 실행은 봇 소스 하나만 다르다. 수정 전 파일은 작업 시작 때 복사했으며 패스 2 직전 보관본과 SHA-256이 일치했다. Git 저장소에서 이 대조 파일을 다시 만들 때는 `git show 5e384f91:Assets/Scripts/AutoTest/GalleryTraversal.cs`의 출력을 사용한다.

```sh
python3 -B tools/validate_district_advance.py --gallery-traversal /tmp/fix5/GalleryTraversal.cs
python3 -B tools/validate_district_advance.py
python3 -B tools/validate_gamedev.py --output unity/RewindNotary/STATIC-VALIDATION.json
sha256sum -c /tmp/fix5/story.sha256
python3 -B /tmp/fix5/verify-delivery.py
```

| 실행 | 통과 | 실패 | 결과 |
|---|---:|---:|---|
| 신규 재생 · 수정 전 소스 | 17 | 5 | 27차·패스 2 지형, 새 물감, 시전 위치 오차 양 끝에서 모두 900틱 동안 F 입력 없음. 종료 코드 1 |
| 신규 재생 · 수정 후 소스 | 22 | 0 | 위 다섯 상태에서 모두 고정면에 착지한 다음 F 입력. 종료 코드 0 |
| 전체 영구 검사 · 수정 후 | **11,168** | **0** | 패스 2 기준선 11,146건 전부 통과, 신규 22건. 기존 경고 25건 동일 |

기준선의 검사명을 중복 횟수까지 대조했다. 누락 0건이며 새 재생 결과를 제외한 기존 지표도 일치했다. 스토리 JSON 6개는 파일 전체의 SHA-256 대조에서 모두 `OK`였다. 무진전 18초, 구역 정체 120초, 구역 제한 1,500초와 지형 허용 오차 0.005를 그대로 사용했다. [전체 검증 결과](STATIC-VALIDATION.json) · [패스 2 기준선](LEVEL-DESIGN-PASS2-REPORT.md)

| 27차와 같은 발에서 시작한 정적 재생 | 첫 입력과 움직임 | 08행 문 결과 |
|---|---|---|
| 수정 전 봇 | 목표 x=4.00, 수평 입력 0. 900틱 뒤에도 x=4.2166, 물감 지지 유지 | 고정면 접촉 0회, F 0회 |
| 수정 후 봇 · 수명이 끝난 물감 | x=5.80을 향해 D. 7틱에 물감 제거, 12틱에 (5.5414, 9.0000) 고정면 착지 | 23틱·0.46초에 (4.1114, 9.0000)에서 F |
| 수정 후 봇 · 수명이 3초 남은 물감 | 같은 고정면에 12틱에 착지. 150틱에 물감 제거 | 162틱·3.24초에 F |
| 사람 입력 대조 | 실패 좌표 (4.2166, 9.2600)에서 이동 없이 F 한 번 | 첫 입력에서 `crossed:gallery_return_02_done` 기록과 08행 `Travel` 호출 |
| 물감을 이미 벗어난 봇 | 문 중심 (4, 9)의 고정면에서 F | 첫 입력에서 같은 문 통과 |

두 입력 모두 도착점은 `gallery_return_lower_safe`다. 08의 복원 좌표에서 낙하를 재생하면 x=6.45의 `vault_0_0`에 착지해 다음 접촉 저장점 `gallery_rest_08`의 조건을 만족한다. 표적 한 기가 남았거나, `irina_defeated`가 없거나, 문 범위 밖이거나, 공격 중이면 사람 F 입력에도 전환과 `crossed` 기록은 모두 0회였다. [전환 조건](Assets/Scripts/Story/DistrictRoomLayout.cs) · [접촉 저장 조건](Assets/Scripts/Story/StoryTerrain.cs)

구역 스토리에서 웨이브를 걸고 다음 방으로 이동하는 `advance` 행은 8개다. 각 행에서 마지막 명단 표적의 고정면을 출발점으로 두고, 봇이 선택한 문·생성한 경로·도착 후 F·목적지 바닥을 대조했다. 오페라 07은 마지막 램프 점등 후 그늘 신호가 열린 상태, 각 09는 조사와 두 기술 획득을 마친 상태다. 아래의 틱은 이 조건의 고정면 출발 재생값이다.

| 구역 | 07 → 08 · 마지막 표적 바닥에서 F까지 | 09 → 10 · 마지막 표적 바닥에서 F까지 |
|---|---|---|
| 시계탑 | `drum_floor`에서 보행, 68틱 | `switchback`에서 보행, 37틱 |
| 오페라 | `turn_3 → circle_4 → turn_4 → circle_5`, 611틱 | `access_step → counterweight → fly_door`, 186틱 |
| 대종루 | `lower_3 → chime_3 → lower_4 → heel_4 → chime_5`, 698틱 | `anvil → sound_bridge`, 52틱 |
| 화랑 | 마른 `atelier_3_0`에서 3틱. 물감 위 출발은 위 표 참조 | `portrait_balcony → terrace_0_4`, 91틱 |

벨벳의 해당 토큰은 0개다. 27차 실기에서 벨벳은 접촉 6/6, 시계탑·오페라·대종루는 각각 10/10으로 통과했다. 이 세 구역 probe의 마지막 접촉 순서는 08 → 10 → 09다. 위 표의 09 → 10은 probe 종료 뒤에도 해당 스토리 행동을 계속 수행하는 조건의 정적 대조다. [27차 구역별 결과](../../testresults/run27/report.json) · [8개 행의 경로와 도착 좌표](STATIC-VALIDATION.json)

## 판단 근거

1. **실측 위치.** 27차 241.1초에는 봇이 (59.27, 9.00)에 있었고 `sword_latch_3` 한 기·한 마디가 남았다. 이 수치는 절단 좌표를 기록한 값이 아니다. 걸쇠 배치는 (3.50, 9.05)다. 271.3초와 실패 시점인 284.3초(무진전 18.2초)의 발은 모두 (4.22, 9.26)이고, 접지 참·고정 바닥 거짓·표적 0·접촉 7/10이다. 이곳은 07 입구 저장점 (6.45, 25.20) 아래층의 **08행 출구 앞**이다. 실패 화면에도 08행 F 안내가 떠 있다. 로그의 `segments=0`과 스냅샷의 빈 `target`은 웨이브 표적의 마디 수와 ID다. 경로 상태는 재생으로 구했다. [27차 로그 5156·5217·5222행](../../testresults/run27/run.log) · [로그 필드 정의](Assets/Scripts/AutoTest/AutoTestDriver.cs) · [화랑 배치](Assets/Resources/Story/gallery.json)
2. **행과 문.** `story.gallery.return.02`의 대화 확인 플래그는 있었고 행 완료 플래그는 없었다. 120.5초에 표적이 4기 남았을 때부터 이 행의 첫 행동 `advance:gallery_08_return_gallery`가 이미 활성 상태였다. 07에서 웨이브 0기, 안전한 접지, 열린 문, 접근 범위, F가 모두 충족되면 게임이 `crossed`를 기록하고 이동한다. 08에서 같은 행의 두 번째 행동 `clear:wave`를 마쳐야 행이 끝난다. 07 출구 2개 중 목적지가 08인 문은 하나이며, 필요한 `irina_defeated`는 실패 당시 보유했다. 접근 상자는 x=3.10~4.90, y=8.84~9.74다. [27차 플래그](tools/run27-gallery-advance.json) · [행 진행과 복원](Assets/Scripts/Story/RepresentativeArea.cs) · [게이트](Assets/Scripts/Story/RepresentativeRoomLayout.cs)
3. **봇이 멈춘 분기.** 문의 조준 높이 9.29와 고정면 `atelier_3_0` 상단 9.00의 차이 0.29는 `FixedFaceAt` 한계 0.50 안이다. 실제 고정면 접촉은 없지만 발 높이 9.26은 `ClockPosition`이 낮은 덧발판 아래의 고정면을 찾는 범위 8.90~9.50에 든다. 재생의 면 조회 순서는 목표 `atelier_3_0` → 실제 접촉 없음 → 아래 면 `atelier_3_0`이었다. 따라서 `here == goal`에서 `Walk`로 갔고, `FixedSurfaceRoute.Next` 호출은 0회였다. 문 중심과의 x 차이 0.2166도 정지 오차 0.24 안이라 이동 입력은 0이다. 이어지는 `OnReturnFloor`가 거짓이어서 봇은 F도 누르지 않았다. 수명이 끝난 물감은 발이 지지 범위를 벗어날 때까지 충돌을 유지한다. [고정면 조회와 분기](Assets/Scripts/AutoTest/ClockTraversal.cs) · [보행 판정](Assets/Scripts/AutoTest/FirstDepartureBot.cs) · [실제 고정 바닥 판정](Assets/Scripts/AutoTest/PlaytestObservations.cs)
4. **물감의 복원 조건.** 마지막 정거장 물감은 x=8의 틈을 덮었고, 폭은 2.40이었다. 이 물감은 실패 좌표를 받칠 수 없다. 마지막 걸쇠 세팅은 왼쪽 시전이며 명목 시전점 x=4.95, 전투 물감의 복원 범위는 x=2.20~4.60이다. 마지막 정거장에서 실측한 같은 높이의 물감 상단 9.2550을 적용했다. 전투 물감 자체의 박스·시전 오차·잔여 수명·속도는 로그에 없다. 초기 속도 0, 공격 회복 완료로 두었고, 시전 허용 오차 양 끝(±0.10)에서도 수정 후 각각 21틱·25틱에 통과했다. 재생은 문 선택과 `ClockPosition`, 게임의 게이트·전환 분기를 C# 소스에서 읽어 실행하고, 충돌과 발 이동은 20ms 고정 틱, 속도 6.5, 중력 29.43, 몸 반폭 0.31로 계산했다. [물감 설치](Assets/Scripts/Skills/CanvasCarveSkill.cs) · [실측 발췌](tools/run27-gallery-advance.json) · [재생 코드](tools/replay_district_advance.js)
5. **패스 2 영향.** 07의 고정면 수는 50 → 58, 08은 53 → 63으로 늘었다. 241.1초에 밟은 `atelier_3_7`에서 출구 바닥까지의 고정 경로도 7 → 9구간으로 바뀌었다. 마지막 걸쇠 배치, 출구 바닥 x=1~7·상단 y=9, 문 상자·게이트와 08 도착 바닥은 같았다. 패스 2 전 데이터를 복원해 같은 발에서 재생했을 때도 수정 전 900틱 정지, 수정 후 23틱 전환이었다. 25차의 03 가교 통과와 이번 07 정지는 서로 다른 분기에서 발생했다. [패스 2 변경](LEVEL-DESIGN-PASS2-REPORT.md) · [이전 설계 복원](tools/story_link_baseline.py) · [03 가교 수정](LEVEL-STEP6-FIX3-REPORT.md)

## 남은 일

이 환경에는 Unity Editor와 C# 컴파일러가 없어 수정본의 Play Mode와 새 Game 뷰 스크린샷은 미측정이다. 27차 실측은 화폭 유효 사용 48회(정거장 40·전투 8), 07에서 2단계, HP 6/6·사망 0으로 끝났다. 08의 실제 접촉 저장과 극의, 지름길, 슬롯 6 최종 판독은 다음 Unity QA에서 확인할 항목이다. 매니페스트를 바이트로 다시 읽어 Unity 레포 상대 경로 7개·225바이트·끝 NUL을 확인했다. 누락 파일·중복·자기 참조는 0건이다. 푸시는 메인이 담당한다. [27차 실패 스크린샷](../../testresults/run27/screenshots/048-GalleryDistrict-failure.png) · [푸시 매니페스트](../../push-paths-level-step6-fix5.bin)
