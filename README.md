# LaptopBar / win64_taskbar

Windows 노트북용 가벼운 C# WPF 상단바입니다. CPU/RAM/GPU, 배터리, 연결 상태와 로컬 AI 채팅을 한곳에서 사용합니다.

## 자동 설치 · 업데이트

**Windows x64, 관리자 권한 불필요.** PowerShell에서 다음 명령을 실행하세요. 이 저장소의 설치 스크립트를 내려받아 실행합니다.

```powershell
irm https://raw.githubusercontent.com/kimAssembly/win64_taskbar/main/install.ps1 | iex
```

설치 스크립트는 다음을 자동으로 처리합니다.

- 최신 GitHub Release ZIP 다운로드 및 SHA256 검증
- `%LOCALAPPDATA%\LaptopBar`에 앱과 .NET 런타임 설치
- 시작 메뉴 바로가기 등록: Windows 검색에서 **LaptopBar**
- 현재 사용자 Windows 로그인 시 자동 실행 등록
- 앱 실행. LLM 모델은 다운로드하거나 자동으로 로드하지 않습니다.

같은 명령을 다시 실행하면 최신 버전으로 업데이트하며 `settings.json`을 보존합니다. 업데이트할 설치 폴더에서 실행 중인 앱은 종료됩니다. 기존 대화는 메모리에만 있으므로 업데이트 전에 필요한 내용을 보관하세요.

자동 시작을 원치 않으면 앱 설정에서 OFF로 바꾸세요. 설치 옵션을 지정할 수도 있습니다.

```powershell
$installer = [scriptblock]::Create((irm https://raw.githubusercontent.com/kimAssembly/win64_taskbar/main/install.ps1))
& $installer -NoStartup
```

`-NoStartup`은 시작 프로그램 등록을 생략하며, 기존 등록을 삭제하지 않습니다. `-NoStart`는 설치 후 실행을 생략합니다. 설치 경로를 바꾸려면 `-InstallDir "D:\Apps\LaptopBar"`를 지정하세요. 비어 있지 않은 무관한 폴더에는 설치하지 않습니다.

수동 설치는 [Releases](https://github.com/kimAssembly/win64_taskbar/releases/latest)의 `LaptopBar-win-x64.zip`을 내려받아 압축을 풀고 `LaptopBar.exe`를 실행하세요. 수동 압축 해제는 시작 메뉴를 등록하지 않습니다.

## PC에 따른 자동 감지

GPU는 Windows 성능 카운터로 감지하며, 읽을 수 없으면 표시와 여백을 숨깁니다. GPU가 여러 개면 모든 어댑터에서 가장 바쁜 엔진의 사용률을 표시합니다. 프로세스별 사용률은 같은 엔진끼리만 합산합니다. 0%는 유효한 값이므로 계속 표시합니다.

Wi-Fi 어댑터 연결, Bluetooth 라디오/캐시된 연결 기기, 기본 스피커·마이크와 배터리를 읽습니다. BLE 전용 기기는 표시에서 누락될 수 있습니다. Wi-Fi 표시는 인터넷 연결 보장이 아닌 무선 어댑터 상태입니다. 오디오 슬라이더와 음소거 버튼은 실제 시스템 설정을 변경하며 마이크를 녹음하지 않습니다.

## 사용법

- 배경 드래그: 상단바 이동
- 연결 아이콘: Wi-Fi/Bluetooth/볼륨 패널
- 점 세 개: 표시·자동 시작·자동 숨기기·GPT 앱 설정
- 보라색 L 트레이 아이콘 더블클릭: 상단바 표시/숨기기
- AI 체크박스: Qwen / Oss / GPT 버튼 개별 표시
- 자동 숨기기: 기본 OFF, 기본 8초(3~60초). 팝업 사용 중에는 숨기지 않습니다.
- 자동으로 숨겨지면 주 화면 상단의 얇은 표시 영역에 마우스를 올려 펼칩니다.
- 회색 연결 점은 연결 미확인, 보라색은 서버/모델 확인 상태입니다. 버튼 배경은 열린 창을 표시합니다.

Windows 기본 작업표시줄을 유지합니다. 화면 공간을 예약하지 않으므로 최대화된 창 위에 겹칠 수 있습니다. 주 모니터 기준이며 해상도·화면 배율 변경에 대응합니다.

## 로컬 AI

LM Studio/Ollama/llama.cpp에서 모델과 API 서버를 먼저 실행하세요. PC마다 모델 설치와 실행기 선택은 별도로 필요합니다.

| 실행기 | OpenAI 호환 주소 |
|---|---|
| LM Studio | http://127.0.0.1:1234/v1 |
| llama.cpp | http://127.0.0.1:8080/v1 |
| Ollama | http://127.0.0.1:11434/v1 |

Qwen/Oss 버튼은 서버 목록에서 이름에 `qwen`/`oss`가 포함된 모델을 선택합니다. 실제 모델 로드·언로드는 실행기에서 관리합니다. 프로필을 전환하면 이전 채팅 창은 닫힙니다. 채팅은 최근 6회만 메모리에 유지하며 디스크에 저장하지 않습니다. 입력이 길면 실행기의 컨텍스트 한도를 넘을 수 있습니다.

GPT 버튼은 설치된 ChatGPT 앱을 자동 감지해 실행합니다. 설정에서 지정한 EXE가 있으면 우선 사용하며 앱이 없으면 웹을 엽니다. ChatGPT 답변을 이 앱으로 가져오는 연동은 아닙니다. 로컬 API는 HTTP loopback만 허용하며 프록시/리다이렉트를 사용하지 않습니다.

모델 다운로드, 자동 타일링, 파일 편집 및 에이전트 권한 승인은 포함하지 않습니다.

## 개발 · 테스트

.NET 9 SDK 및 Windows가 필요합니다.

```powershell
git clone https://github.com/kimAssembly/win64_taskbar.git
cd win64_taskbar
dotnet build LaptopBar.csproj -c Release
.\bin\Release\net9.0-windows\LaptopBar.exe --smoke-test
.\tests\installer-tests.ps1
```

테스트할 때는 실행 중인 다른 LaptopBar를 종료하세요. `verification`에 WPF 렌더링 이미지와 PASS/FAIL 보고서가 생성됩니다. 자동 숨기기/마우스 복귀, 버튼 강조, 팝업 닫힘, GPU 유무, AI 표시 조합 16가지에서 오른쪽 잘림 여부를 검사합니다.

GitHub Actions는 push/PR마다 Windows 빌드·설치기·UI 검사를 실행합니다. `v*` 태그를 push하면 .NET 포함 Windows x64 ZIP과 체크섬, 설치 스크립트를 릴리스로 게시합니다.

Qwen/OpenAI 식별 로고의 출처와 라이선스는 [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)를 참고하세요.
