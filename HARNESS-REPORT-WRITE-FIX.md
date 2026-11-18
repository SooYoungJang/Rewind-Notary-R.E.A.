# 하네스 리포트의 일시적 파일 잠금 처리

12차 실행(커밋 `3b12a304`)은 `NewSlot`을 통과한 직후 `Attic` 시작 상태를 기록하다 중단됐다. `run.log`에는 두 단계의 전환과 “바꿀 파일을 제거할 수 없습니다”가 연이어 남았다. `report.json`의 `errors`에는 `StartMeasurement`가 호출한 `AutoTestReport.Write`의 `File.Replace`에서 예외가 발생한 스택이 있다. 당시 교체는 한 번만 시도했다. 이후에는 같은 경로에 최종 실패 리포트를 쓸 수 있었으므로 일시적 파일 경합과 일치한다. 잠금을 건 프로세스는 로그에서 식별할 수 없다.

## 기록 방식과 대기 상한

`Write`는 호출마다 JSON을 한 번 직렬화하고 같은 디렉터리의 `report.json.tmp`에 쓴다. 임시 파일 쓰기 도중 `IOException`이 나면 같은 JSON을 처음부터 다시 쓴다. 쓰기가 끝난 뒤에는 완성된 임시 파일을 재사용한다. `report.json`이 있으면 `File.Replace`로 원자적으로 교체하고, 최초 생성에는 `File.Move`를 사용한다.

첫 시도는 즉시 실행한다. 파일 I/O에서 `IOException`이 발생하면 **50ms 간격으로 최대 5회 재시도**한다. 임시 파일 쓰기와 교체가 같은 예산을 써서 호출당 최대 6회 시도하며, 명시적 대기 합계는 **250ms**다. 성공하면 즉시 반환한다. 이 상한은 파일 I/O 자체의 소요 시간을 제외한 값이다.

에디터 실행기의 초기·실패 기록, 드라이버의 단계 시작·체크포인트·종료 준비 기록, 런타임의 최종 기록이 모두 이 메서드를 사용한다. 하네스에서 다른 원자적 교체 지점은 발견되지 않았다. 게임 설정과 슬롯 저장의 교체는 각각 기존 `GameSettings.cs`와 `SkillSaveStore.cs`가 처리한다.

## 재시도 소진 시 동작

- 여섯 번째 시도에서는 `catch` 필터가 거짓이 되어 원래 `IOException`과 스택이 호출자에게 전파된다. 다른 예외는 첫 발생 시 전파된다.
- 단계 기록이 실패하면 `AutoTestDriver.Update`가 메시지와 스택을 `errors`에 `harness`로 추가한다. 12차처럼 진행 중이던 단계는 `Stop`이 실패 체크포인트를 남기고 `fail`로 기록한 뒤 `StartDrain(false)`에서 실패 리포트 쓰기를 시도한다.
- 최종 쓰기까지 실패하면 `AutoTestRuntime.Finish`가 예외를 Unity 로그에 남기고 종료 코드 1을 사용한다. 잠금이 끝까지 유지되면 디스크의 JSON은 마지막으로 교체에 성공한 시점의 내용일 수 있다.

## 정적 검증

작업공간 루트에서 `python3 -B tools/validate_gamedev.py --output unity/RewindNotary/STATIC-VALIDATION.json`을 실행했다. **7,840건 통과·0건 실패**로 기존 7,837건의 검사명과 각 중복 횟수를 모두 보존했다. 추가한 3건은 임시 파일 쓰기와 원자적 교체, 재시도 상한과 예외 전파, 드라이버의 실패 기록과 최종 쓰기 실패 시 종료 코드를 검사한다. 18초·120초 정체 판정과 단계별 데드라인은 기존 코드를 사용한다. Windows Unity에서 파일 잠금을 재현하거나 실측을 다시 실행하지는 않았다. [전체 검증 결과](STATIC-VALIDATION.json)

## 변경 파일

푸시 매니페스트에는 Unity 저장소 루트 기준으로 다음 네 파일을 수록했다.

| 경로 | 변경 내용 |
| --- | --- |
| `Assets/Scripts/AutoTest/AutoTestReport.cs` | 리포트 쓰기와 원자적 교체 재시도 |
| `tools/validate_autotest.py` | 기록 경로 정적 회귀 검사 3건 |
| `STATIC-VALIDATION.json` | 전체 정적 검증 결과 |
| `HARNESS-REPORT-WRITE-FIX.md` | 12차 실패 원인, 재시도 정책과 검증 결과 |

작업공간 루트의 `push-paths-harness-report-fix.bin`은 각 경로 뒤에 NUL을 하나씩 두며 매니페스트 자체는 목록에서 제외한다.
