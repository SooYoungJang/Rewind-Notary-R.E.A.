# 빌드 장부

Unity 프로젝트 루트의 `VERSION`을 사용한다. 아래 수동 템플릿은 실제 실행 후 값으로 채워 누적 기록 표 끝에 붙인다. 실행하지 않은 빌드를 성공으로 기록하지 않는다.

## 수동 기록 템플릿

```text
| <완료 UTC: yyyy-MM-ddTHH:mm:ssZ> | <빌드 ID> | <VERSION> | <Unity 버전> | <Windows64 또는 macOS> | <Succeeded/Failed/Cancelled/Unknown/NotStarted> | <종료 코드> | <STATIC-VALIDATION passed 또는 unknown> | <STATIC-VALIDATION SHA-256 또는 unknown> | <Player 경로 또는 unknown> | <결과물 bytes 또는 unknown> | <비고> |
```

`StandaloneBuild`가 각 호출의 결과를 표 끝에 자동 append한다. 성공은 BuildPipeline 성공, 설정 복원, 결과 기록까지 완료한 경우에만 종료 코드 0이다. BuildPipeline 결과와 종료 코드는 별개 열이다. `NotStarted`는 진입점 안의 사전 검사에서 중단된 경우다.

용량은 `Player/` 아래 파일 길이의 합(bytes)으로 기록하는 정보다. 실행 파일과 동반 데이터, macOS 앱 번들 내부를 포함하며 Unity 로그·검증 스냅샷·결과 JSON은 제외한다. 실패 시 남은 부분 결과물도 측정하고, 측정할 수 없으면 `unknown`을 기록한다. `0`은 측정된 빈 폴더를 뜻한다.

정적 검증 수치는 호출 시점의 `STATIC-VALIDATION.json`에서 읽는다. 해당 파일 복사본과 SHA-256을 빌드 폴더에 보관한다. 정적 검증은 Unity 컴파일·실행을 증명하지 않는다.

라이선스/패키지/컴파일 문제, 강제 종료 등으로 진입점에 도달하지 못하면 자동 행이 없을 수 있다. Unity 로그를 확인하고 필요하면 수동 템플릿으로 기록한다. 기록 파일 쓰기에 실패하면 콘솔에 오류와 경로를 남기고 비정상 종료한다. 시작 상태에는 실제 빌드 행이 없다.

## 누적 기록

| 완료 UTC | 빌드 ID | 버전 | Unity | 대상 | BuildPipeline 결과 | 종료 코드 | STATIC-VALIDATION passed | STATIC SHA-256 | 결과물 경로 | 결과물 bytes | 비고 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

| 2026-11-20T02:56:33Z | stage1-20261120b | 0.1.0 | 6000.0.65f1 | Windows64 | Succeeded | 0 | 12302 | 0180a1500289fce4b0af84386fd7962c6858f8b13c88a07984af9d73faa1e123 | C:\Users\장수영\Documents\rea_gemini\Builds\Windows64\stage1-20261120b\Player\RewindNotary.exe | 890510130 | automatic |

| 2026-11-20T03:58:54Z | stage1-20261120c | 0.1.0 | 6000.0.65f1 | Windows64 | Succeeded | 0 | 12302 | 0180a1500289fce4b0af84386fd7962c6858f8b13c88a07984af9d73faa1e123 | C:\Users\장수영\Documents\rea_gemini\Builds\Windows64\stage1-20261120c\Player\RewindNotary.exe | 890510130 | automatic |

| 2026-11-20T04:20:59Z | stage2-20261120a | 0.1.0 | 6000.0.65f1 | Windows64 | Succeeded | 0 | 12302 | 0180a1500289fce4b0af84386fd7962c6858f8b13c88a07984af9d73faa1e123 | C:\Users\장수영\Documents\rea_gemini\Builds\Windows64\stage2-20261120a\Player\RewindNotary.exe | 890528050 | automatic |

| 2026-11-20T04:36:45Z | stage3-20261120a | 0.1.0 | 6000.0.65f1 | Windows64 | Succeeded | 0 | 12302 | 0180a1500289fce4b0af84386fd7962c6858f8b13c88a07984af9d73faa1e123 | C:\Users\장수영\Documents\rea_gemini\Builds\Windows64\stage3-20261120a\Player\RewindNotary.exe | 890537266 | automatic |

| 2026-11-20T04:39:55Z | stage3-20261120b | 0.1.0 | 6000.0.65f1 | Windows64 | Succeeded | 0 | 12302 | 0180a1500289fce4b0af84386fd7962c6858f8b13c88a07984af9d73faa1e123 | C:\Users\장수영\Documents\rea_gemini\Builds\Windows64\stage3-20261120b\Player\RewindNotary.exe | 890536754 | automatic |

| 2026-11-20T04:47:12Z | stage4-20261120a | 0.1.0 | 6000.0.65f1 | Windows64 | Succeeded | 0 | 12302 | 0180a1500289fce4b0af84386fd7962c6858f8b13c88a07984af9d73faa1e123 | C:\Users\장수영\Documents\rea_gemini\Builds\Windows64\stage4-20261120a\Player\RewindNotary.exe | 890554162 | automatic |
