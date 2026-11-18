# 번역 후속 패스 검증 보고서

2026-11-18 · `python3 tools/validate_gamedev.py` **7,037건 통과, 오류 0건**. 시작 시점은 6,949건 통과, 오류 65건이었다. 언어 파일 9개의 8,847개 값 가운데 획득 알림 45개와 지원 기술 라벨 32개를 수정했다. [최종 정적 검증 JSON](STATIC-VALIDATION.json)

## 1. 기술 획득 안내

다섯 키에는 장면 지시, 대사, 시스템 알림이 함께 들어 있다. 각 언어에서 마지막 알림의 기술명 다음에 오는 조작법만 교체했다. 수정 전후 대사와 기술명까지의 접두부, 줄바꿈, 끝 괄호·인용부호를 대조했다. 표시 명칭은 용어집 §3-2와 HUD의 동결 표기를 따랐다. [입력 보고서](INPUT-SCHEME-REPORT.md) · [GameInput.cs](Assets/Scripts/Shell/GameInput.cs) · [언어별 톤 가이드](LOCALIZATION-REPORT.md)

| 키 | 게임 내 기술명 | Q 또는 패드 Y를 누르는 순간 |
|---|---|---|
| `story.mikhail.i0.04` | 은실 박음질 | 방향 입력을 놓은 상태 |
| `story.julius.i0.05` | 진자 되감기 | 레아가 바라보는 쪽과 반대인 가로 방향을 홀드 |
| `story.valeria.i0.05` | 커튼콜 원무 | 위를 홀드 |
| `story.gabriel.i0.05` | 공명 단조 | 공중에서 아래를 홀드 |
| `story.irina.i0.05` | 여백 도려내기 | 지상에서 아래를 홀드 |

각 키를 ko·en·de·fr·es·zh-Hans·ja·ru·it에 적용했다. ‘무대인사 왈츠’와 ‘화폭 조각’에 해당하는 HUD 이름은 각각 ‘커튼콜 원무’와 ‘여백 도려내기’다. 한국어 파일에서 변경된 키는 위 5개다.

## 2. 지원 기술 명칭

대련 카탈로그에서 지원 효과의 `label`이 참조하는 키와 한국어·외국어 테이블 내 기존 명칭의 사용처를 대조했다. 사용 알림 `support.used_notice`는 번역된 이름을 인자 `{1}`로 받는다. 아래 5개 라벨 × 9개 언어를 용어집과 직접 비교한 결과는 **45/45 일치**다. [용어집 §3-2](../../26-terminology-glossary.md) · [대련 카탈로그](Assets/Resources/Trials/catalog.json) · [지원 사용 알림](Assets/Scripts/Trials/BenefactorSupportController.cs)

| 대상 UI 키 | 동결 ko / en | 실제로 값이 바뀐 언어 |
|---|---|---|
| `data.trials.catalog.trials.mikhail.support.label` | 귀환 솔기 / Return Seam | en, de, fr, es, zh-Hans, ru, it |
| `data.trials.catalog.trials.julius.support.label` | 귀환 시간 / Return Time | en, de, fr, es, zh-Hans, ja, ru, it |
| `data.trials.catalog.trials.valeria.support.label` | 착지 원무 / Landing Waltz | fr, es, it |
| `data.trials.catalog.trials.gabriel.support.label` | 귀환 종격 / Return Bell Strike | en, de, fr, es, zh-Hans, ru, it |
| `data.trials.catalog.trials.irina.support.label` | 귀환 채색 / Return Stroke | en, de, fr, es, zh-Hans, ru, it |

## 3. 숫자 검증

기존 검사는 연속된 숫자만 떼어 비교해 소수의 정수부와 소수부가 분리됐다. 새 검사는 플레이스홀더를 분리하고, 숫자를 로케일의 소수점·세 자리 구분자로 읽어 정확한 십진수 값과 출현 횟수를 비교한다. de·es·it의 점 자릿수 구분, fr·ru의 일반 공백과 줄바꿈 방지 공백, 다섯 언어의 소수점 쉼표를 처리한다. 시각은 시·분·초의 순서를 보존한다. `06시 30분`, `06 h 30`, `6:30`은 같은 시각이다. 카셀 제0식과 레아류 제1식은 독일어·러시아어 격변화를 포함한 동결 고유명사에서 수를 읽는다. [숫자 비교와 회귀 사례](../../tools/validate_localization.py)

기존 오탐은 아래 17키 × 8개 언어, **136셀 → 0셀**이다. 종전 정적 결과에서는 언어별 1건씩 총 8개 오류로 집계됐다. [A묶음 숫자 이관 목록](../../discussion/job-argon-translate-a-result.md)

| 영역 | 숫자 오탐 해소 키 |
|---|---|
| 조사 기록 | `investigation.record.core_waveform.body`, `investigation.record.preservation_order.body`, `investigation.record.discarded_draft.body` |
| 카셀전 | `story.cassel.p1.04`, `story.cassel.p2.08_r1_cassel`, `story.cassel.p3.01_open`, `story.cassel.p3.02_live_b3`, `story.cassel.p3.03_b3_ready`, `story.cassel.p3.05_true_hit` |
| 진 엔딩 | `story.ending.true.11_marta_ethan`, `story.ending.true.12_blank_slate`, `story.ending.true.14_registered` |
| 재개 | `story.resume.lift.02_echo`, `story.resume.crucible.02_theo`, `story.resume.crucible.03_ethan` |
| 후일담·표적 | `story.epilogue.05_rea_final`, `area.target.rea_form_one_sky` |

정상 표기 변형과 실제 값 변경을 각각 검사했다. 독일어 `story.cassel.p2.12_r4`의 지연 시간을 메모리에서 `0,12s → 0,012s`로 바꾸자 전체 검증기가 숫자 불일치로 해당 키를 지목했다. 부호 반전, 시·분 교환, 두 소수의 소수부 교환, 제0식과 제1식 교환도 개별 회귀 검사에서 검출했다.

## 4. 단독 잔향전 흐름 검사

`GameBootstrap.Build`의 본문 안에서 `if (echoDuel)` 바로 뒤에 공백과 여는 중괄호가 오는 분기를 추출한다. 한 줄 인카운터 ID 대입과 다른 메서드의 동명 분기가 앞뒤에 있는 실제 소스로 검증했다. 결함 주입은 각각 같은 원본의 메모리 복사본에 적용했다. [본문 추출](../../tools/validate_gamedev.py) · [흐름 검사](../../tools/validate_flow_assembly.py) · [기존 재검증 기록](FLOW-ASSEMBLY-REPORT.md)

| 검사 입력 | 흐름 결과 | 판정 |
|---|---:|---|
| 수정 전 추출기 + 원본 게임 코드 | 24/25 | 아레나 분기 오탐 1건 |
| 수정 후 추출기 + 원본 게임 코드 | 25/25 | 통과 |
| 해당 분기의 플레이어 시작점을 `ArenaStart`에서 `BossStart`로 이동 | 24/25 | 단독 잔향전 검사 실패 |
| 해당 분기에서 `bossAI.Activate();` 제거 | 24/25 | 단독 잔향전 검사 실패 |

## 5. 전수 검사와 기준선

수정 전 9개 파일을 보관해 JSON 중복 멤버, 중복 키, 순서, 값, 한글, 플레이스홀더를 검사했다. 감수 대상 594키 × 9개 언어의 전문도 Muse의 감수 당시 덤프와 일치했다. 문구 수정 후 같은 전수 검사를 다시 통과한 뒤 기준선을 기록했다. 기존 CF-03 기술명은 9개 언어 모두 수정 전 값과 일치한다. 세이브 v4와 런타임 C# 파일의 변경은 0개다. [Muse 감수 결과](../../discussion/job-claude-final-review-result.md) · [감수 당시 전문](../../discussion/review/dump-all.md)

| 전수 검사 | 변경 전 | 변경 후 |
|---|---:|---:|
| 유효 JSON / 지원 언어 | 9/9 | 9/9 |
| 언어별 키 수 | 983 | 983 |
| ko 대비 키·순서 일치 | 100% | 100% |
| 중복 키·빈 값 | 0 / 0 | 0 / 0 |
| 외국어 7,864셀 중 한글 잔존 | 0 | 0 |
| 플레이스홀더 인덱스·반복 횟수·서식 불일치 | 0 | 0 |
| 정적 검사 통과 / 오류 / 경고 | 6,949 / 65 / 21 | 7,037 / 0 / 21 |

언어 선택기의 `language.ko`는 9개 언어 모두 자칭 `한국어`인지 따로 검사했다. 외국어 한글 잔존 집계는 이 키를 제외한다. 기존 미번역 집계 8셀은 `area.travel`의 공통 입력 표기 `F / L3 · {0}`였다. 이 문구를 보존하고 완료 집계의 한글 잔존 수를 바로잡았다.

### 기준선 변경 내역

| 기존 실패 검사군 | 전 → 후 | 현재 대조 대상 |
|---|---:|---|
| 로케일 숫자 | 8 → 0 | 로케일 숫자값과 고유명사의 식 번호 |
| 입력 체계 이전 바이트 / 스토리 역패치 | 8 + 8 → 0 | 승인된 후속본 → 감수 당시 파일 → 이관 당시 파일 → 기존 역사적 SHA-256 |
| 대표 구역 / 4구역 / 최종 구간 | 16 + 8 + 16 → 0 | 한국어는 납품 원고와 갱신 안내, 각 언어 전문·라벨은 해당 언어 감수본과 대조 |
| 단독 잔향전 | 1 → 0 | `Build` 안의 실제 아레나 진입 분기 |

A·B의 합집합은 524키다. 감수 덤프가 담은 이관 키 523개와 A묶음의 `skills.forge.form3`를 사용했다. 기준선은 후속본 SHA-256을 확인한 뒤 이번 안내와 라벨만 되돌려 감수본의 파일 해시를 확인한다. 이후 이관 범위에 한해 당시의 한국어 값을 복원하고 기존 최종 구간 해시와 대조한다. 언어별로 523개 값이 번역됐고 `area.travel` 1개는 공통 표기였다. 복원된 9개 파일은 기존 스토리·입력 역패치까지 통과했다. 별도 결함 주입에서 키 순서 변경, 한글 자모 삽입, 플레이스홀더 인덱스 변경, 승인 영역 밖 값 변경을 검출했다. [후속 해시·역패치 자료](../../tools/localization-followup.json) · [감수본 대조](../../tools/localization_followup.py) · [기존 역패치 연결](../../tools/story_link_baseline.py)

| 언어 | 이관 당시 SHA-256 | 이번 변경 직전 SHA-256 | 후속 SHA-256 | 실제 변경 값 |
|---|---|---|---|---:|
| ko | `ad83da3a14f2` | `ad83da3a14f2` | `050cfaae1da1` | 5 |
| en | `102dbc563c17` | `892c98667d0e` | `f4d2081c77ff` | 9 |
| de | `05e0803dd9c9` | `bbf67f67f0e8` | `bc0ad973cf2a` | 9 |
| fr | `4ebc56eaee8b` | `43ccc81c9a8f` | `c24c82287a2a` | 10 |
| es | `94de8c99911d` | `a76586393a98` | `da656afe40c7` | 10 |
| zh-Hans | `1b4689dbc46b` | `9e15eac749a7` | `3c8baf188ebe` | 9 |
| ja | `33e0e4123d22` | `ea69697a979b` | `c3f3ce2e692e` | 6 |
| ru | `389f22472d08` | `436c00490397` | `c4658250bc72` | 9 |
| it | `783d6de2eb28` | `8ea45c74ccb8` | `c358a54f6817` | 10 |

표에는 식별용 앞 12자리를 실었다. 실제 검사는 SHA-256 전체를 비교한다. 최종 경고 21건에는 Unity Play Mode, 실제 패드 입력, 언어별 폰트와 줄바꿈 등 정적 검사의 범위 밖인 항목이 포함된다. 이번에 새로 쓴 조작 안내는 Muse의 11월 18일 감수 후 추가됐다.

## 산출물과 경로 기준

[푸시 매니페스트](../../push-paths-locfollow.bin)는 Unity 프로젝트 루트에서 바로 읽을 수 있다. 로케일 JSON 9개, 이 리포트, 최종 정적 검증 JSON을 담았다. [전체 변경 목록](../../push-paths-locfollow-all.bin)은 같은 Unity 루트 기준으로 위 11개와 프로젝트 밖의 검증 도구·자료 9개(`../../tools/`), 두 매니페스트 자체를 포함한다. 두 목록 모두 UTF-8 상대 경로마다 마지막 경로까지 NUL 바이트를 붙였다.

| 용도 | 워크스페이스 파일 |
|---|---|
| 숫자·키 순서·한글·동결 표기 검사 | `tools/validate_localization.py` |
| 아레나 본문 추출과 흐름 검사 | `tools/validate_gamedev.py`, `tools/validate_flow_assembly.py` |
| 대표 구역·4구역·최종 구간 전문 대조 | `tools/validate_scene_links.py`, `tools/validate_area_links.py`, `tools/validate_finale_links.py` |
| 감수본·후속 해시·역패치 | `tools/localization_followup.py`, `tools/localization-followup.json`, `tools/story_link_baseline.py` |
