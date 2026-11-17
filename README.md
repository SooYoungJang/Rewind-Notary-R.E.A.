# Rewind Notary — Unity 횡스크롤 프로토타입

레아의 이동·점프·검 공격과 어제의 레아(잔향) 보스전을 시험하는 단일 테스트 스테이지다. `Assets/Scenes/Main.unity`에는 카메라와 `GameBootstrap`만 저장되어 있으며, Play 시 나머지를 코드로 생성한다.

## 열기와 실행

1. [Unity Hub](https://unity.com/download)를 설치하고 Unity **6.0 LTS (6000.0)** 에디터를 설치한다. 기록 버전은 **6000.0.65f1**이다. Hub에 더 최근의 6000.0 LTS 패치가 있으면 해당 버전으로 열 수 있지만 업그레이드 후 검증이 필요하다.
2. Hub → Projects → Add/Open project from disk에서 **이 README가 있는 `unity/RewindNotary/` 폴더**를 선택한다. 상위 `game-project/`나 `Assets/`를 선택하지 않는다.
3. 첫 실행은 인터넷에 연결된 상태에서 패키지 다운로드·임포트·스크립트 컴파일이 끝날 때까지 기다린다. 2D Sprite 및 Cinemachine 3.1.7은 `Packages/manifest.json`으로 요청한다.
4. **Play**를 누르고 Game 뷰를 클릭해 키보드 포커스를 준다. Editor 초기화 코드가 Main을 시작 씬으로 지정한다. 씬이 보이지 않으면 `Assets/Scenes/Main.unity`를 더블클릭하거나 `Rewind Notary → Open Main Scene`을 선택한다.
5. 오른쪽으로 이동하면서 발판·틈을 시험하고, 황금색 바닥 표시(x=35)를 지나 보스 아레나로 들어간다.

이 환경에는 Unity 에디터가 없어 실제 import/컴파일/Play/빌드는 실행하지 못했다. 파일은 바로 Play를 목표로 구성했으나 실행 성공을 검증했다고 주장하지 않는다. 검사 결과와 알려진 한계는 [BUILD-REPORT.md](BUILD-REPORT.md)를 참고한다.

## 조작

| 동작 | 키 |
|---|---|
| 좌우 이동 | A / D 또는 ← / → |
| 점프 | Space, W 또는 ↑ |
| 짧은 점프 | 상승 중 점프 키 놓기 |
| 검 공격 | J, Z 또는 마우스 왼쪽 버튼 |
| 전체 재시작 | R |

코요테 타임과 점프 버퍼는 각각 **0.1초**다. 플레이어 체력 6, 보스 체력 12이며 검 타격은 1회당 1이다. 보스의 황금색 점멸은 0.45초 공격 예고다. 피격에는 짧은 경직·넉백·무적 시간이 있다. 사망하면 1.1초 후 복귀하며, 낙하하면 즉시 복귀한다. 아레나 진입 후에는 아레나 시작점에서 플레이어와 보스 체력을 초기화한다. R은 시작 지점부터 초기화한다.

## 구현 범위

- 지면 2구간, 낙하 틈 1개, 발판 7개, 시작점과 보스 아레나.
- Rigidbody2D 기반 이동/점프, 검 공격 판정, 체력·피격·복귀.
- Texture2D `Resources.Load`와 `Sprite.Create`를 통한 8×4 시트 재생. idle/run/jump/fall/land/attack 상태, 왼쪽은 flipX. Animator와 `.meta` 스프라이트 슬라이싱 없음.
- 잔향의 추적·주기적 점프·예고 후 검 공격, HUD 체력바·클리어 표시.
- **CinemachineCamera + CinemachinePositionComposer + CinemachineBrain**으로 추적. 제한된 추적 타깃으로 스테이지 범위 유지.
- 원경/중경 패럴랙스와 반복 지면. 타일을 번갈아 좌우 반전하여 연결 경계를 맞춘다.
- 코드 합성 WAV 효과음 9종·루프 BGM 2종. SFX 풀·필드/보스 BGM 크로스페이드와 타격 히트스톱·화면 흔들림·흰색 플래시. 연결/파라미터/검증 한계는 [SOUND-REPORT.md](SOUND-REPORT.md) 참고.
- 스토리 진행·시간 회귀 시스템·새 인물·새 설정은 추가하지 않았다. HUD는 기본 폰트 호환을 위해 영어다.

두 시트는 2048×768이며 정확한 8×4 격자(셀 256×192)를 사용한다. 코드에서 정수 셀 Rect와 프레임별 발 피벗 JSON으로 읽는다. 원본 발 기준선에는 최대 12픽셀 편차가 있어 피벗으로 지면을 맞추며, 이미지 자체의 중심·프레임 연속성은 추가 보정 대상이다.

## PC에서 우선 확인할 항목

1. Console의 컴파일 오류와 `Missing GameArt`가 없는지 확인한다.
2. idle/run/점프/착지/검 프레임, 오른쪽·왼쪽 표시와 발 위치를 확인한다.
3. 발판 끝을 벗어난 직후 점프, 착지 직전 점프 입력, 짧은 점프를 각각 시험한다.
4. x=16~20의 틈에서 점프 통과와 낙하 복귀를 시험한다.
5. 아레나 진입, 보스 예고/공격/점프, 양쪽 체력 감소, 사망 복귀, 보스 처치 후 R을 시험한다.
6. 화면 비율을 바꾸며 카메라와 배경 반복 경계를 확인한다.
7. [사운드 보고서의 실행 검증](SOUND-REPORT.md#unity에서-남은-실행-검증)에 따라 루프·예고 취소·타격 플래시·사망/재시작 후 시간 배율을 확인한다.

이후에는 시트의 중심/발 위치와 잔상 정리, 발판 이동 감각·공격 판정·보스 템포를 Unity에서 조정하는 순서를 권장한다. 이 프로젝트는 액션의 기본 동작을 확인하는 프로토타입이며 출시용 콘텐츠나 빌드가 아니다.

## 파일 위치

- 실행 씬: `Assets/Scenes/Main.unity`
- 런타임 코드: `Assets/Scripts/`
- 에디터 초기화/임포트: `Assets/Editor/PrototypeProjectSetup.cs`
- 프로젝트에 포함된 PNG: `Assets/Resources/GameArt/`
- 원본 에셋/프롬프트/미달 항목: 상위 프로젝트의 `../../assets/game/GAME-ASSET-LIST.md`
- 작업 기록: 상위 프로젝트의 `../../gamedev-progress.md`
- 사운드 원본/목록: `../../assets/game/audio/AUDIO-LIST.md`, 재생성: `python3 tools/generate_audio.py` (NumPy 필요)
- 정적 검사: 상위 프로젝트 루트에서 `python3 tools/validate_gamedev.py` (Pillow, PyYAML, NumPy 필요; Unity 실행 검증 아님)
