# 자동 플레이테스트 3차 수정 — Investigation 반사 계측판

**원인은 봇의 기본검 타격 거리였다.** 3차 PC 실행에서 반사 계측판을 켜는 첫 `F`는 성공했다. 봇은 판 바로 앞에서도 `J`를 눌렀고, 기본검의 전진 동작 뒤에 검사하는 칼날 범위에서 판이 벗어날 수 있었다. 수정 범위는 고정된 장면 표적을 치는 봇 입력과 해당 검증기다. 게임 트리거는 지정된 입력으로 도달 가능하다. [3차 PC 보고서](../../testresults/run3/report.json) · [실패 화면](../../testresults/run3/009-Investigation-failure.png)

## exp3의 완료 조건

본편 로더가 읽는 `Story/representative.json`의 `mid_invest / 11_exp_step3_mirror`는 `mid_invest_10_read` 후에 열린다. `hub_workroom`에서 테오 대사를 확인하면 다음 세 행동이 차례로 활성화된다. 세 행동 모두 같은 `reflection_plate`(x = 19)를 가리킨다. exp2에서 사용한 `calibration_target`은 x = 16이다. `Investigation/catalog.json`은 단서·기록 열람을 정의하며, 그 파일의 월드 조사 지점은 `prototype`에서 생성된다. [본편 로더](Assets/Scripts/Story/RepresentativeScript.cs) · [장면과 방 데이터](Assets/Resources/Story/representative.json) · [조사 카탈로그](Assets/Resources/Investigation/catalog.json) · [월드 생성 분기](Assets/Scripts/GameBootstrap.cs)

| 순서 | 플레이어 행동 | 게임 판정 |
|---|---|---|
| 1 | `use:reflection_plate` — `F`로 판 가동 | 지상에서 생존하고 공격·경직·방어·기술 동작에 막히지 않은 상태로, 몸과 (19, 0.3)의 거리가 1.25 이내일 때 조사 키를 새로 누른다. 공격 등과 동시에 누르면 조사 입력은 무효다. |
| 2 | `strike:reflection_plate` — 판을 향해 기본검 `J` | 실제 검격의 `BasicStrikeDelivered` 시점에 `OnStrike`가 칼날의 `Bounds.Contains((19, 1, 0))`를 검사한다. 접촉하면 즉시 마지막 행동으로 넘어간다. |
| 3 | `use:reflection_plate` — 공격 회복 후 `F`로 계측기 분리 | 첫 조사와 같은 입력 조건에서 `FinishAction`이 임시 휴면 기술을 복구한다. 마지막 행동이므로 `Complete`가 `exp_step3_complete`와 `workroom_mount_floor` 체크포인트를 저장한다. |

작업실은 적 목록이 비어 있고 투사체 계측소도 0개다. 발사기는 구역 실험의 투사체 계측소에 생성된다. 이 실험에서 반사 대조를 진행시키는 이벤트는 기본검 접촉이다. 은실 시전이나 패링 반사 이벤트는 요구하지 않는다. `BotKeyboard`는 프레임마다 누름과 뗌을 구별하므로 두 번의 독립된 `F`와 그 사이의 `J`, 위치를 맞추는 `A/D`를 기존 게임 입력 경로로 전달할 수 있다. [실험 트리거와 저장](Assets/Scripts/Story/RepresentativeArea.cs) · [발사기 생성 조건](Assets/Scripts/Story/DistrictRoomLayout.cs) · [키 매핑](Assets/Scripts/Shell/GameInput.cs) · [가상 키보드](Assets/Scripts/AutoTest/BotKeyboard.cs)

## 3차 실행 근거와 수정

- **PC 기록.** Unity 6000.6.9f1, 스키마 v2에서 6/9단계가 통과했다. Investigation은 21.593초에 18초 정체 감지로 종료됐다. 마지막 상태는 `Playing`, `strike:reflection_plate`, 레아 (18.83999, 0.005), 지상, 체력 6이었다. Investigation 전체에서 기본검 활성화 16회, 유효 사용 0회를 기록했다. 장면 표적 접촉은 유효 사용 횟수를 올리는 판정과 별개다. HUD의 ◆도 수평거리 1.25 이내라는 표시다. Search 예외는 경고 1건, 오류는 0건이었다. [실행 원본](../../testresults/run3/report.json) · [HUD 거리 표시](Assets/Scripts/Story/RepresentativeRoomLayout.cs)
- **기존 입력.** 봇은 매 프레임 현재 행동의 표적을 찾았다. 첫 `F`를 위해 수평거리 0.38 이내까지 걸어간 뒤, 같은 판에 전투용 `Strike → Aim`을 사용했다. `Aim`의 상한은 1.15였고 타격에 필요한 최소 거리는 없었다. 거리 0.06 이내에서는 방향 확인도 생략했다. 이 실행의 기본검은 시작 120회와 선행 단계 유효 사용 108회를 합쳐 228회로, 2단계다. 초격은 0.16초에 닿고 칼날 범위는 그때의 레아 전방 0.05~1.75다. 검을 휘두르며 먼저 약 0.25(2.5 × 0.1초) 전진하므로, 실패 좌표에서 오른쪽을 향해 공격을 시작하면 0.16 앞의 판을 지나친다. 실패 스냅샷에는 개별 타격 프레임의 좌표·방향이 없어 이 계산은 관측된 근접 상태에서 가능한 헛치기를 재현한 것이다. [기본검 단계별 전진량](Assets/Resources/Skills/catalog.json) · [검격 시점과 칼날 범위](Assets/Scripts/CombatMotor2D.cs)
- **수정 입력.** 장면의 `strike:`는 전용 `StrikeStoryPoint`를 사용한다. 너무 가깝거나 가까운 거리에서 등을 보이면 바깥으로 걸어 나갔다가 표적 쪽으로 돌아선다. 공격 중에는 대기하고, 지상에서 표적이 전방 0.65~1.40에 있을 때만 `J`를 누른다. 예를 들어 x = 18에서 오른쪽으로 초격을 시작해 0.25 전진해도 판은 전방 0.75에 있어 판정 안에 든다. 이후 게임이 마지막 `use:`를 제시하면 봇이 다시 접근해 `F`를 누른다. 전투 표적과 보스는 기존 조준 경로를 사용한다. [수정된 봇](Assets/Scripts/AutoTest/FirstDepartureBot.cs)

## 검증

| 항목 | 결과 |
|---|---|
| 전체 정적 검증 | 수정 전 7,125 통과 / 0 실패 → 수정 후 **7,135 통과 / 0 실패**. 기존 통과 항목 누락 0건. |
| 하네스 | 62/62 → **72/72**. 추가 10건이 장면 순서와 트리거 연결, 봇의 키·거리·방향, 기본검 4개 콤보 판정과 최대 전진량, 접근 경로를 검사한다. |
| 작업실 이동 모형 | 실패 시점 거리 ±0.1600094, 표적과 겹친 위치 등을 포함한 시작 위치 7개 × 방향 2개 × 입력 갱신당 물리 틱 1·2·5개, **42/42**에서 전방 타격 거리에 도달했다. |
| 변조 주입 | 메모리에서 입력을 한 곳씩 바꿔 실제 하네스 검증기를 재호출했다. **16/16 검출**. 기존 조준 복귀·표적 오지정·공격/조사 키 교체(4), 공격 중 재입력·방향/최소거리 무시·후퇴/재접근 오류(5), 전진량·최대 사거리·물리 틱에 맞지 않는 거리 설정(3), 타격점 높이·기술 복구 시점·마지막 분리 행동 변조(3), 18초 감시 완화(1). |

재현 명령은 작업공간 루트에서 `python3 tools/validate_gamedev.py --output /tmp/rewind-autotest-fix3.json`이다. 위 수치는 소스·데이터·이동 모형의 결과다. 이 환경에는 Unity 에디터가 없어 수정본의 컴파일과 Play Mode 완주는 PC 확인 항목으로 남는다. [확장한 검증기](../../tools/validate_autotest.py) · [18초 정체 감지](Assets/Scripts/AutoTest/AutoTestDriver.cs)

## PC 재실행 확인 지점

Unity 6000.6.9f1에서 다음 명령을 실행하고, 부모 에디터가 출력한 `AUTOTEST_RESULT` 경로의 새 `report.json`을 확인한다.

```text
Unity -batchmode -projectPath "<Unity 프로젝트 루트>" -executeMethod AutoTestRunner.Run -quit
```

1. **Investigation 통과.** `SafeReturn` 뒤 반사 계측판의 `F → J 접촉 → F`가 끝나고, exp4와 레아의 마지막 대사를 확인한다. `steps[]`에서 `id`가 `Investigation`인 항목의 `status`는 `pass`, `endState.flags`에는 `exp_step1_complete`부터 `exp_step4_complete`와 `investigation_complete`가 있어야 한다. 통과 판정은 두 단서 `route_ledger`, `route_waveform` 보유도 확인한다.
2. **Lift 통과.** 미하일 재회 후 `mikhail_support_first_granted`, `luka_signalled`, `luka_boarded`, `luka_lift_complete`, `scene_floor:hub:lift_arrival_exit`가 기록되고 구역 선택 화면에서 `clock`이 선택되어 단계가 `pass`여야 한다.
3. **DistrictTwo까지 완주.** 같은 `slot-1`이 `clock_01_entry`에 도착하고 9단계 모두 `pass`여야 한다. 스키마 v2, `errors` 0건, JSON과 프로세스 종료 코드 0, `isolation.verifiedFinalSlot`·`unchanged`·`tempRemoved`가 모두 참인지 확인한다. 동일한 Search 시작 예외가 재발하면 `warnings`에 기록된다. [단계별 판정](Assets/Scripts/AutoTest/FirstDepartureBot.cs) · [최종 슬롯 읽기 검증](Assets/Scripts/AutoTest/AutoTestRuntime.cs)
