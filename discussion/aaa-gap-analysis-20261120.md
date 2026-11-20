# AAA 품질 격차 분석 및 4단계 고도화 계획 — 《리와인드 공증록: 레아》

작성일: 2026-11-20  
작성: Gemini (전 영역 개발·에셋·스토리)  
공유 대상: Astra (Codex), Dots, Muse (PO), 사용자  
기준 문서: `31-quality-standard.md` (기준작: Hollow Knight · 록맨 · 산나비 · 환세취호전)

## 요약

시스템·서사 골격(56방·321행 스토리 연결, 9개 언어 번역, 스킬 6종·4단 성장 구조, 대시·패리·벽타기, 세이브 v4, 자동 QA 하네스)은 이미 갖춰져 있다. 그러나 **UI/HUD·애니메이션·조명/VFX·공간 일체감·사운드**의 5개 축이 아직 프로토타입 수준이어서 실제 플레이 첫인상과 AAA 목표 사이에 큰 격차가 남아 있다.

---

## 1. 최우선 보완 5대 항목 (S급 공백)

### 1) UI·HUD·대화창 전체가 구형 `OnGUI()`(IMGUI)로 구현됨
- **근거**: `Assets/Scripts/Shell/GameShell.cs`, `Assets/Scripts/GameBootstrap.cs`, `Assets/Scripts/Story/RepresentativeRoomLayout.cs`, `Assets/Scripts/Skills/SkillEvolutionPresentation.cs`
- 타이틀, 세이브 슬롯, 일시정지, 스킬창, 스토리 대화창, 인게임 체력바/보스바가 모두 회색 `GUI.Box`와 기본 버튼으로 렌더링된다.
- 스토리 대화창이 화면 전체를 어두운 단색(`Color(0.045, 0.065, 0.1, 0.97)`)으로 가린 채 텍스트만 스크롤 표시하며 초상화·표정·타이핑 연출이 없다.
- 폰트가 OS 동적 폰트(`Malgun Gothic` 등)에 의존해(`LocalizationFonts.cs`에 번들 폰트 없음) 비한글·타 OS 환경에서 9개 언어 글자 깨짐 위험이 크다.

### 2) 보스·적·플레이어 스프라이트와 애니메이션 부족
- **근거**: `Assets/Scripts/GameBootstrap.cs` (L97~110), `Assets/Scripts/SpriteAnimator.cs` (L62~79), `Assets/Resources/GameArt/sprites/`, `COMBAT-PASS-EVIDENCE/mikhail-knot.png`
- 은인 보스(미하일 등)와 최종 보스 카셀이 전부 `janhyang-sheet.png`(레아 복제 시트)에 색 틴트만 입혀 재사용된다.
- 일반 적 20종(`enemy-*-*.png`)이 모두 1프레임 정지 이미지이며 걷기·예고·공격·피격·사망 애니메이션이 없다.
- 레아 시트는 Idle(4)·Run(8)·Jump/Fall/Land(각 1)·Attack(4)뿐이며, 벽타기(현재 12도 회전 대체)·대시·패리·피격·사망·4단 콤보 구분·스킬 시전 모션과 성장 가시화(검 4단 + 의상 변화)가 미반영 상태다.

### 3) 조명·파티클·포스트프로세싱 부재 및 디버그성 이펙트
- **근거**: `Packages/manifest.json`, `Assets/Resources/Shaders/HitFlash.shader`, `Assets/Scripts/Skills/SkillFlourish.cs`, `Assets/Scripts/Story/DistrictTrap.cs`, `COMBAT-PASS-EVIDENCE/skill-effects.png`, `velvet-trap.png`, `entry.png`
- `Packages/manifest.json`에 `com.unity.ugui`, `com.unity.modules.particlesystem`, 2D 조명/포스트프로세싱 구성이 없고 셰이더도 `HitFlash.shader` 1종뿐이다.
- 스킬·트랩·투사체가 `CombatInk` 벡터 도형과 주황/노란 히트박스 테두리로 그려지며, 이미 보유한 `Assets/Resources/GameArt/sprites/fx-*-additive.png` 6장이 인게임 전투 이펙트에 연결되지 않았다.

### 4) 배경 일러스트와 발판·프롭의 시각적 분리
- **근거**: `ART-FIX-OPERA-02.png`, `ART-FIX-BELL-02.png`, `ART-FIX-CLOCK-02.png`, `ART-FIX-HUB-WORKROOM.png`, `Assets/Scripts/Story/StoryTerrain.cs`
- 고품질 배경 일러스트 위에 얇고 끝단 캡·지지 구조·그림자가 없는 반복 띠 발판이 공중에 떠 보이고, 문(`door-*.png`)도 벽체/프레임 없이 단독 배치돼 있다.
- `ART-FIX-HUB-WORKROOM.png` 좌측에 하늘색 세로 막대 마커가 잔존한다.

### 5) 사운드: OST 18곡 미연결 + 코드 합성 SFX + 환경음 부재
- **근거**: `Assets/Scripts/AudioManager.cs`
- 인게임 BGM이 `field.wav`, `boss.wav` 2곡뿐이며(OST 18곡은 `origin/main`에 수록 완료, `사운드 패스`에서 연결 예정), 은인 스킬 5종 효과음이 런타임 사인파/노이즈 합성(`TechniqueTone`)이다.
- 발소리, 대시, 벽 슬라이드, UI 조작음, 구역별 환경음(앰비언스), 피치/볼륨 랜덤화가 빠져 있다.

---

## 2. 게임플레이·연출 추가 항목 (A급 공백)

1. **보스전·장면 전환 연출**: 보스 인트로(카메라 락·이름 배너·1초 정적), 방 전환 페이드, 구역명 타이틀 카드, 보스 처치/패리 슬로우모션, 카메라 선행 시야(Look-ahead)·상하 살피기.
2. **탐험 구조(할로우나이트 축)**: 56방 구역 미니맵/전체맵, 숨겨진 방·수집품, 체크포인트(공증대) 휴식·회복 연출, 빠른 이동 UI.
3. **전투·조작 편의(록맨·산나비 축)**: 키 리매핑 실구현(`GameInput.cs` 하드코딩 해소), 스킬 선택 UI/단축 슬롯, 공중 하단 베기(포고), 피격 넉백·적 소멸 연출.
4. **설정·접근성**: 해상도·창모드/전체화면·VSync·화면 흔들림 강도·컨트롤러 버튼 글리프 자동 전환.

---

## 3. 4단계 실행 로드맵 및 병렬 작업 경계

| 단계 | 핵심 범위 | 담당 / 협업 메모 |
| --- | --- | --- |
| **1단계 (진행 중)** | **uGUI UI/HUD·초상화 스토리 대화창 전면 개편 + 9개 언어 OFL 폰트 임베드 + 카메라 포스트프로세싱(Bloom·Vignette·구역 색보정·방 전환 페이드) + ParticleSystem(구역 환경 입자·타격/착지/대시/벽타기 VFX) + `fx-*-additive.png` 스킬 이펙트 연결 + 접촉 그림자** | **Gemini 착수** (기준 커밋 `9ad645b`, 독립 워크트리에서 개발 후 `main` 푸시·바탕화면 빌드 교체). Astra의 `전투·조우 패스` 작업 파일(`Wave*`, `DistrictTrap`, `BenefactorBout`, `EchoReprise`)은 건드리지 않음. |
| **2단계** | 보스 6인(은인 5인 + 카셀) 전용 스프라이트 시트 제작, 일반 적 동작 시트화, 레아 추가 모션(4단 콤보·대시·패리·벽타기·스킬) + 성장 가시화(검 4단·의상) | Gemini / Dots / Astra 분담 (보드에서 조율) |
| **3단계** | 지형 타일셋(끝단 캡·지지대·문 프레임·전경 실루엣·배경 명도 분리) + OST 18곡·구역 환경음·프로 SFX 전면 교체 | 사운드: Dots(`사운드 패스`), 지형·거점/최종 비주얼: Astra·Gemini 분담 |
| **4단계** | 보스 인트로·피니시 슬로우·카메라 피크 + 56방 구역 지도(미니맵) + 키 리매핑·디스플레이 설정·컨트롤러 아이콘 전환 | 1~3단계 완료 후 순차 진행 |

---

## 4. 다른 AI 작업자(Astra·Dots)를 위한 충돌 방지 안내

- **Gemini 1단계가 새로 추가·관리하는 파일**:
  - `Packages/manifest.json` (`com.unity.ugui`, `com.unity.modules.particlesystem` 추가 — 기존 패키지 전부 유지)
  - `Assets/Resources/Fonts/*` (9개 언어 OFL 폰트 에셋)
  - `Assets/Resources/GameArt/portraits/*` (화자 12인 초상화 에셋)
  - `Assets/Resources/Shaders/AtmospherePost.shader` (Built-in 호환 포스트프로세싱 — `HitFlash.shader` 무변경)
  - `Assets/Scripts/UI/*` (신규 uGUI 타이틀·메뉴·HUD·초상화 대화창·진화 배너·페이드 오버레이)
  - `Assets/Scripts/Vfx/*` (신규 포스트프로세싱·2D 분위기 조명·파티클·접촉 그림자·`fx-*-additive` 연동)
- **기존 스크립트 최소 훅**:
  - `GameShell.cs`, `GameBootstrap.cs`, `RepresentativeRoomLayout.cs`, `SkillEvolutionPresentation.cs`, `LocalizationFonts.cs`는 기존 퍼블릭 API·`AutoTest` 계약을 100% 유지하면서 렌더링/연출만 신규 UI·VFX 모듈로 위임한다.
  - Astra가 진행 중인 `전투·조우 패스` 핵심 로직(`Wave*`, `BenefactorTrialOpponent`, `DistrictTrap`, `Janhyang*`, `Cassel*`, `CombatMotor2D` 판정 로직)은 수정하지 않는다.
