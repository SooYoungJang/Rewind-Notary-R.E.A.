# 자동 플레이테스트 하네스 2차 수정 — 에디터 Search 예외 분류

Unity 6000.6.9f1의 2차 PC 실행에서 `NewSlot`은 0.616초에 `Unity or harness error`로 실패했다. 유일한 오류였던 Search 시작 예외는 수정한 분류 규칙으로 원본 스택을 정적 재생하면 `warnings`에 해당한다. 전체 정적 검증은 **7,125건 통과, 0건 실패**다. 수정본의 Unity 컴파일과 Play 실행은 PC 재확인 항목이다. [2차 실행 원본](../../testresults/run2/report.json)

## 경고가 되는 조건

`OnLog`는 에디터에서 수신한 `Error`, `Exception`, `Assert`의 스택을 순수 함수 `IsEditorSearchStartupNoise(string stack)`에 전달한다. 아래 두 조건을 모두 만족해야 경고다. 메인 스레드 콜백과 백그라운드 큐에서 꺼낸 로그는 같은 판정을 거친다. [수집기와 명시적 서명 목록](Assets/Scripts/AutoTest/AutoTestRuntime.cs)

1. 스택에 `UnityEditor.Search.SearchInit.IndexationOnStartup` 프레임이 있다.
2. 비어 있지 않은 **모든 줄**이 등록한 메서드 서명과 대소문자까지 일치한다. 비교 시 들여쓰기, 앞의 `at `, 빈 인자 괄호, Unity의 `(at <32자리 16진수>:줄번호)` 위치 표기를 허용한다. 인자가 표기된 메서드는 인자 문자열까지 비교한다.

원본 스택에는 아래 8개 프레임이 순서대로 있다. 검증용 픽스처에는 제네릭 표기, 인자, 위치 정보와 마지막 줄바꿈까지 원본 그대로 담았다. [원본 스택 픽스처](../../tools/fixtures/autotest-search-6000.6.9f1.stack)

| 프레임 | 판정 근거 |
|---|---|
| 1~4 | `List<T>.get_Item`, `SearchDatabase+<EnumerateAll>d__78.MoveNext`, `Enumerable.TryGetFirst`, `Enumerable.First`는 Search 데이터베이스 열거 중 관측된 서명이다. |
| 5~6 | `SearchDatabase.GetDefaultSearchDatabase`에 이어 필수 시작 콜백인 `SearchInit.IndexationOnStartup`이 있다. |
| 7~8 | `EditorApplication.Internal_CallDelayFunctions`와 `StackTraceUtility:ExtractStringFromExceptionInternal`은 등록된 호출·스택 출력 프레임이다. |

게임이나 하네스 메서드가 앞·중간·끝 어디에 있어도 로그 전체를 `errors`에 보낸다. 빈 스택, 미등록 Search 메서드, 판독할 수 없는 줄, 프로젝트 소스 위치도 오류다. 스탠드얼론의 세 로그 유형과 하네스가 직접 기록하는 저장·캡처·종료 오류는 기존 실패 경로를 따른다.

## report.json 스키마 v2

`warnings[]`를 추가했다. `errors[]`와 같은 `RunError` 형식으로 UTC 수집 시각 `utc`, 원래 로그 유형 `type`, 메시지 `message`, 원본 `stack`을 보존한다. 두 배열 모두 빈 배열로 초기화한다. 일반 `LogType.Warning`은 계속 `run.log`에 기록한다. 두 배열 중 단계 실패와 최종 종료 코드에 반영되는 것은 `errors`다. [보고서 필드와 항목 생성](Assets/Scripts/AutoTest/AutoTestReport.cs) · [단계 실패 판정](Assets/Scripts/AutoTest/AutoTestDriver.cs)

필드 추가와 함께 같은 Search 예외의 실패 의미도 달라져 `schemaVersion`을 1에서 2로 올렸다. 과거 v1 결과를 읽는 도구는 없는 `warnings`를 빈 목록으로 취급하고, 당시의 `errors`와 종료 코드를 그대로 해석하면 된다.

## 정적 검증

작업공간 루트에서 아래 명령으로 검증했다. 수정 전에도 같은 검증기를 실행해 결과를 별도 파일에 저장하고, 검사명별 통과 횟수를 대조했다. [하네스 검증기](../../tools/validate_autotest.py)

```sh
python3 -B tools/validate_gamedev.py --output /tmp/rewind-autotest-fix2-after.json
```

| 검사 | 결과 |
|---|---|
| 전체 | 7,107 → **7,125 통과**, 실패 0, 종료 코드 0 |
| 기준선 회귀 | 기존 통과 7,107건 중 누락 0건. 하네스 외 검사군 지표 변화 0건 |
| 하네스 | 44/44 → **62/62**. 추가 18건은 구조·연결 6건, 경고 사례 2건, 오류 사례 10건 |
| 데이터 재생 | C#의 프레임 정규식과 허용 목록을 읽어 PC 원본과 요청문의 축약형(CRLF)을 검사했다. 둘 다 경고 조건을 충족했다. |
| 변조 주입 | 하네스 소스를 메모리에서 한 곳씩 바꿔 검증기를 재호출했다. 독립 변조 **12/12 검출** |

검출한 변조는 분류 함수·시작 콜백 서명·Search 열거기 서명·PC의 LINQ 서명 삭제(4건), 미등록 프레임 건너뛰기·시작 콜백에서 조기 승인·게임 메서드를 허용 목록에 추가(3건), 에디터 한정 조건 제거·미분류 오류를 경고로 전송·경고를 오류 배열에 저장(3건), 경고가 있을 때 단계나 최종 실행을 실패시키는 변경(2건)이다.

## PC 재실행 확인 지점

1. Unity **6000.6.9f1**에서 아래 명령을 실행한다. 새 실행 디렉터리는 부모 에디터가 출력하는 `AUTOTEST_RESULT` 경로로 찾는다.

   ```text
   Unity -batchmode -projectPath "<Unity 프로젝트 루트>" -executeMethod AutoTestRunner.Run -quit
   ```

2. `report.json`에서 `schemaVersion == 2`와 두 배열을 확인한다. 같은 Search 예외가 재발하면 콜백 발생 건마다 `warnings`에 한 건씩, `type == "Exception"`과 원본 스택이 남아야 한다. `errors`에서 같은 항목은 0건이어야 한다. 재발하지 않은 실행에서는 빈 `warnings`도 정상이다.
3. 다른 오류가 없는 실행에서 `steps[0]`의 `NewSlot` 통과와 다음 `Attic` 시작을 확인한다. 별도의 실제 오류가 발생했다면 해당 `errors` 항목, 실패 단계와 종료 코드 1을 대조한다. 완주 시에는 9단계 모두 `pass`, `errors` 0건, 프로세스와 JSON의 종료 코드가 모두 0이어야 한다.
4. 새 디렉터리의 체크포인트 PNG와 `isolation.unchanged`, `isolation.tempRemoved`를 확인한다. 완주 시 `isolation.verifiedFinalSlot`도 `true`여야 한다.
