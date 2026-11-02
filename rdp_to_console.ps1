# 원격(RDP) 연결이 끊기면 세션을 모니터(console)로 옮겨 화면을 살려 둠 → KeyPresser가 계속 키를 누를 수 있음
# 예약 작업 "KeyPresser RDP to console"이 SYSTEM으로 실행 (트리거: LocalSessionManager 이벤트 24 = 세션 연결 끊김)
$log = Join-Path $PSScriptRoot 'rdp_to_console.log'
# SYSTEM으로 돌기 때문에 $env:USERNAME을 쓸 수 없음 → 설치 경로(C:\Users\<계정>\...)에서 계정 이름을 얻음
if ($PSScriptRoot -notmatch '\\Users\\([^\\]+)') { exit }
$user = [regex]::Escape($Matches[1])
function Log($m) { Add-Content -Path $log -Value ((Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + ' ' + $m) -Encoding UTF8 }

# 콘솔→원격으로 넘어갈 때도 잠깐 끊김(24)이 나므로, 몇 초 뒤에도 여전히 끊긴 상태일 때만 옮김
Start-Sleep -Seconds 5
$pattern = "\s$user\s+(\d+)\s+(\w+)"
$line = qwinsta | Where-Object { $_ -match $pattern } | Select-Object -First 1
if (-not $line -or $line -notmatch $pattern) { Log "$user 세션 없음"; exit }
$id = [int]$Matches[1]

# qwinsta 상태 글자는 실행 계정 언어에 따라 바뀜(SYSTEM은 "활성") → WTS API 상태 번호로 판정 (4 = 연결 끊김)
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class Wts {
    [DllImport("wtsapi32.dll")] static extern bool WTSQuerySessionInformation(IntPtr srv, int id, int cls, out IntPtr buf, out int len);
    [DllImport("wtsapi32.dll")] static extern void WTSFreeMemory(IntPtr p);
    public static int State(int id) {
        IntPtr buf; int len;
        if (!WTSQuerySessionInformation(IntPtr.Zero, id, 8, out buf, out len)) return -1; // 8 = WTSConnectState
        int s = Marshal.ReadInt32(buf); WTSFreeMemory(buf); return s;
    }
}
'@
$state = [Wts]::State($id)
if ($state -ne 4) { Log "세션 $id 상태 $state(0=활성, 4=끊김) → 그대로 둠"; exit }

tscon $id /dest:console
Log "세션 $id 끊김 → console로 이동 (tscon 종료코드 $LASTEXITCODE)"
