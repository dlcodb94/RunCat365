# RunCat Windows AI Monitor

Windows 11용 개인 PC 모니터입니다. RunCat 365의 달리는 고양이와 기존 메뉴를 유지하면서 CPU, RAM, **Codex 7일 사용률**을 한 팝업에서 표시합니다. 이 MVP에서는 5시간 사용량, 일반 ChatGPT 채팅, WSL, 원격 API를 수집하지 않습니다.

## 실행 및 빌드

- Windows 11, .NET 9 SDK가 필요합니다. 기존 C# / WinForms 스택을 유지합니다.
- Store 패키징 솔루션 대신 앱 프로젝트를 직접 빌드하면 Visual Studio의 Store 패키징 도구가 필요 없습니다.

```powershell
dotnet build RunCat365/RunCat365.csproj -c Release -p:Platform=x64
dotnet run --project RunCat365/RunCat365.csproj -c Release -p:Platform=x64
# 외부 테스트 패키지가 없는 실행형 검사. 실패하면 0이 아닌 종료 코드를 반환합니다.
dotnet run --project RunCat365.Tests -c Release -p:Platform=x64
# 런타임을 포함하는 개인 PC용 배포본
dotnet publish RunCat365/RunCat365.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true -o artifacts/win-x64
```

배포본은 `artifacts/win-x64/RunCat 365.exe`를 실행합니다. 고양이가 숨겨져 있으면 Windows의 트레이 숨김 아이콘 메뉴에서 찾으세요. 기존 RunCat과 같은 단일 실행 잠금을 사용하므로 기존 앱을 먼저 종료해야 합니다.

- **고양이 좌클릭** 또는 **우클릭 → AI 대시보드**: CPU / RAM / Codex 7일 카드.
- 팝업 밖 클릭 또는 Esc: 팝업만 숨깁니다. 상단 메뉴 또는 우클릭의 기존 종료 메뉴로 앱을 종료합니다.
- 기존 캐릭터, CPU에 따른 애니메이션 속도, 시작 프로그램, 테마, 게임 메뉴를 유지합니다.
- 대시보드는 기존 테마 설정(시스템/밝게/어둡게)을 따릅니다. 이 개인용 빌드의 화면과 메뉴는 영어로 고정했습니다. 기존 번역 리소스는 유지합니다.
- [Zajno의 BRIK 디자인](https://dribbble.com/shots/26817947-Mobile-App-Design-for-a-Brain-Health-Training-Platform)을 참고한 둥근 카드 UI입니다. 크림색/어두운 바탕, 짙은 청록색 카드, 라벤더색 수치와 분절형 사용량 막대를 사용합니다. Codex 7일 사용량 아래에 CPU·RAM 카드를 나란히 배치합니다. 상단 점 세 개는 기존 RunCat 메뉴를 엽니다. 참고 이미지나 원본 디자인 자산은 앱에 포함하지 않습니다.
- CPU/RAM 그래프는 앱 실행 중 측정한 최근 60초만 표시합니다. 빈 구간이나 절전 후 공백은 보간하지 않으며 시작 전의 기록은 생성하지 않습니다. 메모리에 최대 61개 표본만 보관하고 디스크에는 저장하지 않습니다.
- 기본 고양이는 테마와 관계없이 검은색입니다. 트레이 애니메이션, 메뉴 썸네일, 기본 게임의 고양이에 적용합니다. 대시보드 테마는 고양이 색에 영향을 주지 않습니다.

## UI를 직접 수정하기

대시보드는 C# WinForms 코드로 만들어져 있으며 HTML/CSS나 디자이너 파일은 사용하지 않습니다. 대부분의 모양은 [`RunCat365/DashboardForm.cs`](RunCat365/DashboardForm.cs)에서 바꿀 수 있습니다.

| 바꾸려는 부분 | 수정 위치 |
| --- | --- |
| 글꼴·크기·두께 | 파일 상단 `bodyFont`, `headingFont`, `valueFont`, `brandFont` |
| 창 크기 | 생성자의 `ClientSize` |
| 전체 여백·헤더 위 공간 | 생성자의 `root.Padding` — `Padding(left, top, right, bottom)` |
| 헤더·Codex·시스템 영역 높이 | 생성자의 `root.RowStyles`에 들어가는 높이 배열 |
| 아이콘과 텍스트 사이 간격 | `MetricRow()`, `aiHeader`, `CreateIcon()` |
| 상세 수치·그래프 간격 | `MetricRow()`의 `RowStyles` |
| 배경·테두리·글자·그래프 색 | `ApplyTheme()`의 `Color.FromArgb(...)` |
| 둥근 모서리·카드 배경 | `Rounded()`, `MetricCard.OnPaintBackground()` |
| 영문 문구 | `RunCat365/Properties/Strings.resx`의 `Dashboard_*` 항목 |
| 앱 언어 | `Program.Main()`의 `defaultCultureInfo` |

예를 들어 `root`의 `new Padding(12, 20, 12, 12)`에서 두 번째 숫자를 늘리면 제목 위 공간이 넓어집니다. 창 높이는 각 영역 높이의 합과 위아래 여백을 포함해야 합니다. 글꼴/행 높이를 함께 확인해 텍스트가 잘리지 않게 조절하세요.

수정 후 기존 앱을 메뉴의 **Exit**로 종료하고 위의 `dotnet publish` 명령으로 다시 빌드한 뒤 실행합니다. 이 PC에서 기본 `dotnet`에 SDK가 없다면 [검증 기록의 임시 SDK 실행 방법](docs/AI-MONITOR-VALIDATION.md#build-and-executable)을 사용하세요.

Codex 패널은 사용자가 제공한 투명한 흰색 `chatgpt-white.png`를 사용합니다. [아이콘 출처와 권리 고지](RunCat365/resources/ASSET-SOURCES.md)를 참고하세요.

### 고양이 색상 수정 위치

- `RunCat365/ContextMenuManager.cs`의 `SetIcons()`: `Runner.Cat`이면 `Color.Black`을 `ReplaceIconList()`에 전달합니다. Light의 원본 프레임 사용 분기도 건너뛰므로 항상 검은색으로 재색칠합니다.
- 같은 파일의 `GetRunnerThumbnailBitmap()`은 고양이 메뉴 썸네일을, `EndlessGameForm.cs`의 `catIcons` 생성부는 게임 고양이를 검은색으로 고정합니다.
- `RunCat365/BitmapExtension.cs`의 `Recolor()`: 투명도를 유지하면서 픽셀 색을 바꾸는 공통 처리입니다.
- `Theme.cs`는 일반 테마 대비색/메뉴 이름을 담당합니다. 기본 고양이의 색을 바꾸는 위치가 아닙니다.

별도 미리보기 창과 `preview.ps1`은 제거했습니다. 수정한 UI는 빌드 후 트레이 팝업에서 확인합니다.

## Codex 사용량 수집 및 개인정보

`CODEX_HOME`이 설정되어 있으면 그 아래 `sessions`, 아니면 `%USERPROFILE%\.codex\sessions`의 하위 JSONL 파일을 읽습니다. 인증 파일을 읽거나 API 키를 요구하지 않으며, 앱은 사용량·인증 정보·대화 내용을 서버로 전송하지 않습니다. 원본 로그나 대화를 저장소에 복사하지 않습니다.

실제 Windows 로컬 샘플에서 확인한 다음 필드만 사용합니다. 아래 값은 형식 설명용입니다.

```json
{"timestamp":"2026-10-26T02:31:54.137Z","type":"event_msg","payload":{"type":"token_count","rate_limits":{"limit_id":"codex","primary":{"used_percent":2.0,"window_minutes":10080,"resets_at":1793583218},"secondary":null}}}
```

- `primary`/`secondary` 중 `window_minutes == 10080`인 주간 한도만 사용합니다. `primary`가 반드시 5시간이라는 가정은 하지 않습니다.
- `limit_id`는 `codex` 또는 필드 없음/null만 허용합니다. 다른 모델별 한도를 주간 Codex 한도로 섞지 않습니다.
- `used_percent`는 기록된 0–100 사이 숫자만 사용합니다. 토큰 수로 한도 사용률을 추정하지 않습니다.
- `resets_at`은 Unix 초를 로컬 시각으로 표시합니다. 없거나 잘못된 값이면 `N/A`입니다.
- 사용량 갱신 시각은 이벤트의 `timestamp`입니다. 폴링 자체로 사용량 갱신 시각을 바꾸지 않습니다. 파일 확인 시각과 평상시의 마지막 관측 안내 문구는 화면에 표시하지 않습니다.
- 최근 관측이 15분보다 오래되면 **오래된 기록**, 초기화 예정 시각이 지나면 **초기화 시각 경과**를 표시합니다. 기존 값을 0%로 임의 초기화하지 않습니다. 15분은 UI의 신선도 기준이며 서비스의 한도 규칙이 아닙니다.
- 백그라운드에서 시작 시와 15초마다 검사합니다. 변경된 파일의 마지막 256KiB만 읽고 변경 없는 파일은 캐시합니다. UI 스레드에서 세션 파일을 읽지 않습니다. CPU/RAM은 기존 수집 흐름에서 1초마다 갱신합니다.
- 깨진 JSON, 지원하지 않는 이벤트, 기록 중인 마지막 미완성 줄은 무시합니다. 잠긴 파일·접근 오류는 읽기 실패로 표시하고 마지막 관측값을 유지합니다. 폴더나 데이터가 없으면 `N/A`입니다.

## CLI / 데스크톱 지원 범위와 한계

Windows Codex CLI와 데스크톱 기능이 **위 경로에 동일한 `event_msg/token_count/rate_limits` 이벤트를 기록하는 경우** 모두 읽습니다. 실행 프로그램 이름으로 필터링하지 않습니다. 데스크톱의 모든 버전이 이 경로나 이벤트를 생성한다고 보장하지 않으며, 별도 저장소·클라우드 전용 세션은 읽지 않습니다. 수집기의 실제 로컬 로그 확인은 데스크톱 UI와 한도가 일치한다는 검증을 대신하지 않습니다.

- 첫 실행 때 한도 이벤트가 파일 마지막 256KiB보다 앞에만 있으면 찾지 못할 수 있습니다. 새 한도 이벤트가 기록되면 자동 반영됩니다. 이미 관측한 값은 같은 프로세스에서 뒤에 긴 대화가 붙어도 유지됩니다.
- 여러 계정을 번갈아 쓰는 로그는 구분할 수 없습니다. 유효한 주간 이벤트 중 시각이 가장 최신인 값을 표시하므로 현재 로그인 계정과 다를 수 있습니다. 계정 분리가 필요하면 별도 `CODEX_HOME`을 사용하세요.
- 로컬 세션 기록 형식은 버전별로 바뀔 수 있습니다. 지원하지 않는 형식은 값을 추정하지 않습니다.
- WSL, 일반 ChatGPT, 5시간 카드, 웹 계정 조회, 수동 새로고침, 장기 이력 저장은 구현하지 않았습니다.
- 작업 표시줄 안에 별도 복잡한 UI를 삽입하지 않고 기존 시스템 트레이와 WinForms 팝업을 사용합니다.

## 설계 근거 및 출처

기반 커밋은 RunCat365 `a6aa5b1`입니다. 기존 `CPURepository`, `MemoryRepository`의 Windows 측정, `Program`의 수집/애니메이션 타이머, `ContextMenuManager`의 NotifyIcon을 재사용했습니다. 새 코드는 `CodexUsageInfo`(검증/해석), `CodexUsageRepository`(파일 수집/캐시), `DashboardForm`(표시)으로 분리했습니다.

- [RunCat 365](https://github.com/runcat-dev/RunCat365): Windows 기반과 고양이 리소스. 기존 Apache-2.0 [LICENSE](LICENSE) 및 저작권 고지를 유지합니다.
- [RunCat Neo](https://github.com/runcat-dev/RunCatNeo): 통합 대시보드와 `docs/samples/codex/runcat-hook.py`의 실제 기간별 한도 처리 설계를 참고했습니다. macOS SwiftUI/AppKit UI나 Codex hook 설정은 이식하지 않았습니다.
- [TrafficMonitor AI Usage Plugin](https://github.com/bemaru/trafficmonitor-ai-usage-plugin): 로컬 JSONL 탐색과 읽기 실패 처리 방식을 검토했습니다. C++ 플러그인 코드를 복사하지 않고 .NET `System.Text.Json`으로 작성했습니다. 참조 프로젝트의 라이선스는 별개이며 해당 코드를 포함하지 않습니다.

테스트와 검증 기록은 [docs/AI-MONITOR-VALIDATION.md](docs/AI-MONITOR-VALIDATION.md)에 정리합니다.

---

# Original RunCat 365

**A cute running cat animation on your Windows Taskbar.**

> [!CAUTION]
>
> - This project is for Windows, so we do not accept inquiries about macOS version.
> - We do not accept issues or pull requests in languages other than English.
> - Issues that do not follow the Issue Template will be closed without question.

[![Issues](https://img.shields.io/github/issues/runcat-dev/RunCat365)](https://github.com/runcat-dev/RunCat365/issues)
[![Forks](https://img.shields.io/github/forks/runcat-dev/RunCat365)](https://github.com/runcat-dev/RunCat365/network/members)
[![Stars](https://img.shields.io/github/stars/runcat-dev/RunCat365)](https://github.com/runcat-dev/RunCat365/stargazers)
[![Top Language](https://img.shields.io/github/languages/top/runcat-dev/RunCat365)](https://github.com/runcat-dev/RunCat365/)
[![Releases](https://img.shields.io/github/v/release/runcat-dev/RunCat365)](https://github.com/runcat-dev/RunCat365/releases)
[![License](https://img.shields.io/github/license/runcat-dev/RunCat365)](https://github.com/runcat-dev/RunCat365/)

`C#` `Win32` `.NET 9.0` `Visual Studio` `RunCat`

## Demo

<img src="./docs/images/demo.gif" width="600" height="200" alt="demo" />
<br/>
<img src="./docs/images/overview.png" width="600" height="600" alt="overview" />
<br/>
<img src="./docs/images/custom_runner.png" width="600" height="467" alt="overview" />
<br/>
<img src="./docs/images/endless_game.png" width="600" height="375" alt="endless game" />

## Installation

RunCat 365 is available for installation on the Microsoft Store.

- Requirement: Windows 10 version 19041.0 or higher
- Microsoft Store: https://apps.microsoft.com/detail/9nw5lpnvwfwj
- Language:
  - Chinese (simplified)
  - Chinese (traditional)
  - English (default)
  - French
  - German
  - Japanese
  - Korean
  - Spanish

## RunCat Developers' Community

This is a space for RunCat contributors to communicate closely regarding development and operations.
We welcome anyone interested in contributing to RunCat.
However, please note that this is a place for discussing features, not for submitting requests.
For requests, please create an Issue according to the template.

Portal: https://runcat-dev.github.io

## Contributors

<a href="https://github.com/runcat-dev/RunCat365/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=runcat-dev/RunCat365" />
</a>
