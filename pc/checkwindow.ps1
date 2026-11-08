Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class WinEnum {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    public static string[] WindowsOf(int pid) {
        List<string> list = new List<string>();
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            uint p;
            GetWindowThreadProcessId(h, out p);
            if ((int)p != pid) return true;
            StringBuilder sb = new StringBuilder(400);
            GetWindowTextW(h, sb, 400);
            RECT r;
            GetWindowRect(h, out r);
            list.Add((IsWindowVisible(h) ? "VISIBLE" : "hidden ")
                + " | " + (r.Right - r.Left) + "x" + (r.Bottom - r.Top)
                + " | title=[" + sb.ToString() + "]");
            return true;
        }, IntPtr.Zero);
        return list.ToArray();
    }
}
"@

$dir = 'D:\harness gzq\009\miaohua\pc'
$exe = Join-Path $dir 'Miaohua.exe'
$log = Join-Path $dir 'miaohua.log'

# expected window title, built from code points so this script stays pure ASCII
$expectedTitle = [string]([char]0x55B5 + [char]0x5316 + [char]0x52A9 + [char]0x624B)

function StopAll {
    Get-Process -Name Miaohua -ErrorAction SilentlyContinue | ForEach-Object {
        try { $_.Kill() } catch { }
    }
    Start-Sleep -Milliseconds 800
}

function Report($p) {
    $wins = [WinEnum]::WindowsOf($p.Id)
    if ($wins.Count -eq 0) { Write-Host "    (no top-level windows)" }
    # Write-Host, not bare strings: bare output would leak into this function's
    # return value and turn the count into an array
    foreach ($w in $wins) { Write-Host "    $w" }
    $visible = @($wins | Where-Object { $_ -like 'VISIBLE*' })
    return [int]$visible.Count
}

"########## A. normal start (should SHOW the window) ##########"
StopAll
if (Test-Path $log) { Remove-Item $log -Force }
$a = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 4
"  process alive : $(-not $a.HasExited)"
$visA = Report $a
"  visible windows : $visA"
$titleOk = @([WinEnum]::WindowsOf($a.Id) | Where-Object { $_ -like "*$expectedTitle*" }).Count -gt 0
"  window title matches expected : $titleOk"
if ($visA -ge 1 -and $titleOk) { "  ==> PASS: main window is shown on normal start" }
else { "  ==> FAIL: no main window" }

"########## B. --tray start (should stay HIDDEN) ##########"
StopAll
$b = Start-Process -FilePath $exe -ArgumentList '--tray' -PassThru
Start-Sleep -Seconds 4
"  process alive : $(-not $b.HasExited)"
$visB = Report $b
"  visible windows : $visB"
if ($visB -eq 0) { "  ==> PASS: --tray starts silently in the tray" }
else { "  ==> FAIL: --tray still showed a window" }

"########## C. log from mode B ##########"
Get-Content $log -Encoding UTF8 | Select-Object -Last 6 | ForEach-Object { "    $_" }

"########## D. leaving one instance running normally ##########"
StopAll
$c = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 4
"  running pid : $($c.Id)   visible windows : $(Report $c)"
