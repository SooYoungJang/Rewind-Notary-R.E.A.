# 자동 플레이테스트 하네스 1차 컴파일 오류 수정

Unity 6000.6.9f1에서 CS0619를 일으킨 `GetInstanceID()` 두 호출을 제거했다. 보스 대련을 관측하는 `AutoTestDriver.cs`만 수정했다. 전체 정적 검증은 **7,107건 통과, 실패 0건**이며, VM에 Unity 에디터가 없어 두 버전의 실제 컴파일과 Play Mode 실행은 PC에서 확인해야 한다.

## 대련 식별과 버전 호환성

대련을 시작할 때 게임은 상대에게 `BenefactorTrialOpponent` 컴포넌트를 붙이고, 종료할 때 그 컴포넌트를 파괴한다. 하네스의 `Bout` 속성은 이 컴포넌트를 반환한다. 드라이버는 시도 시작 시 해당 참조를 `observedBout`에 보관하고, `EndBout`에서 해제한다. [대련 생성·종료](Assets/Scripts/Story/RepresentativeArea.cs) · [하네스 관측 속성](Assets/Scripts/AutoTest/PlaytestObservations.cs)

같은 컴포넌트가 연속 프레임에 보이면 한 시도로 계속 집계한다. 컴포넌트가 사라지면 기존 시도를 닫고, 다른 컴포넌트로 교체되면 같은 프레임에 기존 시도를 닫고 새 시도를 한 번 연다. 기존 `bout == null` 검사를 유지해 파괴된 Unity 객체도 종료로 처리한다. 6000.0과 6000.6 모두 Unity 객체 비교를 지원하므로 버전 분기나 `GetEntityId()`가 필요하지 않다. 프로젝트 기준 버전은 6000.0.65f1이다. [Unity 6000.0 객체 비교](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/object/op-equality) · [Unity 6000.6 객체 비교](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/object/op-equality) · [프로젝트 버전](ProjectSettings/ProjectVersion.txt)

## GetInstanceID 전수 검색

Unity 프로젝트 루트에서 숨김 파일과 무시 파일을 포함해 전체 텍스트를 검색했다. 수정 전 결과는 아래 두 곳뿐이었다.

| 수정 전 파일·줄 | 용도 | 수정 후 |
|---|---|---|
| `Assets/Scripts/AutoTest/AutoTestDriver.cs:149` | 현재 대련 ID와 관측 중인 ID를 비교해 상대 교체 감지 | 150행에서 `bout != observedBout` 비교 |
| `Assets/Scripts/AutoTest/AutoTestDriver.cs:156` | 새 시도에 사용할 대련 ID 보관 | 157행에서 `observedBout = bout` 대입 |

하네스 밖 호출은 **0건**이다. 수정 후 프로젝트 전체 C# 소스에 남은 호출도 **0건**이다. 아래 명령은 Unity 프로젝트 루트에서 재현할 수 있다. 첫 명령에는 이 보고서의 설명도 포함된다.

```sh
rg -n --hidden --no-ignore -g '!**/.git/**' '\bGetInstanceID\b' .
rg -n --hidden --no-ignore -g '!**/.git/**' -g '*.cs' '\bGetInstanceID\b' .
```

`Assets/Scripts/AutoTest/FirstDepartureBot.cs:36`의 CS0618 경고는 유지했다. 여러 `GameBootstrap`이 있을 때 `FindAnyObjectByType`가 기존 호출과 같은 대상을 선택한다는 보장이 없기 때문이다.

## VM 검증

작업공간 루트에서 `python3 -B tools/validate_gamedev.py --output /tmp/rewind-fix1-AHNKKvKu/validation.json`을 실행하고 기존 기준선과 통과 항목을 대조했다. 하네스 검사 함수도 별도로 호출했다.

| 검사 | 결과 |
|---|---|
| 전체 정적 검증 | 7,107 통과 / 0 실패, 종료 코드 0. 기준선에서 추가·누락된 통과 항목 0건 |
| 하네스 전용 `validate_autotest.py` | 44/44 통과 |
| 기존 입력 / 세이브 / 흐름 | 373/373 · 1,589/1,589 · 25/25 통과 |

## PC 재실행 확인 지점

1. 원격 오류가 발생한 **6000.6.9f1** 에디터로 아래 명령을 실행한다. 기준 버전 **6000.0.65f1**에서도 해당 에디터로 같은 소스를 컴파일한다. 에디터 로그에서 CS0619 두 건이 사라졌는지, 새 실행의 `TestResults/autotest-*/run.log`에서 `AUTOTEST begin NewSlot`까지 도달하는지 확인한다. [Windows PowerShell 실행 예시](AUTOTEST-REPORT.md#실행)

   ```text
   Unity -batchmode -projectPath "<Unity 프로젝트 루트>" -executeMethod AutoTestRunner.Run -quit
   ```

2. 미하일 대련 후 같은 실행의 `report.json`에서 `bossAttemptCount`와 `bossAttempts` 배열 길이가 같은지 확인한다. 각 실제 시도에는 `boss-start`와 `boss-end-<결과>`가 하나씩 있어야 한다. 재시도했다면 이전 기록은 `retry`로 닫히고 다음 대련이 한 건 추가돼야 한다. 승리한 시도는 `victory`로 닫혀야 한다.
3. 완주 판정은 9단계 모두 `pass`, 프로세스와 보고서의 종료 코드 0이다. 세이브 격리는 `isolation.unchanged`, `isolation.verifiedFinalSlot`, `isolation.tempRemoved`가 모두 `true`인지 확인한다.
