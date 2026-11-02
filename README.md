# KeyPresser

정해 둔 키를 일정한 간격으로 대신 눌러 주는 작은 Windows 프로그램입니다. 설치 없이 `KeyPresser.exe`만 실행하면 됩니다.

## 기능

- **키와 주기**: F1~F12, Enter, Space, Tab, Esc, 방향키, PageUp/Down, Home/End, A~Z, 0~9 중 하나를 1~86400초 간격으로 누릅니다.
- **대상 창**: 지금 앞에 있는 창, 또는 목록에서 고른 프로그램 창에 보냅니다. 목록은 `프로그램이름.exe — 창 제목` 형식입니다.
- **누른 뒤 원래 창으로 돌아가기**: 대상 창을 앞으로 불러와 키를 누르고, 쓰던 창으로 되돌립니다.
- **백그라운드로 보내기**: 창을 앞으로 불러오지 않고 키 메시지만 보냅니다. 아래 제한 사항을 참고해 주세요.
- **자동 정지**: 멈출 시각과 반복 횟수를 정할 수 있습니다. 둘 다 켜면 먼저 오는 쪽에서 멈춥니다.
- **전역 단축키**: `Ctrl+Alt+P`로 어느 창에서든 시작하거나 멈춥니다.
- **설정 저장**: 마지막 설정을 exe 옆 `KeyPresser.ini`에 저장해 다음 실행 때 불러옵니다.

## 사용법

1. `KeyPresser.exe`를 실행합니다.
2. 키, 주기, 대상을 고릅니다.
3. **시작**을 누르거나 `Ctrl+Alt+P`를 누릅니다.

서명되지 않은 개인용 프로그램이라 Windows SmartScreen 경고가 뜰 수 있습니다. 소스를 확인하고 직접 빌드하셔도 됩니다.

## 제한 사항

- **백그라운드 모드는 프로그램에 따라 동작하지 않습니다.** VS Code 같은 Chromium 기반 프로그램은 뒤에 있을 때 이 방식의 키를 무시합니다. 이런 프로그램에는 앞으로 불러오는 모드를 쓰세요.
- 백그라운드 모드는 시작 전에 대상 창의 입력칸을 한 번 클릭해 두어야 합니다. 뒤에 있는 창은 Windows가 입력칸을 알려주지 않아서, 앞에 있을 때 기억해 둡니다.
- 관리자 권한으로 실행 중인 창에 보내려면 KeyPresser도 관리자 권한으로 실행해야 합니다.
- 화면이 잠겨 있거나 원격 데스크톱 연결이 끊긴 상태에서는 키가 전달되지 않습니다.
- 같은 프로그램의 창이 여러 개면 저장된 대상과 다른 창이 골라질 수 있습니다. 시작 전에 대상 칸을 확인해 주세요.

키 입력은 앞에 있는 창에 그대로 들어갑니다. 대상을 잘못 고르면 다른 프로그램에서 그 키의 기능이 실행될 수 있습니다.

## 원격 데스크톱 연결이 끊겨도 계속 쓰기 (선택)

원격 데스크톱 연결이 끊기면 세션에 화면이 없어져 키 입력이 멈춥니다. `rdp_to_console.ps1`은 연결이 끊겼을 때 세션을 PC 본체 화면(console)으로 옮겨 이 문제를 피합니다.

> **주의: 이 스크립트를 쓰면 원격 연결이 끊긴 뒤 PC 본체 화면이 잠기지 않은 채로 남습니다. 누구나 그 PC를 조작할 수 있으므로, 다른 사람이 접근할 수 있는 PC에서는 쓰지 마세요. 회사 PC라면 보안 정책도 확인해 주세요.**

설치 방법:

1. 이 폴더를 `C:\Users\<계정>\` 아래에 둡니다. 스크립트가 이 경로에서 계정 이름을 얻습니다.
2. `rdp_to_console_task.xml`의 `C:\Users\YOUR_NAME\KeyPresser\` 경로를 실제 경로로 고칩니다.
3. 관리자 PowerShell에서 등록합니다.

```powershell
schtasks /create /tn "KeyPresser RDP to console" /xml rdp_to_console_task.xml
```

연결이 끊긴 뒤 5초를 기다려 여전히 끊긴 상태일 때만 세션을 옮깁니다. 기록은 같은 폴더의 `rdp_to_console.log`에 남습니다. 지우려면 `schtasks /delete /tn "KeyPresser RDP to console"`을 실행합니다.

## 빌드

.NET Framework가 있는 Windows에서:

```powershell
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& $csc /target:winexe /out:KeyPresser.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll KeyPresser.cs
```
