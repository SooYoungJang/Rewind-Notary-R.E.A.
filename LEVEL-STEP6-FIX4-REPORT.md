# 레벨 6단계 수정 4 — 자동 테스트 워커 상한 75분

QA 26차에서 화랑이 진행 중일 때 부모 에디터가 워커를 종료했다. 이번 수정은 워커의 임포트와 Play Mode 실행에 **45분에서 75분으로** 시간을 늘린다. 기존 영구 검사와 정적 검증은 **10,984건 통과, 실패 0건**이다. 게임 코드 변경은 0건이다. [러너](Assets/Editor/AutoTestRunner.cs) · [정적 검증 결과](STATIC-VALIDATION.json)

## 75분의 근거

| QA 26차 구간 | 소요 시간 |
|---|---:|
| 본편 9단계 | 386.6초 (약 390초) |
| 벨벳 | 368.4초 |
| 시계탑 | 473.7초 |
| 오페라 | 658.5초 |
| 대종루 | 736.2초 |

본편을 약 390초로 잡으면 화랑 직전까지 `390 + 368.4 + 473.7 + 658.5 + 736.2 = 2,626.8초`, 약 **43.8분**이다. 여기에 화랑 완주 예상 시간 약 12분을 더하면 약 **55.8분**이 필요하다. 75분 상한에서는 워커 에디터 시작과 임포트, 실행 시간 편차에 약 **19.2분**의 여유가 있다. [26차 구간별 실측](../../testresults/run26/report.json)

커밋 `17c8d054`의 26차 기록에서 본편 9단계, 조작감, 파일럿, 앞선 네 구역 probe가 통과했다. 화랑의 마지막 로그는 probe 시작 60.2초 뒤로, `gallery_03_easel_bridge`에서 봇이 `glass_sill`에 착지해 있었고 표적 2개가 남았다. 이어서 러너가 45분 초과 예외를 기록했다. 25차의 ‘경로 없음’은 이 시점까지 재발하지 않았다. 수정 후 Unity Play Mode에서의 화랑 완주는 미검증이다. [26차 로그](../../testresults/run26/run.log) · [25차 수정 근거](LEVEL-STEP6-FIX3-REPORT.md)

## 상한 적용 지점

- `AutoTestRunner.WorkerTimeoutMinutes`를 정수 상수 75로 정의했다(14행). `Run()`은 워커를 시작한 뒤 `WaitForExit(WorkerTimeoutMinutes * 60 * 1000)`으로 **4,500,000ms**를 기다린다(50행). 초과하면 기존 순서대로 `Kill()`을 호출하고 종료 확인에 최대 10초를 쓴 뒤, 같은 상수로 75분 초과 메시지를 생성한다(52–54행). 예외 처리에서 실행 중인 probe를 실패로 기록하고 종료 코드 1을 반환한다. [AutoTestRunner.cs](Assets/Editor/AutoTestRunner.cs)
- 기존 영구 검사가 대기 호출에서 `45`를 찾던 조건을 갱신했다. 이제 75분 상수, 밀리초로 환산한 대기 호출, 시간 초과 분기 안의 강제 종료·10초 종료 확인·예외 문구를 한 검사에서 확인한다. [validate_autotest.py](tools/validate_autotest.py)

## 판정 기준 대조

| 항목 | 작업 전후 코드에서 확인한 조건 |
|---|---|
| 구역 제한 1,500초 | 벨벳·시계탑·오페라·대종루·화랑 모두 `now - entered > 1500`이면 실패한다. [AutoTestDriver.cs](Assets/Scripts/AutoTest/AutoTestDriver.cs) |
| 무진전 18초 | 위치나 관측 상태의 진전 없이 `now - lastProgress > 18`이면 실패한다. [AutoTestDriver.cs](Assets/Scripts/AutoTest/AutoTestDriver.cs) |
| 구역 정체 120초 | `DistrictIdleLimit = 120`이다. 접촉 수 증가, 같은 방의 표적 처치, 유효 사용으로 갱신하는 구역 진행 시계가 120초에 도달하면 실패한다. [AutoTestDriver.cs](Assets/Scripts/AutoTest/AutoTestDriver.cs) |
| 지형 허용 오차 0.005 | 감사에서 작성값·충돌체·렌더링 경계의 중심과 크기를 대조한다. 벡터 차이의 제곱 한계는 `0.000025 = 0.005²`이다. 플레이 중 지형 대조에도 같은 허용 오차를 적용한다. [AuthoredTerrainAudit.cs](Assets/Scripts/AutoTest/AuthoredTerrainAudit.cs) · [PlaytestObservations.cs](Assets/Scripts/AutoTest/PlaytestObservations.cs) |
| 시작 전 지형 감사 | 부모의 `Run()`이 워커 시작 전에 `AuditAuthoredTerrain`을 호출한다. 감사 결과를 저장하고 오류가 하나라도 있거나 측정한 면이 0개면 예외로 중단한다. QA 26차 실측은 435개, 오류 0개다. [감사 호출](Assets/Editor/AutoTestRunner.cs) · [감사 구현](Assets/Scripts/AutoTest/AuthoredTerrainAudit.cs) · [26차 지형 실측](../../testresults/run26/terrain-bounds.json) |
| 접촉·유효 사용 통과 기준 전체 | 다섯 구역 probe와 공통 드라이버의 파일 해시가 모두 일치했다. 예를 들어 화랑은 지정 바닥 접촉·저장 성공·저장 플래그를 확인하고, 물감 지지는 실제 콜라이더 접촉을 요구한다. 유효 사용은 중복 활성화를 거부하며 지정 방에서의 30회·80회 도달과 최종 80회 이상을 확인한다. [구역별 probe와 드라이버](Assets/Scripts/AutoTest) · [화랑 접촉·유효 사용 판정](Assets/Scripts/AutoTest/GalleryDistrictProbe.cs) |

작업 전후 SHA-256을 대조했다. `Assets/Scripts` 아래 C# 파일 113개, 스토리 JSON 6개, 현지화 JSON 9개가 모두 일치했다. 게임 코드·스토리 텍스트·대사·행 데이터 변경은 0건이다.

## 재실행과 변경 파일

작업공간 루트에서 `python3 -B tools/validate_gamedev.py --output <임시 결과 경로>`를 실행했다. **10,984건 통과, 실패 0건, 기존 경고 25건**으로 기준선을 유지했다. 재실행 JSON 전체가 저장된 결과와 바이트 단위로 일치했다. [STATIC-VALIDATION.json](STATIC-VALIDATION.json)

| Unity 저장소 상대 경로 | 변경 내용 |
|---|---|
| `Assets/Editor/AutoTestRunner.cs` | 부모 에디터의 워커 대기 상한과 예외 문구에 같은 75분 상수 적용 |
| `tools/validate_autotest.py` | 기존 워커 검사에서 75분 상한과 시간 초과 처리 확인 |
| `LEVEL-STEP6-FIX4-REPORT.md` | 26차 실측, 상한 산정, 판정 기준 및 검증 결과 기록 |

작업공간 루트의 푸시 매니페스트에는 위 세 경로만 담았다. 각 경로 뒤에 NUL이 있으며 전체 크기는 86바이트다. [푸시 매니페스트](../../push-paths-level-step6-fix4.bin)
