# 이동 중 지상 공격

## 목표

이동 키를 누른 채 지상에서 검을 휘둘러도 플레이어가 입력 속도로 계속 움직이고, 공격 도중 반대로 입력하면 그 방향으로 이동하도록 한다. 키를 놓고 공격할 때는 검술 단계별 전진 스텝을 유지한다.

## 변경

- `CombatMotor2D.Move`에서 플레이어의 지상 공격 속도를 항상 스텝 속도 또는 0으로 대체하던 두 줄을 교체했다. 입력 속도의 절댓값이 기존 공중 조향 기준과 같은 0.01유닛/초를 넘으면 부호와 크기를 그대로 쓴다. 그 이하에서는 기존 `stepSeconds`, `stepSpeed`, `Facing`으로 스텝을 계산한다. 주석 삭제는 없다. [이동 코드](Assets/Scripts/CombatMotor2D.cs)
- 다락방 FeelProbe의 기존 공중 정지 검사 뒤에 `feel.movingAttack`을 추가했다. 착지 후 D로 최고 속도에 도달하면 J를 한 프레임 누르고 공격이 끝날 때까지 D를 유지한다. 매 물리 틱이 끝난 뒤 바닥 접촉과 전진 속도를 확인한다. 보고서에는 공격 직전 속도(`startSpeed`), 공격 중 최저 속도(`minSpeed`), 관측한 틱 수(`physicsTicks`)를 기록한다. 측정값이 없거나 한 틱이라도 하한에 못 미치면 실패한다. [FeelProbe](Assets/Scripts/AutoTest/FeelProbe.cs) · [보고서 합격 조건](Assets/Scripts/AutoTest/AutoTestReport.cs)
- 전체 정적 검증에 이동 공격 재생을 연결했다. 과거 소스 해시를 검사할 때만 이번 모터 변경 두 줄의 원문을 복원한다. 이동 재생에는 검증 대상 모터를 그대로 사용한다. [재생 도구](tools/replay_moving_attack.js) · [정적 검사](tools/validate_feel_pass.py) · [원문 대조 기록](tools/feel-fix2-extensions.json)

## 검증

**전체 정적 검사 11,182건 통과, 실패 0건.** 수정 전 기준선 11,168건을 다시 실행했고, 수정 후 검사명을 중복 횟수까지 대조했다. 기존 검사 누락은 0건이고 이동 공격 관련 검사는 14건 늘었다. 지상 정지·공중 정지·정지 공격·공격 입력 수용 검사가 모두 통과했다. 기존 경고 25건은 내용까지 같았다. [전체 검증 결과](STATIC-VALIDATION.json)

기본검 첫 타를 평지에서 충돌 상대와 이동 발판 없이 재생했다. 달리기 속도는 6.50유닛/초, 공격 구간은 0.40초, 물리 간격은 0.02초다. 아래 값은 공격 시작 시각부터 0.38초까지 20틱에서 계산한 수평 속도다.

| 입력과 검술 조건 | 수정 전 (유닛/초) | 수정 후 (유닛/초) |
|---|---|---|
| D 유지, 프로필 없음 또는 기본검 1단계 | 20틱 모두 0.00 | **20틱 모두 +6.50** |
| D 유지, 스텝이 있는 기본검 2단계 | 처음 5틱 +2.50, 이후 15틱 0.00 | 20틱 모두 +6.50 |
| 기본검 2단계, 0.20초에 D에서 A로 전환 | 처음 5틱 +2.50, 이후 15틱 0.00 | 처음 10틱 +6.50, 전환한 틱부터 10틱 −6.50 |
| 방향 입력 없이 기본검 2단계 공격 | 처음 5틱 +2.50, 이후 15틱 0.00 | 처음 5틱 +2.50, 이후 15틱 0.00 |

새 FeelProbe 판정은 수정 전 모터에서 첫 틱에 실패했다. 수정 후에는 20틱을 관측해 통과했다. 별도 경계 재생에서 +5.20은 통과했고, +6.50을 두 틱 유지한 뒤 세 번째 틱만 +5.19로 떨어지면 실패했다. 역방향 속도, 표본 0개, 바닥 접촉 상실도 각각 실패했다.

작업공간 루트에서 다음 명령을 실행했다. 첫 번째 전체 검사와 수정 전 모터 사본은 편집 전에 확보했다.

```sh
python3 -B tools/validate_gamedev.py --output /tmp/feel-fix2.vskjDS/baseline.json
node --check unity/RewindNotary/tools/replay_moving_attack.js
node unity/RewindNotary/tools/replay_moving_attack.js /tmp/feel-fix2.vskjDS/CombatMotor2D.cs
node unity/RewindNotary/tools/replay_moving_attack.js
python3 -B tools/validate_gamedev.py --output unity/RewindNotary/STATIC-VALIDATION.json
sha256sum -c /tmp/feel-fix2.vskjDS/story.sha256
python3 -B /tmp/feel-fix2.vskjDS/verify_delivery.py
```

스토리 JSON 6개의 해시가 모두 일치했다. 매니페스트의 9개 항목은 모두 Unity 레포 기준 상대 경로이며 각 파일의 존재, 마지막 NUL, 매니페스트 자체 제외를 확인했다.

## 판단 근거

속도 표는 실제 `Move` 분기를 JavaScript로 옮겨 실행한 정적 재생값이다. 입력 속도와 공격 길이, 스텝 수치, 물리 간격은 컨트롤러·콤보·검술 데이터·프로젝트 설정에서 읽었다. 기본검 1단계 프로필의 스텝 속도와 시간은 모두 0이므로, 프로필이 없는 경우와 구분해 두 조건을 각각 재생했다. [컨트롤러](Assets/Scripts/PlayerController2D.cs) · [검술 데이터](Assets/Resources/Skills/catalog.json) · [물리 설정](ProjectSettings/TimeManager.asset)

수정 전후 모터 파일을 대조하면 차이는 지상 수평 속도를 선택하는 부분에만 있다. 공격 중 방향을 바꿔도 타격 방향은 기존 `Facing`을 따른다. 공중 감속 6·28, 공격 지속 시간·타격 시점·콤보·넉백·유효 사용 카운팅과 적·보스의 이동 처리를 보존했다. 무진전 18초, 구역 정체 120초, 구역 제한 1,500초, 지형 허용 오차 0.005도 소스에서 확인했다. [이동·타격 처리](Assets/Scripts/CombatMotor2D.cs) · [시간 제한](Assets/Scripts/AutoTest/AutoTestDriver.cs) · [지형 판정](tools/validate_terrain_bounds.py)

## 남은 일

이 환경에는 Unity 에디터와 C# 컴파일러가 없어 Play Mode와 새 빌드의 실기 조작감은 확인하지 못했다. 스크린샷은 없다. 메인이 매니페스트로 푸시한 뒤 새 빌드에서 확인할 항목은 달리는 중 J를 눌렀을 때와 공격 도중 방향을 바꿨을 때의 체감이다.
