# 본편 연결 접점 구현 보고서

2026-11-18 · 아스트라 · Unity 6000.0.65f1

과제 12·4·6의 저장 및 지급 접점을 구현했다. **전체 정적 검증은 6,080건 통과, 오류 0건**이다. 전용 검사 143건은 소스·데이터 계약 42건과 Python 참조 검사 101건으로 구성된다. Unity Editor에서 실행할 임시 슬롯 검사도 추가했다. 이 환경에서 C# 컴파일, Play Mode, Windows 빌드는 실행하지 않았다. [정적 검증 결과](STATIC-VALIDATION.json)

## 기본 엔딩과 직전 기록

일반 슬롯에서 카셀 시도를 시작할 때 진입 직전 진행을 복사한다. 기본 엔딩이 성립하면 주 저장에 결착과 `ending_basic`, 대법정 승강기 앞 복귀점 `court_lift`를 함께 기록한다. 승강기 화면은 저장 완료를 기다린 뒤 결착을 표시한다. 여기에서 **최종전 직전 기록 재개**, 카셀 재도전, 스테이지 선택을 이용할 수 있다. 다른 스테이지에서 저장한 뒤에도 같은 슬롯의 스테이지 선택 화면에서 직전 기록을 열 수 있다.

- `slot-N.json.bak`은 기존 슬롯 자동 복구용이다. 직전 기록에는 `slot-N.json.previous-a`와 `.previous-b`를 번갈아 쓴다. 사용하지 않는 쪽에 임시 파일을 쓰고 디스크 반영·재판독을 마친 뒤 주 저장이 해당 파일과 결착 시도 ID를 가리키게 한다. 두 쓰기 사이에 중단돼도 기존 주 저장이 가리키는 기록을 읽을 수 있다.
- 재개 시 슬롯 ID, 결착 시도, 잔향 격파·명령 철회·최종 열람·승강기 해금을 검사한다. 복귀 위치와 시도 상태를 복원해 `R=0`, 다섯 기술 사용 가능 상태로 돌아간다. 숙련, 종언 이전 원장, 본편 장면, 단서, 대련 증거, 지원 잔량과 엔딩 달성 표시는 현재 슬롯 값을 유지한다. 엔딩 이후 훈련한 횟수나 이미 소비한 지원도 여기에 포함된다.
- 패배·중단·진 엔딩·F8 검증 시도는 기본 엔딩의 직전 기록을 교체하지 않는다. 슬롯 삭제는 두 기록과 임시 파일까지 처리한다. 손상된 직전 기록을 선택하면 오류를 표시하고 현재 진행을 유지한다.
- v1~v3의 기존 v4 이행과 v4 필드 형식을 유지했다. 업데이트 전 **현재 시도가 기본 엔딩으로 결착된 카셀 슬롯**에는 당시 진입 사본이 없으므로, 첫 선택 시 보유한 결착·진행으로 복귀점을 구성한다. 구 슬롯의 현재·직전 시도에서 확인할 수 있는 기본·진 엔딩 달성도 계승한다.
- 승강기는 이번 선행 작업에서 논리 복귀점과 선택 화면으로 구현했다. 고정 바닥의 월드 좌표와 에단 연성로까지의 물리 동선은 방 제작 때 연결한다.

## 본편 단서 지급

| 플레이어가 마친 행동 | 호출 접점 | 지급 결과 |
|---|---|---|
| 다락방에서 마르타에게 사본 수령 | `session.Story.CompleteMartaAtticHandoff()` | `hub`에서 `contract_copy` |
| 구역 초입에서 루카의 유품 안내 완료 | `session.Story.CompleteLukaKeepsakeGuide(benefactor)` | 해당 은인의 `imprint_*` 한 개 |
| 귀환 후 코어 단말에서 2단계 선택 | `GameShell.OpenStoryCore()` → 조사 화면의 `PrepareStoryComparison` | `hub`, `C=5`, `I=1`에서 `contract_original` 한 번 |

루카 안내는 벨벳–미하일, 시계–율리우스, 오페라–발레리아, 종루–가브리엘, 화랑–이리나를 대응시킨다. 단서와 장면 완료를 같은 저장에 넣는다. 이전 슬롯에 단서 ID가 있으면 장면 영수증만 남기므로 획득 이벤트와 본문 요청이 중복되지 않는다.

원본은 단말에서 2단계를 선택해 입장 조건을 평가하기 직전에 지급한다. 2단계 열람에는 별도로 1단계 색인 완료와 계약 문서 2/2가 필요하다. 일시정지 로그, 다른 구역, 다른 단계 선택은 원본을 지급하지 않는다. 구 슬롯이 테스트 배치에서 원본을 얻었어도 본편의 공개 시점까지 이름·본문·관련 단서명·수집률에 노출되지 않는다. 이미 대조 열람을 진행한 구 슬롯의 열람 이력도 인식한다.

기존 `Investigation/catalog.json`의 `prototype` 조사 지점 10개(단서 9개와 단말), 좌표·반경·지정 범위 검사를 보존했다. 본편 장면 행에서는 실제 수령·안내 행동이 성공한 시점에 위 단서 함수를 호출하고 반환값이 참일 때 다음 행으로 진행한다.

## 본편 첫 지원과 대련 증거

`session.Story.CompleteDuelHelp(benefactor)`와 `CompleteShelterHelp(benefactor)`는 `I=1` 이후 해당 은인의 도움 행동이 끝난 시점에 호출한다. 대련은 해당 구역, 구호소 재회는 거점이나 해당 구역 귀환로에서 받는다. 본편 지급에 연습 대련 선행은 필요하지 않다.

- 첫 지급에서는 장면 완료와 함께 `unlocked`, `charged`, `started`, `rewarded`, 본편 지급 출처 `story_grant`를 한 번 저장한다. 일부 연습 후 쌓아 둔 일반 전투 횟수는 첫 지급 시점부터 다시 세고, 기존 세션의 성장 상한은 보존한다.
- 연습에서 이미 해금한 슬롯은 장면 완료만 추가한다. 보유 1/1·소비 0/1, 재충전 진행 중인 일반 전투·대련 횟수는 그대로 이어진다. 같은 장면 재진입이나 다른 경로의 도움으로 추가 충전되지 않는다.
- 해당 기술의 일반 전투 유효 사용 **12회** 후 연습 대련에 입장하면 새 세션이 열린다. 그 세션의 조건 **3회** 성공으로 한 번 충전된다. 본편 지급 직후에도 실제 대련 조건을 채우면 검증 증거 `verified`를 얻을 수 있다. 12회 이전에는 이 검증으로 지원이 충전되지 않는다. 세션 성장 상한 8회 뒤에도 실제 대련 조건은 판정한다.
- 은인별 선택 열람은 실제 대련 증거를 읽는다. 이전 슬롯의 `unlocked`는 당시 실제 대련으로만 발급됐으므로 그 이력을 인정한다. 본편 지급으로만 해금한 슬롯은 연습 검증을 마친 뒤 선택 열람 조건을 충족한다. 선택 열람 수와 지원 수는 카셀 입장·엔딩 판정에 관여하지 않는다.

## 변경 파일

| 용도 | 경로 |
|---|---|
| 직전 기록 생성·복원·종료 처리 | `Assets/Scripts/Finale/EndingCheckpoint.cs`(신규), `FinalBattleService.cs`, `CasselShell.cs` |
| 슬롯 저장·재시도·장면 접점 | `Assets/Scripts/Progression/SkillSaveStore.cs`, `SkillSession.cs`, `StorySceneLinks.cs`(신규) |
| 승강기·슬롯 메뉴와 전투 종료 입력 | `Assets/Scripts/Shell/GameShell.cs`, `Assets/Scripts/GameBootstrap.cs` |
| 단서·단말·로그 표시 및 대련 보상 | `Assets/Scripts/Investigation/InvestigationService.cs`, `InvestigationShell.cs`, `Assets/Scripts/Trials/BenefactorTrialProgress.cs` |
| 승강기·직전 기록 메뉴 문구 | `Assets/Resources/Localization/{ko,en,ja,zh-Hans,de,fr,es,it,ru}.json` |
| 임시 슬롯 실기 검사 | `Assets/Editor/StoryLinkVerification.cs`(신규). 신규 C# 파일 3개에 Unity `.meta` 포함 |
| 정적 검사 | `tools/validate_story_links.py`(신규), `validate_gamedev.py`, `validate_investigation.py`, `validate_cassel.py`, `validate_benefactor_trials.py`, `validate_skeleton.py`, `validate_localization.py` |
| 역사적 해시 대조 | `tools/story_link_baseline.py`, `story-link-extensions.json`(신규), `input_scheme_baseline.py`, `localization_baseline.py` |
| 결과 | `unity/RewindNotary/STATIC-VALIDATION.json`, 본 보고서 |

슬롯 화면을 연결하며 기존 제목 호출에서 인자 안에 들어가 있던 `return`의 위치도 수정했다. 변경 전 정적 검사 오류 9건은 `18-release-checklist.md`의 기존 상태 갱신 한 줄에서 발생했다. 작업 중 아르곤의 PO 승인 문서 3개(`17-story-argon-v6-1.md`, `20-po-plan-argon.md`, `21-balance-hypothesis.md`)도 갱신됐다. 검증기는 기록된 차이만 역적용하여 기존 SHA-256과 일치하는지 검사한다. 아스트라 작업에서 이 네 문서의 본문을 편집하지 않았다. [아르곤 변경 내역](../../discussion/job-argon-step1-result.md)

## 검증 및 실기 인수

실행 명령은 저장소 루트 기준이다.

```bash
python3 tools/validate_gamedev.py --output unity/RewindNotary/STATIC-VALIDATION.json
```

**전체 6,080건 통과, 오류 0건.** 기존 카셀 반환 순열은 120/120이다. 신규 검사는 구 슬롯 단서 중복과 원본 조기 노출, 다섯 은인의 두 지급 경로, 연습 선소비, 11→12회 및 대련 2→3회 경계, 저장 파일 교체 실패와 재시도, 백업 회전 후 복귀, 다른 슬롯·손상 사본의 거부, 숙련과 결착 이력 보존을 포함한다.

1. Unity 6000.0.65f1에서 임포트·컴파일 후 **Rewind Notary → Story → Verify links in a temporary slot**을 실행한다. 실제 C# 서비스, `JsonUtility`, 파일 저장소로 장면 저장 실패→재시도, 일반 사용 11/12회와 대련 조건 3회, 기본 엔딩 저장 실패→재시도→재개·새 시도·삭제까지 점검한다. 검사용 슬롯과 로그는 임시 디렉터리에 생성한다.
2. 폐기 가능한 일반 슬롯의 기본 엔딩에서 결착 직후 종료·재실행하고, 승강기에서 직전 기록 재개와 카셀 직접 재도전을 각각 시험한다. 훈련 후 스테이지 선택으로 다시 재개해 기본검 400회와 소진한 지원이 보존되는지 확인한다. Windows64에서 파일 잠금·쓰기 실패·백업 복구, 세 슬롯 간 격리, F8 검증 기록 격리도 확인한다.
3. 대표 구역과 나머지 구역의 장면 배치 시 마르타 수령, 각 구역 유품 안내, 각 은인의 대련·구호소 도움 행동 완료 프레임에 접점을 연결한다. 행동 전 취소, 완료 직후 종료, 재진입을 시험한다. 코어에서는 원본을 갖고 있던 구 슬롯과 없는 슬롯 모두에서 조건 전 본편 로그, 2단계 선택, 1단계 미완료와 사본 누락을 각각 확인한다.
4. 승강기 고정 바닥과 연성로 방 배치가 완료되면 착지·왕복 동선을 확인한다. 현재 승강기 화면의 키보드·패드·마우스 선택, 작은 창과 9개 언어의 줄바꿈은 Play Mode에서 확인한다.
