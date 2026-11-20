# 작업 지시서 — AAA 이미지 에셋 제작 (담당: Astra / Codex)

작성일: 2026-11-20  
배정 근거: 사용자 지시 (2026-11-20 — 이미지 생성 작업은 Codex 전담, Gemini는 2단계 코드·연출·드롭인 로더 구현)  
참고 화풍 기준: `Assets/Resources/GameArt/portraits/portrait_rea.png`, `Assets/Resources/GameArt/ui/title-keyart.png`, `Assets/Resources/GameArt/sprites/rea-sheet.png`

모든 에셋은 **드롭인(Zero-code drop-in) 계약**으로 게임 코드에 이미 연결된다. 아래 경로와 규격에 맞춰 PNG만 배치하면 코드 수정 없이 인게임에 즉시 반영된다.

---

## Set A. 대화창 NPC 초상화 10장 (우선순위 1 — AAA 1단계 보완)

- **규격**: `512 × 512` PNG (RGBA, 텍스트/워터마크 금지, Vanillaware/Atelier 계열 회화풍 바스트 초상화, `portrait_rea.png`와 동일 화풍·조명 밀도)
- **로더**: `NotaryUiTheme.LoadPortrait(speakerId)` (`Assets/Scripts/UI/NotaryUiTheme.cs`)
- **배치 경로**: `Assets/Resources/GameArt/portraits/`
  1. `portrait_marta.png` — 마르타 할머니 (은발 쪽머리, 크림색 숄, 따뜻한 다락방 등불 배경)
  2. `portrait_mikhail.png` — 미하일 (벨벳 재단 거리 장인, 버건디 벨벳 조끼·은바늘 라펠, 중년 신사)
  3. `portrait_ethan.png` — 에단 (대장장이/숫돌 장인, 가죽 앞치마, 따뜻한 화로 빛 배경)
  4. `portrait_theo.png` — 테오 (청년 목수/기술자, 리넨 소매, 가죽 공구 벨트)
  5. `portrait_luka.png` — 루카 (푸른 비단 리본을 지닌 온화한 동료, 여행 외투)
  6. `portrait_julius.png` — 율리우스 (시계탑 장인, 황동 단안경·기어 문양 코트, 금빛 시계탑 배경)
  7. `portrait_valeria.png` — 발레리아 (유리 오페라 극장 프리마돈나, 진홍·상아색 무대 의상·깃털 머리장식)
  8. `portrait_gabriel.png` — 가브리엘 (대종루 주조 장인, 청동 망토·소리굽쇠 문장, 푸른 수로 배경)
  9. `portrait_irina.png` — 이리나 (화랑 화가, 크림 베레모·물감 묻은 작업복, 햇빛 화실 배경)
  10. `portrait_cassel.png` — 대공증관 카셀 (백금·고금 의례용 법복, 천칭 문장, 냉철한 눈매)

---

## Set B. 보스 6인 전용 스프라이트 시트 (우선순위 2 — AAA 2단계)

- **규격**: `2048 × 768` 투명 배경 RGBA PNG, 정확한 `8 × 4` 정수 격자(셀 `256 × 192`, `rea-sheet.png`·`janhyang-sheet.png`와 동일 구조)
  - Row 0 (col 0~3): Idle 4프레임
  - Row 1 (col 0~7): Run/Move 8프레임
  - Row 2 (col 0~2): Jump / Fall / Land 각 1프레임
  - Row 3 (col 0~3): Attack 4프레임 (준비 → 거상 → 타격 → 회수)
- **로더**: `BossVisualIdentity2D` (`Assets/Scripts/Vfx/BossVisualIdentity2D.cs`)가 자동으로 아래 리소스명을 탐색해 교체한다.
- **배치 경로**: `Assets/Resources/GameArt/sprites/`
  1. `boss-mikhail-sheet.png`
  2. `boss-julius-sheet.png`
  3. `boss-valeria-sheet.png`
  4. `boss-gabriel-sheet.png`
  5. `boss-irina-sheet.png`
  6. `boss-cassel-sheet.png`

---

## Set C. 일반 적 애니메이션 스프라이트 시트 20종 (우선순위 3 — AAA 2단계)

- **규격**: `1024 × 512` 투명 배경 RGBA PNG, `4 × 2` 격자(셀 `256 × 256`)
  - Row 0: col 0~1 Idle(2f), col 2~3 Move/Walk(2f)
  - Row 1: col 0 Attack Windup(1f), col 1~2 Attack Strike(2f), col 3 Hit/Recoil(1f)
- **로더**: `EnemyMotionPolish2D` (`Assets/Scripts/Vfx/EnemyMotionPolish2D.cs`)가 기존 단일 PNG 이름 뒤에 `-sheet`가 붙은 파일을 자동 감지해 슬라이싱·재생한다.
- **배치 경로**: `Assets/Resources/GameArt/sprites/`
  - `enemy-{notary-guard|reflection-statue|shell-sentry|sword-latch}-{velvet|clock|opera|bell|gallery}-sheet.png`

---

## Set D. 레아 성장 단계별 시트 (선택 확장 — 성장 가시화 고도화)

- **규격**: `2048 × 768` RGBA PNG (`rea-sheet.png`와 동일 8×4 격자 및 발 피벗)
- **로더**: `ReaGrowthVisual2D` (`Assets/Scripts/Vfx/ReaGrowthVisual2D.cs`)
- **배치 경로**: `Assets/Resources/GameArt/sprites/`
  - `rea-sheet-tier2.png` (2단계 진화 검 + 초기 은인 흔적)
  - `rea-sheet-tier3.png` (3단계 공증 대검 + 중기 의상 장식)
  - `rea-sheet-tier4.png` (최종 각성 검 + 5은인 결속 의상)
