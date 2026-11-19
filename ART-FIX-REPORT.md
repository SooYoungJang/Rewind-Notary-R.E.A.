# 아트 통합 후속 수정

## 목표

아트 통합 뒤 VM에서 보고한 정적 검증 실패 33건을 해소하고, 화랑 02의 청록색 막대와 착지 음원 GUID를 바로잡는 작업이다. 다섯 방의 화면 수정과 검증기 단언 두 건을 확인했다. **VM의 기존 승인 이력 병합과 전체 검증의 최종 판정은 미완료다.**

## 변경

- `RepresentativeRoomLayout.BuildRoom`이 일반 착지점에 그리던 청록색 `*_mark` 생성을 제거했다. 접촉 저장 지점에는 기존 금색 표식을 그린다. 착지 좌표와 체크포인트 복원은 계속 `Room.floors`를 사용한다. 이번 라운드에 편집한 코드 주석은 0개다. [방 생성 코드](Assets/Scripts/Story/RepresentativeRoomLayout.cs)
- 구역 연결 검사는 적을 초기화할 때 `RoomArt.Enemy(Room.stage, group.id)`를 실제 스프라이트 인자로 전달하는지 확인한다. 출정 흐름 검사는 웨이브 초기화와 HUD 안내를 계속 확인하며, 문의 통행 판정이 열린 그림과 잠긴 그림을 고르는지, 이전 노란 분기나 렌더러 색 대입이 들어갔는지도 확인한다. [구역 연결 검사](tools/validate_area_links.py) · [출정 흐름 검사](tools/validate_flow_assembly.py)
- 기준 커밋 `338024d451352648bf4ac5886707aefce0bafbe9`의 Git diff에서 보호 대상 C# 파일 11개의 편집을 추출했다. 아래 세 JSON에는 이번 아트 통합의 `before`와 `after` 쌍만 들어 있다. **VM의 기존 동명 JSON에 병합할 추가분**이다. 이전 등록분을 보존하도록 VM 회수본은 `vm-tools/additions/`에 두었다.
- 방 생성 코드에는 별도의 역방향 편집을 등록했다. 기존 아트 통합 기록의 해시를 보존하고, 수정 후 코드에서 먼저 이번 청록 표식 변경을 되돌린다. [후속 편집](tools/art-fix-extensions.json) · [적용 순서](tools/story_link_baseline.py)

| 용도 | 이 PC의 `tools/` 파일 | VM 루트 `tools/`용 회수본 |
| --- | --- | --- |
| 저장 무결성 승인 추가분 | [save-integrity-extensions.json](tools/save-integrity-extensions.json) | [병합용 사본](vm-tools/additions/save-integrity-extensions.json) |
| 현지화 승인 추가분 | [localization-extensions.json](tools/localization-extensions.json) | [병합용 사본](vm-tools/additions/localization-extensions.json) |
| 조사 승인 추가분 | [investigation-extensions.json](tools/investigation-extensions.json) | [병합용 사본](vm-tools/additions/investigation-extensions.json) |
| 방 표식 역방향 편집 | [art-fix-extensions.json](tools/art-fix-extensions.json) | [사본](vm-tools/art-fix-extensions.json) |
| 이전 아트 기록과 연결 | [story_link_baseline.py](tools/story_link_baseline.py) | [사본](vm-tools/story_link_baseline.py) |
| 적 스프라이트 단언 | [validate_area_links.py](tools/validate_area_links.py) | [사본](vm-tools/validate_area_links.py) |
| 문 상태 단언 | [validate_flow_assembly.py](tools/validate_flow_assembly.py) | [사본](vm-tools/validate_flow_assembly.py) |

착지 음원은 요청한 PC 파일 세 개를 매니페스트에 넣었다. `land.wav`는 14,156바이트이며 기준 커밋의 본체와 바이트가 같다. 두 메타에는 에디터가 분리한 GUID가 들어 있다. [land.wav](Assets/Resources/Audio/Sfx/land.wav) · [land.wav.meta](Assets/Resources/Audio/Sfx/land.wav.meta) · [land-thud.wav.meta](Assets/Resources/Audio/Sfx/land-thud.wav.meta)

## 검증

rea_dev 루트에서 Python 3.12.14로 실행했다. 종합 검사에 필요한 PyYAML은 이 PC의 기존 의존성 디렉터리를 사용했다.

```powershell
$py = 'C:/Users/장수영/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
$env:PYTHONPATH = 'C:/Users/장수영/Documents/rea_dev/Temp/art-integration-deps'
rtk proxy $py -X utf8 -B tools/validate_gamedev.py --output Temp/art-fix-static.json
rtk proxy $py -X utf8 -B tools/validate_art_integration.py
rtk proxy $py -X utf8 -B Temp/art-fix-levels.py
rtk proxy $py -X utf8 -B Temp/art-fix-subset.py
```

| 실행 범위 | 이번 실행 결과 |
| --- | --- |
| 종합 진입점 | 종료 코드 1. `story_link_baseline.py`가 `localization_followup`을 가져오는 중 `ModuleNotFoundError`로 종료했다. 종합 결과 JSON은 생성되지 않았다. |
| 아트 통합 | 951건 통과, 실패 0건. 방 56개, 출구 169개, 적 165개, 기믹 정거장 346개, 은실 앵커 83개를 검사했다. |
| 레벨 설계 및 1~6단계 | 3,771건 통과, 실패 0건. 설계 84건, 단계별 264·128·308·612·766·1,609건이다. 기존 PC용 부분 실행기에서 후속 편집 한 층과 이번 출력 경로를 반영했다. |
| 종합 검사 중 로컬 입력이 있는 핵심 구간 | 836건 통과, 실패 0건. 실제 메타와 GUID, 씬, 설정, 전투, 소스 구문 검사를 실행했다. |
| 수정한 단언 두 건 | 수정 전 두 단언의 값은 모두 거짓, 수정 후에는 모두 참이었다. 사각형 스프라이트, 잘못된 구역 스프라이트, 노란 문 분기 복원, 문 상시 열림, 렌더러 내부 틴트 대입을 각각 메모리에 주입하자 다섯 경우 모두 해당 단언이 거짓이 됐다. |

`rtk proxy git -c safe.directory=C:/Users/장수영/Documents/rea_dev diff --no-ext-diff --no-color --unified=3 338024d4 --`에 보호 파일 11개를 전달해 편집을 추출했다. 부분 실행기는 JSON을 다시 읽고, `git cat-file --batch`에서 가져온 같은 커밋의 원본과 비교했다. C# 작업 사본의 개행은 저장소의 LF로 읽었다.

| 승인 추가분 | 파일 | 편집 쌍 | 역적용한 바이트가 Git 원본과 일치 |
| --- | ---: | ---: | ---: |
| 저장 무결성 | 11 | 30 | 11/11 |
| 현지화 | 11 | 30 | 11/11 |
| 조사 | 9 | 24 | 9/9 |

세 체인의 공통 대상은 `LevelBuilder`, `DestructibleTarget2D`, `PaintSurface2D`, `PracticeLamp2D`, `PracticeTemporalHazard2D`, `ResonanceTarget2D`, `StitchSurface2D`, `WaveStageDirector`, `WaveTarget2D`다. 저장 무결성과 현지화에는 `CasselArena`와 `InvestigationWorld`도 들어간다. 31개 비교에서 정방향 재적용도 현재 소스를 복원했다. 승인 밖의 개행을 추가하면 31개 모두 원본 해시와 달랐다. 기존 아트 이력에 등록된 C# 파일 21개도 Git 원본과 일치했다. 방 생성 파일은 청록 표식 수정 전후 두 버전으로 확인했다.

Unity 6000.0.65f1의 Play Mode에서 격리 저장 슬롯을 사용해 각 방을 `RepresentativeArea.Restore()`로 열었다. 활성 방의 렌더러를 센 뒤 같은 카메라 좌표에서 수정 전후를 1280×720으로 촬영했다.

| 확인한 방 | 청록 표식 수정 전 → 후 | 수정 후 금색 저장 표식 | 수정 후 화면 |
| --- | ---: | ---: | --- |
| 화랑 `gallery_02_white_hall` | 4 → 0 | 1 | [화랑 02](ART-FIX-GALLERY-02.png) |
| 시계탑 `clock_02_pendulum` | 3 → 0 | 1 | [시계탑 02](ART-FIX-CLOCK-02.png) |
| 오페라 `opera_02_auditorium` | 3 → 0 | 1 | [오페라 02](ART-FIX-OPERA-02.png) |
| 대종루 `bell_02_tunnels` | 3 → 0 | 1 | [대종루 02](ART-FIX-BELL-02.png) |
| 거점 `hub_workroom` | 5 → 0 | 0 | [거점 작업실](ART-FIX-HUB-WORKROOM.png) |

Unity 재컴파일 상태 조회는 오류 0개였다. 에디터는 두 음원을 44,100Hz 모노 클립으로 각각 불러왔다. 기준 커밋에서 동일했던 GUID `8a24c7a95ac85f241a8dcb4e07256728`은 PC에서 `land.wav`의 `ad56ff9578f0b5f48ab432f5e6b32cd4`와 `land-thud.wav`의 `a4842bf84c2abe846ac167201f424c09`로 분리돼 있었다.

종료 직전 열린 씬 1개, 저장 대기 씬 0개를 확인했다. `rtk proxy unity close C:/Users/장수영/Documents/rea_dev --if-saved --timeout 45 --non-interactive --json`은 `not_open`을 반환했다. CLI의 상태 조회에는 에디터 검색 파일 접근 거부가 기록됐다. 연결된 Unity MCP의 `editor.quit`가 종료 코드 0을 반환했고, `Get-Process`로 해당 에디터 PID 37004가 종료된 것을 확인했다.

매니페스트의 저장소 상대 경로는 24개이며 UTF-8 크기는 807바이트다. 모든 경로의 실재, 중복 0개, 마지막 NUL, 자기 참조 0개, `Packages/`·`ProjectSettings/` 항목 0개를 검사했다. 도구 사본 7쌍도 바이트가 일치했다. 매니페스트 SHA-256은 `76f9d49def03a22ec16a396f313a020a18e5525c1307dfdf7c0c7737ecc963f7`이다. [매니페스트](push-paths-art-fix.bin)

## 판단 근거

화랑 동쪽 화면의 세 막대는 월드 X 좌표 39·41·50에 있는 `gallery_frame_landing_mark`, `gallery_hall_east_mark`, `gallery_exit_02_mark`와 일치했다. 서쪽 X=4에도 같은 표식이 있었다. 각 오브젝트는 `tile-gallery`를 청록으로 착색해 1.6×0.05유닛으로 그렸고, 콜라이더 수는 모두 0개였다. 같은 프레임에서 이 렌더러들만 꺼서 촬영한 화면과 최종 화면은 픽셀 단위로 같았다. 기존 통합 스크린샷과 최종 스크린샷의 차이는 세 막대가 차지한 1,256픽셀에 한정됐다. [기존 화면](ART-INTEGRATION-GALLERY-02.png) · [수정 화면](ART-FIX-GALLERY-02.png)

## 남은 일

VM의 `save_integrity_baseline.py`, `localization_baseline.py`, `investigation_baseline.py`와 기존 승인 JSON 세 개가 PC 기준 커밋과 인접 사본에 없다. 상위 작업 트리의 원자료도 필요하다. VM의 기존 이력에 추가분을 병합한 뒤 실제 세 모듈이 각각의 역사적 해시까지 복원하는지와 **전체 검증에서 33건이 해소됐는지** 확인해야 한다. 기존 `STATIC-VALIDATION.json`의 통과 수는 이번 실행 결과에 포함하지 않았다.

이번에 편집한 추적 코드와 지정 음원에 대한 `git diff --check`는 통과했다. 전체 작업 트리 검사에는 에디터가 이미 직렬화한 GameArt 메타 67개와 `ProjectSettings/ProjectSettings.asset`의 행 끝 공백이 남아 있다.
