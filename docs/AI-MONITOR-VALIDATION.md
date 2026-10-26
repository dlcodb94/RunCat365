# AI Monitor MVP validation

검증일: 2026-10-26. Windows 11 x64 (10.0.26200), .NET SDK 9.0.320.

## Repository and scope

- Base: `runcat-dev/RunCat365`, commit `a6aa5b1`.
- Fork: https://github.com/dlcodb94/RunCat365 (`origin`). Original: `upstream`.
- Local branch: `feat/codex-usage-dashboard`.
- 검증 완료 후 사용자가 Fork의 `origin/main`에 커밋/Push하도록 승인했습니다. 원본 `upstream`은 변경 대상이 아닙니다.
- 기존 CPU/RAM 수집기, 애니메이션, NotifyIcon, 우클릭 메뉴를 재사용했습니다. AI 파서·수집기·팝업과 테스트만 추가하고 메뉴/타이머/리소스를 연결했습니다.
- ORCA 및 다른 프로젝트를 수정하지 않았습니다. 로컬 세션은 읽기만 수행했으며 대화/인증 정보를 저장소에 포함하지 않았습니다.

## Build and executable

이 PC의 기본 `dotnet`에는 SDK가 없어 공식 `dotnet-install.ps1`로 `%TEMP%\runcat-dotnet`에 SDK를 준비했습니다. PATH는 변경하지 않았습니다. 이번 환경에서 다시 빌드하려면 다음처럼 실행합니다.

```powershell
$runcatDotnet = Join-Path $env:TEMP 'runcat-dotnet/dotnet.exe'
& $runcatDotnet build RunCat365/RunCat365.csproj -c Release -p:Platform=x64
& $runcatDotnet run --project RunCat365.Tests -c Release -p:Platform=x64
& $runcatDotnet publish RunCat365/RunCat365.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true -o artifacts/win-x64
```

- Release 앱/테스트 빌드 및 self-contained x64 publish 성공.
- 기존 `App.manifest` DPI 설정에 대한 `WFO0003` 경고가 있습니다. MVP에서 원본 DPI 구성을 변경하지 않았습니다.
- 최초 빌드/테스트를 동시에 실행했을 때 출력 DLL 잠금 충돌이 있었습니다. 이후 순차 실행으로 정상 완료했습니다.
- `artifacts/win-x64/RunCat 365.exe`를 이 Windows 11 PC에서 실제 실행했고, 수분 동안 프로세스 생존/응답을 확인했습니다. 실행본은 .NET을 포함하므로 시스템에 .NET 9 런타임을 설치하지 않아도 됩니다.

## Automated checks

외부 테스트 프레임워크 없이 `RunCat365.Tests` 실행 파일이 실패 시 예외/비정상 종료 코드를 반환합니다. **62개 검사 통과**.

- 로컬에서 관측한 주간 `primary` 스키마, 주간 `secondary` 스키마.
- 5시간 데이터 제외, 다른 `limit_id` 제외, null ID 호환.
- 깨진 JSON, 잘못된 루트/필드 타입, 범위 밖 비율, 잘못된 날짜, 잘못된 이벤트 타입.
- 누락/범위 밖 초기화 시각, 15분 신선도, 초기화 시각 이후에도 값 추정 금지.
- 세션 폴더 없음/빈 폴더, 재귀 하위 폴더, 손상된 줄 이후 정상 복구.
- 폴링해도 사용량 timestamp 불변, 미완성 줄 대기/완성 후 반영.
- 파일 내/여러 파일 간 이벤트 시각 기준 선택, mtime에 의존하지 않는 선택.
- 잠긴 변경 파일에서 이전 값 보존 + ReadError, 파일 축소/삭제 반영, 256KiB 경계 처리.
- Windows RAM 실제 측정, Windows CPU 카운터 스모크 검사(카운터 미지원 시 기존 fallback 허용).
- 영어 리소스 및 데이터 없는 대시보드 렌더링.
- 제목줄 없는 팝업 닫기 후 숨김/재열기, 100% 사용률 막대 렌더링.
- 최근 60초/최대 61개 표본 제한, 중복 timestamp 방지, 절전 후 오래된 표본 제거, 비정상 값 N/A 처리.
- 동일 값 갱신 시 창 전체 무효화 없음, 다크/라이트 전환 후 제목 글자 픽셀 렌더링.
- 시스템/수동 테마 9개 조합에서 고양이의 모든 애니메이션 프레임이 검은색이며 비어 있지 않음을 픽셀 검사.
- System/Light/Dark 메뉴 이름 구분 유지.

## Local data and rendering

- 약 365개, 총 1.46GB의 로컬 세션에서 주간 이벤트를 확인했습니다. 당시 `primary.window_minutes = 10080`, `secondary = null`이었습니다.
- 실제 로컬 수집 결과 `Ready`, 주간 사용량 3%를 읽었습니다. 첫 실행 측정 1,366ms, 캐시 재검사 5ms. 이후 따뜻한 파일 캐시에서 226–492ms / 4ms를 관측했습니다. 파일 캐시·디스크·로그 수에 따라 달라집니다.
- 전체 실행 프로세스의 짧은 5초 표본: working set 100.5MiB, CPU 0.93% (14 logical processors 기준). 장기 벤치마크나 기능 추가 전후 비교가 아닙니다.
- 다크/라이트, 데이터 없음, 오래된 기록, 150% 수동 control scaling을 렌더링하고 PNG를 육안 확인했습니다. 캡처는 Git 제외 경로 `TestResults/dashboard-*.png`에 있습니다.
- 처음의 숨겨진 폼 캡처가 비어 있어 실제 폼 표시와 메시지 펌프 후 캡처하도록 테스트를 수정했습니다.

선택적으로 로컬 로그와 화면을 다시 검사할 수 있습니다. `--local`은 현재 PC의 실제 사용량 숫자를 콘솔에 출력합니다. 기본 테스트는 임시 합성 로그만 사용합니다.

```powershell
New-Item -ItemType Directory TestResults -Force
dotnet run --project RunCat365.Tests -c Release -p:Platform=x64 -- C:/dev/RunCat365/TestResults --local
```

## Not verified / limits

UI 후속 변경: RunCat Neo의 `docs/images/en/system_metrics.jpg`, `custom_metrics.jpg` 및 `DashboardView`, `SystemInfoStackView`, `SystemInfoView`, `CustomMetricsCardView`, `LineGraphView`, `MaterialCellStyle`을 확인했습니다. 작은 헤더, 단일 시스템 패널의 세로 행, 왼쪽 아이콘, 상세 수치, 주황색 면적 그래프/한도 막대, 별도 Codex 패널로 재구성했습니다. 시스템 수치는 기존 측정기를 그대로 쓰고 최근 60초 표본만 저장합니다. 새 UI 패키지/애니메이션 타이머는 없습니다. macOS의 실시간 배경 흐림은 구현하지 않았습니다. OS 고대비 색상 대응은 구현했지만 실제 고대비 설정 전환은 검증하지 않았습니다. API 서비스는 아직 사용 전이므로 OpenAI/Claude API 연동은 추가하지 않았습니다.

`TestResults` 화면 검사의 CPU/Codex 값과 그래프는 재현 가능한 합성 테스트 데이터이며 실제 사용량 화면으로 제시하지 않습니다. 실행 앱에는 실제 측정 표본만 들어갑니다. 그래프는 측정 간격이 2.5초를 넘거나 값이 없으면 연결하지 않습니다.

색상 후속 조정: 사용자가 제공한 Neo 스크린샷에서 배경 `#263949`, 패널 `#253747`, 기본 글자 `#EAEEF2`, 그래프 `#F5811A`를 추출해 다크 테마에 적용했습니다. Semibold 글꼴, 낮은 대비의 패널 테두리/그림자, 외곽 이중 테두리, 점과 선으로 된 메뉴 아이콘을 적용했습니다. 정보 배치와 수집 동작은 유지했습니다. 39개 기존 검사 통과 및 다크/라이트/150% 렌더링을 확인했습니다. Windows의 외부 창 그림자는 OS 설정에 따라 달라지며 PNG 렌더링 검사에는 포함되지 않습니다.

여백/영어 후속 조정: 위쪽 여백 20px, 본문 Segoe UI 8.5pt regular, 제목 10pt semibold로 조정하고 화면/메뉴를 영어로 고정했습니다. 파일 확인 footer와 정상 상태의 마지막 관측 안내 문구를 제거했습니다. 오류/데이터 없음/오래된 데이터 상태만 계속 표시합니다. 공식 OpenAI Codex 확장의 PNG를 embedded resource로 포함하고, 별도 `ASSET-SOURCES.md`에 출처를 기록했습니다. 영문 다크/라이트/150% 화면을 재검사했으며 39개 검사 통과. 새 아이콘은 실행 중 네트워크 요청 없이 로컬 리소스에서 읽습니다.

- 실제 트레이 좌클릭·우클릭·Esc·바깥 클릭 전체를 UI 자동화로 검증하지는 않았습니다. 이벤트 연결 코드, 폼 렌더링, 실행 프로세스를 확인했습니다.
- 150% control scaling은 실제 여러 모니터 사이의 DPI 이동 테스트가 아닙니다. 다중 모니터, Windows 테마 전환, 장시간 실행, 절전 복귀는 미검증입니다.
- CLI/데스크톱 각각의 표시 수치와 대조하지 않았습니다. 동일 경로/스키마를 기록하는 클라이언트에 한해 지원하며 앱별 기록 생성 여부는 보장하지 않습니다.
- 새 데이터가 첫 시작 시 파일 끝 256KiB 안에 없으면 N/A일 수 있습니다. 여러 계정 로그에서 현재 계정을 판별하지 않습니다.
- Store 패키징/서명, x86/ARM64 실행, WSL, 5시간 한도, 일반 ChatGPT, 원격 조회는 범위 밖입니다.

## Current UI and black cat follow-up

- This section supersedes the earlier Neo palette and layout notes. The current dashboard follows the rounded cards, cream/dark teal palette, lavender figures, and segmented progress bar of [Zajno's BRIK reference](https://dribbble.com/shots/26817947-Mobile-App-Design-for-a-Brain-Health-Training-Platform). Reference images were inspected from the linked project's CDN and are not distributed with the app.
- Codex is the top card; CPU and RAM are side by side below. The user's unchanged transparent `chatgpt-white.png` remains embedded locally. Both themes use dark card surfaces.
- Theme colors update only when the theme/high-contrast state changes. Metric refreshes do not invalidate the entire form. Buffered layout panels replace the earlier whole-window `WS_EX_COMPOSITED` approach; card interiors use solid backgrounds and labels use compatible text rendering. Tests explicitly show, activate, and refresh the popup before capture because it normally hides on deactivation.
- All 52 checks passed. Dark/light, empty, stale, and 150% control scaling images were inspected. These checks do not prove the absence of visible flicker on every display.
- Reusing a manually scaled form for further `DrawToBitmap` captures intermittently omitted text/backgrounds. Empty-state capture now runs before synthetic scaling; that normal-size capture and the initial 150% image were visually checked. Repeated scaled captures and real per-monitor DPI behavior remain unresolved verification limits, not a claimed pass.
- The built-in cat is recolored black in the tray animation, runner menu, and game. Other runner and general UI contrast colors retain upstream behavior. The theme menu labels were restored to System/Light/Dark.
- Removed `DashboardPreview.cs`, `preview.ps1`, the `--preview` entry point, and their tests at the user's request.
- Frame-by-frame flicker, actual high-contrast switching, monitor/DPI transitions, and long-running behavior remain unverified. Game/thumbnail black recoloring was checked in code; automated pixel checks cover tray animation frames.

## Subtle floating shadow follow-up

- Replaced the old `CS_DROPSHADOW` hint with a small, click-through, non-activating layered window. The center is transparent so it cannot dim the dashboard. Existing corner geometry and content size are unchanged.
- Shadow uses 13 low-alpha layers, a 12-DIP falloff, and a 3-DIP downward offset. Its bitmap is rebuilt only for size/DPI changes; metric refreshes do not repaint it. Hide/dispose follow the dashboard. High contrast suppresses it; native upload failure leaves the dashboard usable without the effect.
- 62 checks pass, including 96/144-DPI alpha falloff and transparency, real layered-window creation, visible popup movement in both themes, hide, and disposal. An earlier movement check ran after the popup had deactivated/hidden; movement is now checked while visible. No shadow movement is needed while the popup is hidden.
- `TestResults/dashboard-shadow-*.png` composites the same shadow bitmap and test dashboard on a neutral background for visual inspection. These are synthetic render checks, not desktop screenshots. Actual desktop composition, mouse pass-through, high-contrast switching, and cross-monitor DPI transitions still need manual verification.
- Windows API reference: [UpdateLayeredWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-updatelayeredwindow).

## Changed files

- `RunCat365/CodexUsageInfo.cs`: JSON 이벤트 검증, 주간 사용량/시간/신선도 모델.
- `RunCat365/CodexUsageRepository.cs`: 로컬 파일 탐색, 제한된 tail 읽기, 변경 캐시, 오류 상태.
- `RunCat365/DashboardForm.cs`: CPU/RAM/주간 Codex 카드, 테마, 팝업 위치 및 닫기 동작.
- `RunCat365/DashboardShadow.cs`: translucent exterior shadow, native layer upload, and bitmap generation.
- `RunCat365/SystemUsageHistory.cs`: 최근 60초 시스템 표본, 시간/개수 제한과 비정상 값 처리.
- `RunCat365/EndlessGameForm.cs`: built-in game cat fixed to black.
- `RunCat365/Program.cs`, `ContextMenuManager.cs`: 기존 애니메이션 유지, 15초 백그라운드 수집, 팝업 열기/정리.
- `RunCat365/Properties/Strings*.resx`: 8개 언어 리소스 파일에 새 문구(한국어/영어) 등록.
- `RunCat365/RunCat365.csproj`: 테스트 어셈블리에 내부 API 접근 허용.
- `RunCat365.Tests/RunCat365.Tests.csproj`, `Program.cs`: 실행형 회귀 테스트 및 선택적 실제 로그 검사.
- `README.md`, 이 문서, `.gitignore`: 실행/수집/한계/출처/검증 문서, 로컬 배포 산출물 제외.
