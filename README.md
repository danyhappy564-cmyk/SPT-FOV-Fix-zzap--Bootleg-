### ⚠️ IMPORTANT NOTICE / DISCLAIMER

**Original Author:** Fontaine
**Original Repository:** SPT-FOV-Fix
**Original Link:** https://github.com/space-commits/SPT-FOV-Fix
**License:** See upstream repository
**This Port By:** R_F (danyhappy564-cmyk) — unofficial, AI-assisted port. Not affiliated with or endorsed by the original author.

1. **Reflection & Take-Downs:** I deeply reflect on the ECOT incident. As an AI-assisted "vibe coder," I will immediately delete files if the original authors ask.
2. **No Re-Distribution:** These ported builds are unverified, temporary fixes. Please do NOT re-upload or share them anywhere else.
3. **Do Not Pester Original Authors:** Never report bugs or pester original modders regarding issues from my unofficial ports.
4. **Full Credit & Respect:** I will always credit original creators on GitHub and prioritize their decisions above all else.
5. **Support Original Creators:** Instead of using my ports, please visit the original authors' Forge pages to leave kind words or tips.

---

## 변경 이력

- 2026-10-01 20:27 — **Realism Mod 연동 완전 삭제.** Realism은 SPT 3.x 이후 업데이트가 끊겨서 더는
  안 씁니다. `RealismCompat.cs`(Realism의 자세 상태를 읽어오던 파일)와 Realism을 찾는 코드를 지우고,
  조준 카메라 계산(`LerpCamera`)에서 Realism 전용 분기(기관권총 보정, 어깨 견착 권총, 자세 전환
  스무딩, 충돌 시 카메라 정지 등)를 걷어냈습니다. Realism 없이 쓸 때 원래 타던 경로만 남겨서
  **게임 내 동작은 그대로**입니다.
- 2026-10-01 20:30 — **원작 4.1.0 업데이트(`e3ece13`) 머지.** 가져온 것:
  ① 조준 중 카메라 반동 누락 수정 — 4.1 게임은 카메라 반동에 "무기 반동(`WeaponRecoilEffect`)"
  항목을 하나 더 더하는데, 우리 포트의 `LerpCamera`(조준 시 카메라 위치·회전을 매 프레임
  계산하는 함수) 대체 코드는 4.0 본문 그대로라 그걸 빼먹고 있었음,
  ② 조준 카메라 앞뒤(z축) 이동 속도가 무기의 조준 속도(에르고)에 비례하도록 변경,
  ③ FOV Scale 수정 기능을 `SetCompensationScale`(플레이어 모델 배율을 실제 적용하는 함수)에
  걸도록 바꾸고 **내 캐릭터에만** 적용(예전엔 봇에게도 적용됨), 켜고 끌 때 즉시 반영,
  ④ 조준 3인칭 애니메이션 끄는 패치가 플레이어를 찾을 때 `GetComponent` 대신 필드를 읽도록 변경,
  ⑤ 광학 조준경 카메라 거리 기본값 -0.03 → -0.015 (새로 생성되는 설정 파일에만 적용).
  안 가져온 것: 고개 돌리기 각도 설정(`Free Look Angle`), 기본 FOV 범위 설정 삭제(30~120 고정),
  Realism 연동 주석 처리, 원작자 PC 경로가 박힌 csproj. 소스는 원작처럼 `src/` 폴더로 이동.
- 2026-09-08 04:26 — SPT 4.1 포팅 (아래 "4.1 포팅에서 바뀐 것")

---

# Fontaine's FOV Fix (fork)

> **원작자 · 원본**
> **Fontaine** — https://github.com/space-commits/SPT-FOV-Fix
>
> 이 레포는 위 원작의 **포크**입니다. 원작 **4.1.0**(`e3ece13`)까지 머지돼 있습니다.
> 원작과 다른 점은 아래 "원작 4.1.0과 다른 점" 표에 정리했습니다.

ADS할 때 FOV가 줄어드는 걸 없애고, 조준 카메라 위치·속도, 기본 FOV 범위(50~75 제한
해제), 배율별 마우스 감도, 토글 줌 등을 F12에서 조절하게 해주는 플러그인입니다.

현재 기준 **SPT 4.1**.

---

## 원작 4.1.0과 다른 점

| 항목 | 원작 4.1.0 | 이 포크 |
|---|---|---|
| 고개 돌리기 각도(`Free Look Angle`) | F12 설정 추가 (0~100°) | **없음** — 바닐라처럼 좌우 50° 고정 |
| 기본 FOV 선택 범위 | 30~120 고정 (설정 삭제) | F12 `Min/Max Base FOV`로 조절 (기본 30~110) |
| Realism Mod 연동 | 코드는 남아 있고 연동만 주석 처리 | **완전 삭제** (관련 파일·분기 없음) |
| FOV 제한 람다 찾기 | 컴파일러가 붙인 이름(`CG_Ctor.method_0`) | 본문 모양(50/75 상수를 쓰는 `int(int)`)으로 찾음 — 이름이 밀려도 안 깨짐 |
| 패치 하나 실패 시 | `Awake`가 죽고 그 뒤 패치 전부 사라짐 | 그 기능 하나만 끄고 로그에 이름 남김 |
| 빌드 설정 | 원작자 PC 경로(`F:\SP EFT\SPT-41X`) 하드코딩, `RealismMod.dll` 필요 | `SptRoot`(기본 `E:\SPT 4.1`), 플러그인 폴더 자동 복사 + 릴리스 zip |
| 문서 | 없음 | 이 README (한국어) |

나머지 게임플레이 코드(카메라·반동·감도·FOV Scale)는 원작 4.1.0과 같습니다.

---

## 4.1 포팅에서 바뀐 것

4.1은 클라이언트를 **역난독화**해서 배포합니다. **타입**은 SPT 위키에 4.0→4.1 대응표가
공개돼 있지만, **멤버(메서드·필드)는 대응표가 없습니다.**

### 1. 타입 이름 — 위키 대응표대로 교체

| 4.0 | 4.1 | 쓰인 곳 |
|---|---|---|
| `CameraClass` | `EFT.CameraControl.CameraManager` | FovController, FovPatches |
| `GClass1085` | `EFT.Settings.Game.GameSettingsGroup` | FovPatches (`GameSettingsClass` 별칭) |
| `GClass3380` | `EFT.InventoryLogic.ItemExtensions` | FovPatches (`CloneItem`) |
| `SharedGameSettingsClass` | `EFT.Settings.SettingsManager` | FovPatches, SensPatches |
| `TemplateIdToObjectMappingsClass` | `EFT.InventoryLogic.JsonTypes` | Utils |

출처: SPT 4.1 wiki [Client Class Name Mappings](https://hub.sp-tarkov.com/) 4.0→4.1 표.
소스에 등장하는 나머지 식별자 562개를 전부 이 표에 대조했고, 걸린 건 위 5개뿐입니다
(나머지는 원래부터 실명이라 표에 없음 = 그대로).

### 2. 멤버 이름

처음 포팅 때는 4.1 멤버 이름을 몰라서 `method_*` 3개를 본문 모양(지문)으로 찾았습니다.
이후 실기 로그와 4.1 `Assembly-CSharp.dll`, 원작 4.1.0 코드로 실제 이름이 확인돼서
2개는 실명으로 바꿨습니다 (컴파일 시점에 존재가 검증됨):

| 4.0 | 4.1 실명 | 하는 일 |
|---|---|---|
| `ProceduralWeaponAnimation.method_19` | `AddHandRecoilRotateToCamera` | 손 반동 회전을 카메라에 적용 |
| `ProceduralWeaponAnimation.method_23` | `OnAimOrPoseChanged` | 조준/자세가 바뀔 때 무기 파라미터 갱신 = 메인캠 FOV 설정 지점 |
| `ProceduralWeaponAnimation.Boolean_0` | `InLeftStance` | 왼쪽 어깨 자세인지 |
| `ProceduralWeaponAnimation.Single_2` | `HeadBobbing` (이름과 달리 FOV 설정값) | 이 포크는 그 식(`FieldOfView.Value`)을 직접 씀 |

**남은 지문 1개** — `GClass1085.Class1841.method_0`(기본 FOV를 50~75로 clamp하는 람다).
컴파일러가 만든 람다 캐시 클래스라 BSG가 그 파일에 람다 하나만 추가해도 이름이 밀립니다.
그래서 `src/ObfuscatedTargets.cs`가 "설정 그룹의 중첩 타입 중 `int(int)`이면서
`MIN_FIELD_OF_VIEW`, `MAX_FIELD_OF_VIEW` 두 상수를 모두 쓰는 메서드"로 찾습니다. 후보가
0개거나 2개 이상이면 찍지 않고 로그를 남기고 그 기능만 끕니다.

### 3. 패치 하나가 실패해도 나머지는 살아남게

`ModulePatch.Enable()` 은 `GetTargetMethod()` 가 null이면 **예외를 던집니다.** 원본은
`Enable()` 10개를 맨몸으로 연달아 호출해서, 대상 하나를 못 찾으면 `Awake` 가 통째로
죽고 **그 뒤 패치가 전부** 조용히 사라졌습니다. 이제 각각을 감싸서 실패한 기능 하나만
끄고 이름을 로그에 남깁니다.

---


## 빌드 설정도 갈아엎었습니다

- **참조가 전부 `HintPath` 없는 맨 `<Reference>` 였습니다.** `OutDir` 이
  `F:\SP EFT\SPT-406\BepInEx\plugins\` 를 직접 가리키고 MSBuild가 OutDir을 탐색 경로에
  넣는 덕에 **작성자 PC에서만** 해결되던 구조입니다. `SptRoot` 로 대체했고 기본값은
  `E:\SPT 4.1`, `-p:SptRoot=...` 또는 환경변수로 덮어쓸 수 있습니다
- BepInEx를 `nuget.bepinex.dev` 패키지 대신 **설치본의 DLL**에서 참조합니다. 그 피드는
  네트워크에 따라 아예 닿지 않고, 설치본에는 게임이 실제로 로드할 바로 그 어셈블리가
  이미 있습니다. `NuGet.Config` 도 같이 삭제
- **Realism Mod 연동 삭제.** 원본은 `RealismMod.dll`이 있어야 빌드됐는데, 연동 자체를
  지웠으므로 이제 필요 없습니다
- 빌드 후 `BepInEx\plugins\` 로 복사 + 릴리스 zip 생성. zip은 압축 대상 폴더 **밖에**
  씁니다 (안에 쓰면 자기가 쓰는 파일을 읽으려 들어 MSB3931이 납니다)

## 빌드

```
dotnet build FOVFix.csproj -c Release
dotnet build FOVFix.csproj -c Release -p:"SptRoot=D:\내 SPT 경로"
```

---

## 확인한 것 / 확인 못 한 것

| | 상태 |
|---|---|
| 실제 4.1 `Assembly-CSharp.dll`로 컴파일 | **통과** (2026-10-01, 원작 4.1.0 머지 후) |
| 원작이 쓴 4.1 실명 멤버 존재 여부 | **확인** — `OnAimOrPoseChanged`, `AddHandRecoilRotateToCamera`, `InLeftStance`, `WeaponRecoilEffect`, `SetCompensationScale`, `ResetFovAdjustments`, `SetFovParams` |
| `LerpCamera` 대체 코드 vs 4.1 원본 | **대조함** — 차이는 `WeaponRecoilEffect` 카메라 반동 1개였고 반영함 |
| `Player.Look` 대체 코드 vs 4.1 원본 | **대조함** — 4.1 원본은 조준 중 자유시점 해제 시 FOV를 35(광학)/설정값-15로 바꾸는 블록이 추가됐는데, 그게 바로 이 모드가 없애려는 "ADS FOV 감소"라 원작과 동일하게 넣지 않음 |
| 인게임 레이드 거동 | **아직** — 사용자 확인 대기 |

### 손대지 않은 죽은 코드

`ScopeZoomPatch`, `CameraUpdatePatch`, `OpticPanelPatch` 는 `Plugin.Awake` 에서 한 번도
`Enable()` 되지 않는 원작자의 실험용 코드입니다. 원본 그대로 뒀습니다.

### 첫 실행 때 로그에서 확인할 것

```
FOVFix: base FOV clamp -> <중첩 클래스>.<이름>
```

`FOVFix: expected exactly one ... candidate` 나 `FOVFix: ... could not be applied` 가
찍히면 그 기능만 꺼진 상태이니 `BepInEx/LogOutput.log`를 알려주세요.
