# 구현·검증 보고서

작성: 2026-11-17. 의뢰: `codex-gamedev-brief.md`. 기준 스토리: v6.1.

**Phase A의 5종 PNG와 Phase B 프로젝트 파일을 순서대로 생성하고, 최종 검토에서 시트를 정수 격자로 개선했다. Unity 에디터 및 C# 컴파일러가 없으므로 실제 실행 가능한 빌드나 Play 성공을 확인한 결과는 아니다.** 원본 발 정렬 등 미달 항목을 아래에 구분했다. GitHub 푸시는 하지 않았다.

## 구현 목록

| 항목 | 결과 |
|---|---|
| 레아/잔향 시트 | 둘 다 2048×768, 8×4 / 256×192 정수 셀, 유효 19포즈, 오른쪽 옆모습, RGBA |
| 배경 3종 | 라피스 원경, 투명 중경, 석조 지면 PNG |
| 프로젝트 | Unity 6000.0.65f1 기록, Built-in 렌더러, 2D 모드, 50 Hz 물리, 구 Input API |
| Packages | 2D Sprite 1.0.0, Cinemachine 3.1.7, 사용 엔진 모듈 명시 |
| Main 씬 | 카메라와 GameBootstrap만 포함한 YAML, 고정 GUID와 Build Settings 연결 |
| GameBootstrap | 레벨·캐릭터·보스·카메라·패럴랙스 생성, HUD/체력바, 아레나 체크포인트, 승리/재시작 |
| LevelBuilder | 지면·7개 발판·4유닛 틈·시작·아레나, 단일 플랫폼 콜라이더와 독립 반복 텍스처 |
| PlayerController2D | 좌우 6.5유닛/초, 점프 12유닛/초, 코요테/버퍼 각 0.1초, 짧은 점프 |
| SpriteAnimator | Resources Texture2D → Sprite.Create, 상태별 프레임, flipX, 발 피벗 JSON, 생성 Sprite 정리 |
| Health / CombatMotor2D | 체력, 피격 무적, 넉백/경직, 0.32초 공격 중 0.16초에 1회 타격 |
| JanhyangBoss | 아레나 진입 시 활성화, 접근·점프·0.45초 예고·검 공격·회복 시간 |
| 카메라 | Cinemachine 3 실제 API로 추적, 코드 카메라 대체 사용 안 함 |
| ParallaxBackground | far 0.12 / mid 0.45, 각 5개 패널 재활용, 전역 타일 인덱스로 반전 상태 유지 |
| 에디터 초기화 | 첫 시작 씬 지정, 원본 PNG 크기/알파 보존, NPOT 축소·압축·mipmap 비활성화 |

## 파일 목록

최종 전체 파일 목록은 [FILE-LIST.txt](FILE-LIST.txt)에 기록한다. 각 Assets 파일과 폴더에는 `.meta`가 있다.

```text
RewindNotary/
  Packages/manifest.json
  ProjectSettings/
    ProjectVersion.txt, ProjectSettings.asset, EditorSettings.asset
    EditorBuildSettings.asset, TagManager.asset, TimeManager.asset, InputManager.asset
  Assets/
    Scenes/Main.unity
    Editor/PrototypeProjectSetup.cs
    Scripts/
      GameBootstrap.cs, LevelBuilder.cs, PlayerController2D.cs
      SpriteAnimator.cs, Health.cs, CombatMotor2D.cs
      JanhyangBoss.cs, ParallaxBackground.cs, CameraTarget2D.cs
    Resources/GameArt/
      sprites/rea-sheet.png, janhyang-sheet.png
      sprites/rea-sheet-layout.json, janhyang-sheet-layout.json
      backgrounds/stage1-far.png, stage1-mid.png, stage1-ground.png
  README.md, BUILD-REPORT.md, FILE-LIST.txt, STATIC-VALIDATION.json, .gitignore
```

상위 프로젝트에 `assets/game/GAME-ASSET-LIST.md`, 생성 프롬프트/초안/검사 자료, `tools/validate_gamedev.py`, `gamedev-progress.md`가 추가되었다. 프로젝트 내부 GameArt는 독립 복사본이므로 원본 폴더 없이 프로젝트를 옮겨도 리소스 참조 경로는 유지된다.

## 검증 결과와 범위

`STATIC-VALIDATION.json`에 상세 결과가 있다. 최초 Phase A/B 검사는 215개 통과했고, 사운드·타격감 추가 후 확장 검사는 **392개 통과, 오류 0개**다. 추가 내용은 [SOUND-REPORT.md](SOUND-REPORT.md)에 기록했다. 이는 C# 컴파일/Unity 실행 성공을 의미하지 않는다.

- 기존 Markdown 문서, 모든 `assets/concept/` 파일, 주인공 참고 이미지의 작업 전 SHA-256과 비교하여 무변경 확인.
- PNG 5개 디코딩·투명 채널, 원본/Unity 복사본 SHA-256 일치 확인.
- 각 시트의 유효 19셀과 피벗 메타데이터, 텍스처 크기 보존 설정 확인.
- 씬의 로컬 fileID, Bootstrap 스크립트 GUID, Build Settings 씬 GUID, `.meta` 존재/중복 검사.
- ProjectSettings YAML, 필수 패키지 선언, Ground 레이어 8, 구 Input 설정 검사.
- C# 문자열/주석을 제외한 구분자 균형 검사 및 코드 수동 검토. **C# 구문 파서나 컴파일러 검증은 아님.** 추가 파서 설치 시도는 환경의 네트워크 제한으로 실패했다.

확인한 공식 API 근거: [Sprite.Create](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Sprite.Create.html), [Physics2D.OverlapBox](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics2D.OverlapBox.html), [CinemachineCamera](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/api/Unity.Cinemachine.CinemachineCamera.html), [CinemachinePositionComposer](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/api/Unity.Cinemachine.CinemachinePositionComposer.html), [LensSettings](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/api/Unity.Cinemachine.LensSettings.html). 패키지 호환 근거: [Unity 6000.0 Cinemachine 목록](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/packages-all/pack-safe/com-unity-cinemachine).

## 알려진 한계·미검증 사항

1. **원본 발 기준선 편차.** 최종 시트는 둘 다 2048×768, 셀 256×192로 정수 격자 요건을 충족한다. 다만 원본 발 기준선은 셀 내 y=174~186에 분포하여 완벽한 픽셀 정렬에는 미달한다. 읽기 전용 분석으로 산출한 발 피벗 JSON을 Sprite.Create에 적용해 지면 위치를 맞춘다. 렌더 결과는 PC에서 확인해야 한다. PNG 픽셀 편집은 전부 내장 생성 도구에서 수행했으며 Python 후처리는 하지 않았다.
2. **그림 품질.** 프레임별 중심/크기·포즈 차이와 반투명 외곽 흔적이 남아 있다. 일부 달리기 포즈가 비슷하다. 시트는 프로토타입용이며 최종 애니메이터의 정밀 정리 대상이다.
3. **타일 경계.** 원본 지면은 좌우 끝이 완전히 일치하지 않는다(RGB 평균 차이 15.077/255). 런타임에서 A/좌우반전A를 번갈아 배치해 경계 픽셀을 맞춘다. 반복 무늬와 대칭은 눈에 띌 수 있다. 원경/중경도 같은 방식을 쓴다.
4. **환경 미검증.** Unity import, 패키지 실제 resolve, C# 컴파일, Play Mode, 사용자 PC별 그래픽, Standalone 빌드는 실행하지 못했다. `Library/`, `packages-lock.json`, 빌드 실행 파일은 임의로 만들어 넣지 않았다. 첫 import에서 Unity가 기본 설정과 패키지 lock을 생성한다.
5. **플레이 튜닝.** 캐릭터 콜라이더는 고정 캡슐이고 공격은 전방 박스다. 검 픽셀을 따라가는 정밀 판정/벽에 막히는 검/복잡한 보스 길 찾기는 없다. 캐릭터끼리 밀지 않도록 상호 물리 충돌을 무시한다. 보스는 발판 아래에서 잠시 막히거나 점프 경로가 비효율적일 수 있다.
6. **범위.** 후속 의뢰로 사운드·타격감 연출을 추가했다([SOUND-REPORT.md](SOUND-REPORT.md)). 게임패드·메뉴·세이브·대사·컷신·시간 회귀·완성 스테이지는 포함하지 않았다. HUD는 영어이며 보스 처치 문구는 프로토타입 완료 표시일 뿐 스토리 결말이 아니다.

사용자 PC 검증 순서는 README의 체크리스트에 정리했다. 스토리와 기존 컨셉 폴더는 수정하지 않았다.
