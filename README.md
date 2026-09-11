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

# Fontaine's FOV Fix (fork)

> **원작자 · 원본**
> **Fontaine** — https://github.com/space-commits/SPT-FOV-Fix
>
> 이 레포는 위 원작의 **포크**입니다. 기능은 그대로고, **SPT 4.1에서 빌드·동작하도록
> 포팅**한 것이 전부입니다. 포크 시점(`f5ee116`)에서 원본과 코드 차이는 없었습니다.

ADS할 때 FOV가 줄어드는 걸 없애고, 조준 카메라 위치·속도, 기본 FOV 범위(50~75 제한
해제), 배율별 마우스 감도, 토글 줌 등을 F12에서 조절하게 해주는 플러그인입니다.
Realism Mod의 자세(stance) 기능과 연동됩니다.

현재 기준 **SPT 4.1**.

---

## 4.1 포팅에서 바뀐 것

4.1은 클라이언트를 **역난독화**해서 배포합니다. **타입**은 SPT 위키에 4.0→4.1 대응표가
공개돼 있지만, **멤버(메서드·필드)는 대응표가 없습니다** — assembly-tool이 빌드 중에
실명 레퍼런스 어셈블리와 시그니처를 맞춰가며 이름을 지어내고 결과를 남기지 않기
때문입니다. 게다가 그 매칭은 휴리스틱이라 어떤 멤버는 바뀌고 어떤 멤버는 그대로
남습니다.

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

### 2. 이름이 아니라 **모양**으로 찾게 바꾼 것 3개

`method_*` 는 assembly-tool이 갈아엎는 접두사고 새 이름은 공개돼 있지 않습니다. 더
나쁜 건, 하드코딩된 `"method_23"` 은 **바뀌면 못 찾는** 정도가 아니라 나중 빌드가 번호를
재배치하면 **컴파일도 되고 조용히 엉뚱한 메서드를 패치**한다는 점입니다. 그래서
`ObfuscatedTargets.cs` 가 역난독화가 건드릴 수 없는 것 — 본문의 모양 — 으로 찾습니다.

| 4.0 대상 | 하는 일 | 지문 |
|---|---|---|
| `ProceduralWeaponAnimation.method_19(float)` | 프레임마다 카메라 반동 적용 | 타입의 `void(float)` 11개 중 **Quaternion 지역변수를 12개** 쓰는 유일한 메서드 (2등은 1개) |
| `ProceduralWeaponAnimation.method_23(bool)` | 무기 파라미터 갱신 = 메인캠 FOV 설정 지점 | `void(bool)` 중 본문이 **`SetFov` 를 호출하는** 유일한 비-접근자 (다른 두 호출부는 `Sprint` 세터와 인자 2개짜리 `InitTransforms`) |
| `GClass1085.Class1841.method_0(int)` | 기본 FOV를 50~75로 clamp하는 람다 | 설정 그룹의 중첩 타입 중 `int(int)` 이면서 본문이 **`MIN_FIELD_OF_VIEW`, `MAX_FIELD_OF_VIEW` 두 상수를 모두 로드**하는 메서드 (형제 람다는 5~100이라 구분됨) |

세 지문 모두 4.0 어셈블리 메타데이터로 **타입 전체에서 유일함을 확인**했고, SPT 4.1
실기에서도 셋 다 정상적으로 붙는 것을 확인했습니다. 후보가 0개거나 2개 이상이면
**찍지 않고** 로그에 남기고 그 기능만 끕니다.

> **4.1은 이 중 최소 하나를 실제로 리네임했습니다.** 인게임 로그:
> ```
> FOVFix: camera recoil -> ProceduralWeaponAnimation.AddHandRecoilRotateToCamera
> ```
> `method_19` 은 4.1에서 `AddHandRecoilRotateToCamera` 가 됐습니다. 이름이 지문이 노린
> 동작(손 반동 회전을 카메라에 적용)과 정확히 일치합니다. 이건 "이름을 박아뒀으면
> 나중에 위험했다" 수준이 아니라, **하드코딩된 `method_19` 로는 4.1에서 컴파일조차 되지
> 않는** 자리였습니다 (패치 대상이 아니라 직접 호출부라서).

`method_19` 는 매 프레임 호출되므로, 찾은 뒤 열린 델리게이트로 캐시해서 프레임마다
리플렉션이 돌지 않게 했습니다.

> `Class1841` 은 애초에 **컴파일러가 만든 람다 캐시 클래스**입니다. 역난독화와 무관하게
> BSG가 그 파일에 람다 하나만 추가해도 이름이 밀리는 종류라, 이름으로 잡는 게 원래부터
> 잘못된 방식이었습니다.

### 3. 이름 의존을 아예 **없앤** 것 2개

두 멤버는 이름이 자기 타입에서 파생된 형태(`IsTypeDerived`)라 assembly-tool의 리네임
대상 조건에 걸립니다. 둘 다 한 줄짜리 래퍼여서, 그 한 줄을 직접 쓰는 걸로 바꿨습니다:

| 4.0 | 실제 정의 | 대체 |
|---|---|---|
| `ProceduralWeaponAnimation.Single_2` | `Singleton<SharedGameSettingsClass>.Instance.Game.Settings.FieldOfView.Value` | 그 식을 그대로 사용 |
| `ProceduralWeaponAnimation.Boolean_0` | `_leftStanceCurrentCurveValue > 0f` | `_leftStanceCurrentCurveValue` 를 Harmony 필드 주입으로 읽어서 비교 |

`_leftStanceCurrentCurveValue` 는 실명이라 역난독화가 건드리지 않습니다.

### 4. 패치 하나가 실패해도 나머지는 살아남게

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
- **Realism Mod 컴파일 의존성 제거.** 원본은 `RealismCompat.cs` 가 Realism 타입을 직접
  불러서, Realism이 **설치돼 있지 않으면 빌드 자체가 안 됐습니다.** 정작 플러그인은
  Realism 없이도 멀쩡히 돌게 설계돼 있는데(`Chainloader` 로 런타임에 감지) 말이죠.
  이제 `RealismCompat` 이 Realism의 static들을 리플렉션으로 읽습니다 — 빌드는 한 벌이면
  되고, 나중에 Realism을 설치하면 연동이 알아서 켜집니다. Realism 버전이 바뀌어 멤버를
  못 찾으면 "Realism 없음"과 동일하게 처리하고 로그를 남깁니다 (반쯤 읽은 자세 상태로
  동작하지 않게)
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
| 타입 5개 대응 | **확인** — 위키 4.0→4.1 표, 소스의 식별자 562개 전수 대조 |
| 지문 3개가 유일한지 | **확인** — 4.0 어셈블리 메타데이터로 타입 전체 검사 |
| 4.1 형태 어셈블리로 전체 컴파일 | **통과** — 4.0 `Assembly-CSharp` 에 위 5개 리네임을 Cecil로 실제 적용한 DLL을 만들어 빌드 |
| Realism Mod 없이 빌드 | **통과** — `RealismMod.dll` 이 아예 없는 상태에서 클린 빌드 확인 |
| 실제 4.1 클라이언트에서 로드 | **확인** — 지문 3개 전부 해석 성공, 패치 부착됨 |
| 실제 4.1 `Assembly-CSharp.dll` 로 컴파일 | **못 함** — 이 작업 환경에 4.1 클라이언트 어셈블리가 없습니다 |
| 인게임 레이드 거동 검증 | **안 함** — 아래 "남아 있는 진짜 위험" 참고 |

### 남아 있는 진짜 위험 하나

이 모드는 `ProceduralWeaponAnimation.LerpCamera` 와 `Player.Look` 을 **Prefix에서
`return false` 로 통째로 대체**합니다. 즉 두 메서드의 4.0 본문을 손으로 옮겨 적은
코드가 들어 있습니다. SPT 4.1은 더 새로운 EFT 빌드라, BSG가 그 두 메서드를 손댔다면
**바뀐 동작이 조용히 사라집니다** — 에러도 로그도 없이 조준 카메라 거동만 달라집니다.
4.1 어셈블리 없이는 확인할 방법이 없어서 손대지 않았습니다. (원본도 같은 구조입니다.)

4.1 `Assembly-CSharp.dll` 을 주시면 그 두 메서드 본문을 4.0과 대조해서 차이를 반영할 수
있습니다.

### 손대지 않은 죽은 코드

`ScopeZoomPatch`, `CameraUpdatePatch`, `OpticPanelPatch` 는 `Plugin.Awake` 에서 한 번도
`Enable()` 되지 않는 원작자의 실험용 코드입니다. 실행되지 않으므로 그 안의
`ScopeZoomHandler.method_4` 같은 이름은 4.1에서 아무 영향이 없어 원본 그대로 뒀습니다.
혹시 나중에 활성화하려면 그 이름들부터 확인해야 합니다.

### 원본 `dev` 브랜치

원본 `dev` 에 아직 릴리스되지 않은 커밋 2개(ADS 판정 수정, z축 스무딩)가 있습니다.
포팅 범위가 아니라서 가져오지 않았습니다. 필요하면 말씀해 주세요.

### 첫 실행 때 로그에서 확인할 것

지문 3개가 각각 어디에 붙었는지 이름을 찍습니다:

```
FOVFix: camera recoil -> ProceduralWeaponAnimation.<이름>
FOVFix: weapon params update -> ProceduralWeaponAnimation.<이름>
FOVFix: base FOV clamp -> <중첩 클래스>.<이름>
```

`FOVFix: expected exactly one ... candidate` 나 `FOVFix: ... could not be applied` 가
찍히면 그 기능만 꺼진 상태이니 로그를 알려주세요.
