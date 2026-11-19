# QA 23차 화랑 지형 판정 수정

**게임의 타일 표시 크기가 원인이었다.** 화랑 첫 방에서 `paper`의 오른쪽 끝은 작성 좌표 x=14보다 약 4.2유닛 더 나와 있었고, 보이는 두께는 작성 두께의 약 43%였다. 같은 방의 다른 세 발판과 앞선 구역의 화면에서도 원본 스프라이트 치수에 따른 배율을 확인했다. 시계탑 13차 수정으로 분리한 충돌 상자는 그대로 두고, 상자에 붙은 타일 자식의 표시 배율을 양축에서 맞췄다. [23차 실패 화면](../../testresults/run23/screenshots/041-GalleryDistrict-failure.png) · [지형 생성 코드](Assets/Scripts/Story/RepresentativeRoomLayout.cs)

**정적 검증은 10,532건 통과, 실패 0건이다.** 수정 전 코드를 새 검사에 넣으면 작성 지형 435개 중 357개가 실패한다. 수정 후에는 435개 모두 0.005유닛 이내였다. 이번 환경에서 수정본의 Unity 전수 실측과 슬롯 6 완주는 확인하지 못했다. 아래에서 화면 픽셀, Unity가 저장한 상자 좌표, 소스와 원화에서 재구성한 좌표의 출처를 각각 표시했다. [통합 결과](STATIC-VALIDATION.json) · [23차 Unity 결과](../../testresults/run23/report.json)

## 첫 방에서 보이는 면과 밟는 면

23차는 Unity 6000.6.9f1로 실행됐다. 커밋 `2a1bf4af`의 방 데이터·지형 생성·스프라이트 생성·두 검사 파일을 로컬 원본과 Git blob으로 대조했다. `Block`은 이미 화랑의 충돌 상자를 단위 배율 루트에 만들고 `autoTiling`을 끈다. 중심에 붙인 자식 `Plate`에 타일 렌더러를 만들고, 상자와 렌더러의 로컬 크기에 각각 작성 폭·높이를 넣는다. 13차 시계탑에서는 상자 자체가 커졌고, 23차에는 분리 뒤에도 남은 그림 쪽 배율이 드러났다. [QA 커밋의 Block](https://github.com/SooYoungJang/Rewind-Notary-R.E.A./blob/2a1bf4af/Assets/Scripts/Story/RepresentativeRoomLayout.cs#L29-L57) · [시계탑 13차 물리 실측](LEVEL-STEP3-FIX4-REPORT.md)

실패 화면은 높이 720픽셀이고 게임의 직교 카메라 세로 시야는 11유닛이다. 종이 마루와 한 단 위 발판의 윗변 차이는 105픽셀로 작성 높이차 1.6유닛과 맞는다. x=15와 x=28에 놓인 두 표식 중심은 화면 x=493.5, 1343.5픽셀이다. 이 두 점으로 수평축을 보정하면 `paper`가 끝나는 픽셀 경계 x=702는 월드 x≈18.19다. 작성 끝 x=14는 화면 x≈428이다. 한 픽셀은 약 0.015유닛이다. 픽셀 반올림은 당시 그림을 복원하는 데만 사용했다. [실패 화면 원본](../../testresults/run23/screenshots/041-GalleryDistrict-failure.png) · [카메라 설정](Assets/Scripts/GameBootstrap.cs) · [표식과 작성 면](Assets/Resources/Story/gallery.json)

| 화랑 01 타일 | 작성 x 구간 / 밟는 상단 y | 화면에서 읽은 타일 경계 | 수정 전 표시 x 구간 / 상단 y 재구성 |
|---|---|---|---|
| `paper` | [0, 14] / 0 | 오른쪽 702px, 세로 532~542px | [−4.2, 18.2] / −0.10767 |
| `folio` | [11, 24] / 1.6 | 오른쪽 1337px, 세로 427~437px | [7.1, 27.9] / 1.49233 |
| `pigment_table` | [22, 35] / 3.2 | 왼쪽 약 696px, 세로 323~332px | [18.1, 38.9] / 3.09233 |
| `studio_door` | [32, 45] / 1.6 | 왼쪽 1350px, 세로 427~437px | [28.1, 48.9] / 1.49233 |

종이 타일은 192×52픽셀이고 게임이 지정한 밀도는 120픽셀/유닛이다. 한 장은 1.6×0.43333유닛이다. 이 두 계수를 `paper`의 14×0.38에 적용하면 22.4×0.16467이 된다. 중심 (7, −0.19)을 기준으로 계산한 오른쪽 끝과 화면 경계가 1픽셀 이내에서 맞고, 두께도 10.78픽셀로 관측값 11픽셀과 맞는다. 이 배치에서 x=16은 종이 마루처럼 보이지만 `paper`의 접촉 사각형 밖이다. 상단 중앙에 서면 그림 윗변이 접촉면보다 약 0.108유닛 아래에 있다. [스프라이트 생성](Assets/Scripts/Story/OperaFixtures.cs) · [화랑 타일](Assets/Resources/GameArt/backgrounds/gallery-gesso.png)

23차 로그가 기록한 실패 시점은 화랑 시나리오 시작 후 10.755초, 발 좌표 (4.000, 0.038), 접지 전이다. `TerrainWidth.Measure`는 화랑에서 `VerifyGalleryTile`을 먼저 부른다. 예외가 첫 면의 상자 좌표를 결과에 추가하기 전에 발생해 `galleryDistrict.widths`는 빈 배열이다. 위 표의 화랑 상자 수치는 작성값과 생성 코드로 복원한 값이다. 23차가 저장한 `paper`의 원시 `box.bounds`·`tile.bounds` 숫자는 없다. [실패 상태](../../testresults/run23/report.json) · [실측 순서](Assets/Scripts/AutoTest/OperaDistrictProbe.cs) · [실기 판정식](Assets/Scripts/AutoTest/PlaytestObservations.cs)

## 앞선 구역까지 전수 대조

| 구역 | 작성 면 | 수정 전 어긋남 재구성 | 수정 후 정적 어긋남 | Unity 화면 또는 물리 기록 |
|---|---:|---:|---:|---|
| 거점 | 2 | 0 | 0 | 단위 사각형과 상자가 같은 트랜스폼을 사용. 작성 면 2개는 정적 대조 |
| 벨벳 | 76 | 0 | 0 | `balcony` 두께 29px, 작성 0.44유닛이면 28.8px. [005 화면](../../testresults/run23/screenshots/005-Velvet-gallery-fixed.png) |
| 시계탑 | 72 | 72 | 0 | 황동 `drum_floor` 두께 37px, 작성 0.35유닛이면 22.9px. 접촉 로그에서 식별한 17개 면 모두 양끝으로 잰 폭과 작성 폭 일치. [016 화면의 시계탑 1번 방](../../testresults/run23/screenshots/016-VelvetDistrict-start.png) · [접촉 로그](../../testresults/run23/run.log#L2272) |
| 오페라 | 55 | 55 | 0 | `box_door` 두께 8px, 높이 0.126유닛 재구성과 일치. Unity 상자 55개 전수 대조. [019 화면](../../testresults/run23/screenshots/019-OperaDistrict-opera_01_entry.png) |
| 대종루 | 85 | 85 | 0 | `quay` 두께 36px, 높이 0.56유닛 재구성과 일치. Unity 상자 85개 전수 대조. [030 화면](../../testresults/run23/screenshots/030-BellDistrict-bell_01_entry.png) |
| 화랑 | 145 | 145 | 0 | 10개 방이 같은 석고 타일 사용. 01의 4개는 실패 화면 대조. 03의 걸쇠 가교도 포함 |
| **합계** | **435** | **357** | **0** | **47개 방** |

오페라·대종루는 방 ID와 면 ID로 실측 140행을 작성 사각형에 대응시켰다. 왼쪽·아래·폭·높이 네 값의 최대 절대 오차는 오페라 0.00000229, 대종루 0.00000153유닛이었다. 예를 들어 대종루 `quay`의 실측 상자는 x=0~15, 상단 y≈16인데 그림은 x=−4.5~19.5, 상단 y=16.105로 복원된다. 앞선 probe는 콜라이더를 전수 검사했고 렌더러 월드 크기까지 검사한 첫 구역이 화랑이었다. [140개 원시 상자](../../testresults/run23/report.json)

화랑 방별 면 수는 01→08→10→09 순서로 **4, 8, 5, 5, 4, 3, 50, 53, 4, 9**다. 영구면 144개와 `folding_canvas` 1개가 모두 포함된다. 07의 `atelier_0_0`은 폭 6이 9.6으로 보였고, 03의 `folding_canvas`는 폭 9가 14.4로 보이는 배치였다. 시계탑의 경사 2개는 회전 후 월드 사각형으로, 이동판은 생성 위치에서 대조했다. 각 면의 수정 전 표시·수정 후 표시·충돌 사각형은 `level_step6_metrics.terrainBounds.rooms[방][면]`에 [왼쪽, 아래, 폭, 높이] 순서로 수록했다. 기존 probe의 누적 목표는 오페라 55 + 대종루 85 + 화랑 145 = 285개다. 붓으로 생성하는 물감 지지 84곳은 기존의 설치·착지·소거 검사에서 별도로 다룬다. [타일별 전수 결과](STATIC-VALIDATION.json)

## 수정과 영구 검사

게임 변경은 `Block` 한 곳이다. 타일을 지정하고 논리 크기를 설정한 뒤, 루트가 단위 배율이고 경사를 회전시키기 전에 실기 검사와 같은 자식 렌더러의 월드 경계 폭·높이를 읽는다. 자식의 x 배율을 ‘작성 폭 ÷ 그려진 폭’, y 배율을 ‘작성 높이 ÷ 그려진 높이’로 설정한다. 23차 화랑 치수에서 이 값은 각각 0.625와 약 2.30769다. 엔진이 이미 요청한 크기를 그리는 축에서는 배율이 1이다. 렌더러 설정 크기, 물리를 소유하는 루트, 경사·이동·걸쇠 판정은 그 상태로 실기 검사에 들어간다. [수정 코드](Assets/Scripts/Story/RepresentativeRoomLayout.cs) · [Unity Renderer.bounds](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Renderer-bounds.html)

기존 10,082개 검사는 타일 자식 생성, 명시적인 `box.size`, `autoTiling=false`와 작성 데이터상의 착지 경로를 확인했다. 같은 수치를 `visual.size`에 대입한 뒤 화면에서 몇 유닛이 되는지는 재지 않았다. 추가한 정적 검사는 타일 치수·픽셀 밀도·실제 자식 배율 대입식과 개별 면의 사각형에서 월드 경계를 계산한다. XY 크기와 중심 차이의 벡터 길이에 0.005유닛을 적용한다. 수정 전 코드를 돌렸을 때 357개가 실패했고 x만 또는 y만 보정한 대조군도 각각 357개가 실패했다. [정적 대조 코드](tools/validate_terrain_bounds.py)

Unity가 있는 환경에서는 같은 통합 명령에 `--unity-editor <실행파일>`을 주면 임시 프로젝트를 가져와 Edit Mode에서 실제 `BuildSurfaces`를 실행한다. 평소 `AutoTestRunner.Run`도 Play Mode 시작 전에 같은 검사를 실행한다. 각 면의 작성값, 로컬 타일·상자 크기, 배율, 월드 중심·크기를 읽고 화랑에는 기존 `VerifyGalleryTile`을 그대로 호출한다. 가교는 보이는 상태에서 검사한다. 면 하나가 어긋나도 다른 면을 끝까지 측정한 뒤 QA 결과 폴더의 `terrain-bounds.json`에 수치를 쓰고 실패를 반환한다. [Unity 전수 검사](Assets/Scripts/AutoTest/AuthoredTerrainAudit.cs) · [QA 진입점](Assets/Editor/AutoTestRunner.cs)

| 다시 실행한 영구 검사 | 결과 |
|---|---|
| 벨벳 갤러리 걸쇠 | 일반 보행 후 83틱 검 입력, 91틱 절단, 잔여 0 |
| 오페라 웨이브 | 세 곳에서 세팅 9회와 검 입력 3회, 15.16초에 잔여 0 |
| 시계탑 하강·걸쇠 | 07 하강 6/6, 08 하강 5/5. 동쪽 걸쇠 잔여 0 |
| 대종루 하강·걸쇠 | 07·08 고정면 방향쌍 각각 240/240. 03·04 재진입 걸쇠 7+8건 모두 절단 |
| 화랑 | 두 관람로 일반 점프·보행, 30회·80회 성장, 84개 물감 위치, 영구 복귀면 검사 통과 |

작업공간에서 `python3 -B tools/validate_gamedev.py --output unity/RewindNotary/STATIC-VALIDATION.json`을 실행했다. 기준선 10,082개 검사명이 중복 횟수까지 모두 남았고 450개가 추가됐다. 스토리·대사·행 데이터와 기존 실기 판정은 원본과 동일하다. 무진전 18초·구역 정체 120초·구역 제한 1,500초, 지형 오차 0.005, 필수 경로의 벽 점프 제한을 유지했다. [통합 결과](STATIC-VALIDATION.json)

로컬에는 Unity 실행 파일이 없고, 지정 QA PC의 SSH 프록시는 연결 소켓 생성 시 `PermissionError [Errno 1] Operation not permitted`를 반환했다. 수정본의 C# 컴파일과 원시 경계 전수 측정, 슬롯 6의 복귀점 10곳·화폭 조각 유효 사용·관람로·지름길 시간·최종 슬롯 판독은 미측정이다. 23차의 화랑 접촉 기록은 0/10이며 전체 QA 결과는 FAIL이다. [23차 원본](../../testresults/run23/report.json)

## 변경 파일

| Unity 저장소 상대 경로 | 내용 |
|---|---|
| `Assets/Scripts/Story/RepresentativeRoomLayout.cs` | 충돌 상자가 있는 타일 자식의 표시 크기 보정 |
| `Assets/Scripts/AutoTest/AuthoredTerrainAudit.cs`, `Assets/Scripts/AutoTest/AuthoredTerrainAudit.cs.meta` | Edit Mode 전수 실측과 타일별 결과 |
| `Assets/Editor/AutoTestRunner.cs` | QA 시작 전 실측, 지형만 검사하는 진입점 |
| `tools/validate_terrain_bounds.py` | 정적 월드 사각형·23차 화면 대조, Unity 결과 비교 |
| `tools/validate_level_step6.py`, `tools/validate_gamedev.py` | 통합 검증 및 Unity 실행 옵션 연결 |
| `STATIC-VALIDATION.json`, `LEVEL-STEP6-FIX-REPORT.md` | 전체 검사 결과와 수정 기록 |

푸시 매니페스트에는 위 9개 경로가 들어 있다. 모든 경로는 Unity 저장소 루트 기준이며 마지막 경로 뒤에도 NUL이 있다. [푸시 매니페스트](../../push-paths-level-step6-fix.bin)
