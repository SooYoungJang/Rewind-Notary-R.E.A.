# Standalone 빌드 체계 구현 보고

작성일: 2026-11-18 UTC. 대상: 《리와인드 공증록: 레아》, Unity **6000.0.65f1**.

자동화와 기록 체계를 구현하고 정적 검증을 실행했다. **이 VM에는 Unity 에디터·C# 컴파일러·PowerShell이 없어 Unity 컴파일, PowerShell 실행, Standalone 빌드와 실기 실행은 수행하지 않았다.** 실제 결과물은 없으며 `BUILD-LOG.md`의 누적 기록은 0행이다.

## 구성 요소

아래 경로의 기준은 `game-project/`다. 버전과 장부의 프로젝트 루트는 `unity/RewindNotary/`다.

| 파일 | 역할 |
| --- | --- |
| `unity/RewindNotary/Assets/Editor/StandaloneBuild.cs` 및 `.meta` | Windows/macOS batchmode 진입점, BuildPipeline 호출, 결과 측정·기록·종료 코드 |
| `unity/RewindNotary/VERSION` | 현재 `0.1.0`, `MAJOR.MINOR.PATCH` 형식의 버전 원본 |
| `unity/RewindNotary/BUILD-LOG.md` | 머리의 수동 기록 템플릿과 자동 append되는 누적 표 |
| **`tools/build-standalone.ps1`** | **Windows 사용자 PC용 원커맨드 러너**. 프로젝트 경로를 스크립트 위치에서 계산 |
| `tools/requirements-validation.txt` | 검증 의존성 버전: NumPy 1.26.4, Pillow 10.2.0, PyYAML 6.0.1 |
| `tools/validate_build_pipeline.py` | 빌드 계약·씬·버전·장부·러너·보호 해시 변경 기록 검사 |
| `tools/validate_gamedev.py` | 기존 통합 검증에 위 모듈 연결, UTF-8 JSON 원자적 저장용 `--output` 추가 |
| `tools/build-pipeline-baseline-update.json` | 작업 전에 변경된 행정 문서에 대한 보호 해시 갱신 내역 |
| `unity/RewindNotary/STATIC-VALIDATION.json` | 이번 전체 정적 검증 결과 |

## 사용자 PC에서 실행

### 최초 준비

1. 전체 `game-project`를 PC에 준비한다. `unity/RewindNotary`만 복사하면 검증용 원본 에셋·도구·보호 문서가 빠지므로 러너를 사용할 수 없다. 해시 검사는 원본 바이트를 비교하므로 체크아웃/복사 시 줄바꿈을 변환하지 않는다.
2. Unity Hub에 **6000.0.65f1**과 필요한 대상의 Build Support를 설치하고 해당 PC의 Unity 라이선스를 활성화한다. Windows64는 Windows Standalone, macOS는 **Mac Build Support (Mono)**가 필요하다. 이 스크립트는 두 대상 모두 Mono로 빌드한다. 최초 패키지 복원에는 네트워크가 필요할 수 있다.
3. 동일 프로젝트를 연 Unity 에디터를 닫는다. Windows PowerShell 5.1 이상과 Python **3.12 권장(3.10~3.12)**을 준비한다.
4. `game-project`에서 검증 환경을 한 번 설치한다.

```powershell
py -3.12 -m venv .venv-build
.\.venv-build\Scripts\python.exe -m pip install -r .\tools\requirements-validation.txt
```

### Windows64 주력 빌드: 한 명령

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build-standalone.ps1
```

러너는 명시한 `-PythonPath`, 저장소의 `.venv-build\Scripts\python.exe`, `py.exe -3`, `python.exe` 순서로 Python을 선택한다. 정적 검증을 새로 실행해 JSON을 갱신하고, 오류가 없을 때만 Unity를 시작한다. 검증 의존성 설치는 최초 준비 단계이며 러너가 자동 설치하지 않는다.

Unity 탐색 순서는 `-UnityPath` → `UNITY_EDITOR_PATH` → `-UnityHubRoot` → `UNITY_HUB_EDITOR_ROOT` → Program Files의 `Unity\Hub\Editor\6000.0.65f1\Editor\Unity.exe`다. 사용자 지정 경로에서도 실행 중인 에디터의 실제 버전을 C# 진입점에서 확인한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build-standalone.ps1 -UnityPath "D:\Unity\6000.0.65f1\Editor\Unity.exe"
```

Unity Hub의 에디터 설치 루트를 지정하려면 `-UnityHubRoot "D:\Unity\Hub\Editor"`를 사용한다. 다른 Python 환경은 `-PythonPath "D:\Python312\python.exe"`로 지정한다. 스크립트 자체의 절대 경로를 쓰면 다른 작업 디렉터리에서도 실행할 수 있다.

### Windows PC에서 macOS 대상 빌드

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build-standalone.ps1 -Target macOS
```

대상은 `StandaloneOSX`이며 `.app`을 생성하도록 구성했다. macOS CPU 아키텍처는 기존 Unity 설정을 따르고 `build-result.json`의 `macArchitecture`에 플랫폼 설정 원문을 기록한다. 빈 문자열은 명시적 플랫폼 값이 없어 Unity 기본 설정을 사용하는 경우다. 실제 아키텍처와 앱 실행은 Mac에서 확인해야 한다. Windows에서 만든 앱은 Mac으로 옮긴 후 내부 실행 파일의 실행 권한을 확인한다. 이는 [Unity 6.0 macOS 빌드 안내](https://docs.unity3d.com/6000.0/Documentation/Manual/macos-building.html)에도 명시돼 있다.

### 직접 batchmode 호출

진입점은 `RewindNotary.Editor.StandaloneBuild.BuildWindows64`와 `RewindNotary.Editor.StandaloneBuild.BuildMacOS`다. 직접 호출할 때도 먼저 정적 검증을 갱신한다. 아래는 Mac에서 저장소 루트를 기준으로 실행하는 예다. Python 의존성은 해당 환경에 별도로 설치한다.

```bash
python3 -X utf8 tools/validate_gamedev.py --output unity/RewindNotary/STATIC-VALIDATION.json
# 위 명령이 종료 코드 0일 때 다음 명령 실행
"/Applications/Unity/Hub/Editor/6000.0.65f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics -quit \
  -projectPath "$PWD/unity/RewindNotary" \
  -buildTarget osxuniversal -standaloneBuildSubtarget Player \
  -executeMethod RewindNotary.Editor.StandaloneBuild.BuildMacOS \
  -logFile "$PWD/rewind-macos-editor.log"
```

Windows 직접 호출은 `-buildTarget win64`와 `BuildWindows64`를 사용한다. 대상은 에디터 시작 시 선택해야 한다. 실행 중 대상 변경에 따른 컴파일 문제를 피하도록 진입점은 현재 대상이 맞는지도 검사한다. 관련 근거: [BuildPipeline.BuildPlayer](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/BuildPipeline.BuildPlayer.html), [Unity CLI](https://docs.unity3d.com/6000.0/Documentation/Manual/EditorCommandLineArguments.html).

직접 호출은 러너의 동시 실행 잠금을 사용하지 않으므로 같은 프로젝트에 대해 직렬로 실행한다. `-rewindBuildId`는 선택 사항이다. 생략하면 UTC 시각과 GUID를 조합하며, 지정하면 영문·숫자·`_`·`-`로 된 1~80자만 허용한다. 이미 결과 JSON이나 Player 폴더가 있는 ID는 재사용하지 않는다.

## 빌드 동작과 기록

- 씬은 `EditorBuildSettings.scenes`에서 **enabled 씬만 기존 순서대로** 사용한다. 현재 목록은 `Assets/Scenes/Main.unity` 1개이며 GUID도 확인했다. 스크립트에 씬 목록을 별도로 고정하거나 설정을 덮어쓰지 않는다.
- 버전 형식·에디터 버전·장부 헤더·정적 검증 JSON·씬 존재·현재 대상·대상 모듈을 검사한다. 정적 JSON은 양수 `passed`, 빈 `errors`, `checks` 길이와 `passed` 일치를 요구한다.
- `VERSION` 값을 `PlayerSettings.bundleVersion`에 적용하고 Standalone Mono, Player 서브타깃, `BuildOptions.None`으로 `BuildPipeline.BuildPlayer`를 호출한다. 원래 버전과 스크립팅 백엔드는 정상/예외 경로의 `finally`에서 복원한다.
- BuildReport의 `Succeeded`와 실행 결과물 경로 존재를 확인한다. 빌드 호출 자체의 결과와 설정 복원·측정·장부 기록의 완료 여부를 구분한다.
- 러너가 만든 폴더는 `unity/RewindNotary/Builds/<Windows64 또는 macOS>/<UTC-GUID>/`다. 이전 산출물을 삭제하거나 덮어쓰지 않는다.

```text
Builds/<대상>/<빌드 ID>/
  Unity.log                  # 러너가 지정하는 Unity 에디터 로그
  STATIC-VALIDATION.json      # 진입점이 읽은 검증 JSON의 정확한 복사본
  build-result.json          # 개별 호출의 결과, 종료 코드, 씬, 경로, 측정값
  Player/
    RewindNotary.exe          # Windows64: 동반 데이터/라이브러리도 같은 폴더
    또는 RewindNotary.app/    # macOS
```

**용량은 정보로만 기록한다.** `Player/` 내부 파일 길이를 재귀적으로 합한 bytes이며 로그·결과 JSON·검증 스냅샷은 제외한다. 실패 후 부분 결과물도 같은 방식으로 측정한다. 측정 불가능은 JSON `-1`/장부 `unknown`, 실제로 측정된 빈 디렉터리는 `0`이다.

장부에는 완료 UTC, 빌드 ID, VERSION, Unity 버전, 대상, BuildPipeline 결과, 종료 코드, STATIC-VALIDATION passed 수, 검증 SHA-256, 결과물 경로, bytes, 비고를 append한다. 파일 경로나 오류 메시지에 들어간 `|`·줄바꿈 등은 표를 깨뜨리지 않도록 처리한다. 결과 JSON을 먼저 기록한 뒤 장부에 append하며, 기록 실패도 비정상 종료로 처리한다. 두 파일은 하나의 원자적 트랜잭션이 아니므로 강제 종료 후에는 개별 JSON·장부·Unity 로그를 대조한다.

| 종료 상태 | 의미 |
| --- | --- |
| C# 0 | BuildPipeline 성공과 결과물 확인, 설정 복원·측정·JSON·장부 기록 완료 |
| C# 1 | 사전 검사 또는 BuildPipeline 실패/예외 |
| C# 2 | 설정 복원, 결과 측정 또는 기록 처리 실패 |
| 러너 비정상 종료 | 위 코드 전달 또는 Python/경로/프로세스/결과 식별 검사 실패 |

라이선스·패키지 복원·C# 컴파일 문제로 진입점에 도달하지 못하거나 프로세스가 강제 종료되면 자동 장부 행이 없을 수 있다. 러너는 결과 JSON이 없으면 성공으로 취급하지 않고 Unity 로그 경로를 출력한다. 정적 검증 단계에서 실패하면 Unity는 호출하지 않는다. 필요할 때 장부 머리의 수동 템플릿을 사용하며, 확인하지 못한 값은 `unknown`으로 남긴다.

버전 갱신은 `VERSION` 한 줄을 변경한 뒤 같은 명령을 다시 실행한다. 버전 번호는 자동 증가하지 않는다. 동일 소스·Unity 버전·패키지/Player 설정에서 반복 실행하는 절차를 제공한다. 기계 간 바이너리의 바이트 단위 동일성은 검증하지 않았다. 최초 실제 빌드에서 Unity가 생성하는 `Packages/packages-lock.json`도 보관해 이후 패키지 해석을 추적해야 한다.

## 정적 검증 결과

실행 명령:

```bash
python3 -X utf8 tools/validate_gamedev.py --output unity/RewindNotary/STATIC-VALIDATION.json
```

- **passed 4863 / errors 0 / 프로세스 종료 코드 0**.
- 기존 **4811개 체크가 전부 유지**됐다. 추가 52개는 빌드 전용 검사 49개와 새 C# 파일의 메타데이터·GUID·구분자 검사 3개다.
- 빌드 검사에는 두 진입점·BuildPipeline 옵션·종료 코드·장부 append·JSON 스냅샷·러너 인수와 실패 경로·씬 경로/GUID·버전·장부 형식이 포함된다. 잘못된 장부 형식 7종을 메모리 내 fixture로 거부했다. 이 fixture는 실제 빌드 기록에 쓰지 않았다.
- 기존 audio/combat/skill/skeleton/dash/remaining/evolution/wave/janhyang/trial/investigation/cassel/save 지표와 sound_character를 이전 JSON과 비교해 모두 동일함을 확인했다. 스킬 집계 계약과 단계 경계의 기존 검사도 그대로 통과했다.
- 작업 시작 시 SHA-256과 비교한 기존 게임 Assets·ProjectSettings·Packages·Markdown 문서 **298개 모두 변경 없음**. 기존 리포트·스토리 문서는 수정하지 않았다.
- 새 Python 검증 모듈과 통합 검증기에 `py_compile`을 실행했다. C# 구분자·소스 계약과 PowerShell 소스 계약 검사는 컴파일/실행 검사가 아니다.
- JSON의 `unity_executed`, `csharp_compiled`, 빌드 지표의 `powershell_executed`, `standalone_build_executed`는 모두 `false`다.

처음 전체 검사를 실행했을 때, 작업 전에 수정된 `22-release-admin-pack.md`의 보호 해시 7건이 불일치했다. 문서에는 사용자가 확정한 스토브 자체등급분류 무료 경로가 이미 반영돼 있었다. 본문을 수정하지 않고 해당 문서 해시 7개와 이를 참조하는 보호 목록 파일 해시 4개를 갱신했다. 다음 SHA-256과 각 치환 내역은 `tools/build-pipeline-baseline-update.json`에 기록했다.

- 이전 문서 해시: `64b1439f3f0447212be9f15498a277f0fa44d30ff374609f9ba0c84aa4f62e22`
- 현재/작업 시작 시 문서 해시: `66252b67ead5362a976a9540d4743c708e9d4621a0a9babff6cb0e6c1e9228dc`

검증기는 기록된 치환을 되돌린 보호 목록 파일의 SHA-256이 원래 값과 일치하는지도 검사한다. 보호 검사를 제거하거나 실패를 무시하는 예외를 추가하지 않았다. 기존 게임 회귀 수치는 바꾸지 않았다.

이번 작업에서 기존 파일 변경은 통합 검증기·정적 JSON·위 보호 목록 7개뿐이다. GitHub 푸시는 수행하지 않았다.

## 사용자 PC 실기 확인 항목

- [ ] Windows PowerShell 5.1/7에서 공백·한글 경로, 자동 Unity 탐색, 사용자 지정 경로, 가상환경 Python 선택을 확인한다.
- [ ] 6000.0.65f1에서 최초 패키지 복원과 C# 컴파일을 완료하고 Windows64 빌드 명령의 종료 코드 0을 확인한다.
- [ ] exe와 동반 데이터가 생성됐는지, 새 빌드 ID·UTC·VERSION·passed·검증 SHA-256·경로가 JSON과 장부에서 일치하는지 확인한다. Player 파일 길이 합이 기록된 bytes와 같은지 확인한다.
- [ ] 연속 두 번 실행해 기존 출력과 장부 행이 보존되고 새 행이 누적되는지 확인한다. `VERSION` 변경 후 Player 버전과 새 행에 해당 값이 반영되는지 확인한다.
- [ ] 별도 작업 복사본에서 모듈 누락·잘못된 VERSION·활성 씬 없음·쓰기 불가능한 장부·컴파일 오류를 유발해 비정상 종료와 로그/기록 경계를 확인한다. 강제 종료 후 복원 상태도 점검한다.
- [ ] Windows Player에서 메인 메뉴, 이동·전투, 키보드/패드, 한글 표시, 음향, 저장/로드를 확인한다. 스킬 유효 사용 집계·성장 경계·웨이브/조사/시련/카셀 진행은 기존 회귀 항목대로 점검한다.
- [ ] macOS 빌드 모듈로 `.app` 생성 후 대상 Intel/Apple silicon Mac에서 실제 아키텍처·실행 권한·시작·입력·표시·저장을 확인한다. 배포용 서명·공증은 별도 실기 절차로 확인한다.
- [ ] 빌드 후 원래 PlayerSettings 버전/백엔드가 복원됐는지 확인하고, 실제 Unity가 만든 패키지 잠금 파일·Unity 로그·개별 결과 JSON을 보관한다.
