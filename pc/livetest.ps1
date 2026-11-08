Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class Fg {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);

    public static IntPtr FindWindowByProcess(string name) {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            if (!IsWindowVisible(h)) return true;
            if (GetWindowTextLength(h) == 0) return true;
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            if (pid == 0) return true;
            try {
                System.Diagnostics.Process p = System.Diagnostics.Process.GetProcessById((int)pid);
                if (string.Equals(p.ProcessName, name, StringComparison.OrdinalIgnoreCase)) {
                    found = h;
                    return false;
                }
            } catch { }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
"@

$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$inFile  = Join-Path $dir 'livetest-in.txt'
$outFile = Join-Path $dir 'livetest-out.txt'
$exe     = Join-Path $dir 'Miaohua.exe'

$VK_CTRL = 0x11; $VK_SHIFT = 0x10; $VK_ALT = 0x12
$VK_A = 0x41; $VK_C = 0x43; $VK_V = 0x56; $VK_M = 0x4D

function Combo([int[]]$mods, [int]$key) {
    foreach ($m in $mods) { [Fg]::keybd_event([byte]$m, 0, 0, [IntPtr]::Zero) }
    Start-Sleep -Milliseconds 50
    [Fg]::keybd_event([byte]$key, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 50
    [Fg]::keybd_event([byte]$key, 0, 2, [IntPtr]::Zero)
    foreach ($m in $mods) { [Fg]::keybd_event([byte]$m, 0, 2, [IntPtr]::Zero) }
    Start-Sleep -Milliseconds 90
}

$log = New-Object System.Text.StringBuilder
function Say($s) { [void]$log.AppendLine($s); Write-Host $s }
function Finish() { [IO.File]::WriteAllText($outFile, $log.ToString(), (New-Object Text.UTF8Encoding($false))) }
function Cleanup() {
    Stop-Process -Name Miaohua -Force -EA SilentlyContinue
    Get-Process -Name Notepad -EA SilentlyContinue | Stop-Process -Force -EA SilentlyContinue
}

$text = [IO.File]::ReadAllText($inFile, [Text.Encoding]::UTF8).Trim()

$helper = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 2
Say ("helper alive      : " + (-not $helper.HasExited))
if ($helper.HasExited) { Finish; exit 1 }

Start-Process notepad | Out-Null
$h = [IntPtr]::Zero
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Milliseconds 500
    $h = [Fg]::FindWindowByProcess('Notepad')
    if ($h -ne [IntPtr]::Zero) { break }
}
Say ("notepad window    : " + $h)
if ($h -eq [IntPtr]::Zero) {
    $names = (Get-Process | Where-Object { $_.ProcessName -like '*ote*' } | Select-Object -ExpandProperty ProcessName) -join ','
    Say ("ABORT: no notepad window found. processes matching *ote*: " + $names)
    Cleanup; Finish; exit 1
}

[void][Fg]::ShowWindow($h, 9)
[Fg]::keybd_event($VK_ALT, 0, 0, [IntPtr]::Zero); [Fg]::keybd_event($VK_ALT, 0, 2, [IntPtr]::Zero)
Start-Sleep -Milliseconds 300
[void][Fg]::SetForegroundWindow($h)
Start-Sleep -Milliseconds 800

$fgOk = ([Fg]::GetForegroundWindow() -eq $h)
Say ("notepad foreground: " + $fgOk)
if (-not $fgOk) {
    Say 'ABORT: notepad is not foreground - sent NO keystrokes on purpose'
    Cleanup; Finish; exit 1
}

Set-Clipboard -Value $text
Start-Sleep -Milliseconds 250
Combo @($VK_CTRL) $VK_V
Start-Sleep -Milliseconds 700

Set-Clipboard -Value ' '
Combo @($VK_CTRL) $VK_A
Combo @($VK_CTRL) $VK_C
Start-Sleep -Milliseconds 500
$before = Get-Clipboard -Raw
Say ("BEFORE : " + $before)

Combo @($VK_CTRL, $VK_SHIFT) $VK_M
Start-Sleep -Milliseconds 2000

Set-Clipboard -Value ' '
Combo @($VK_CTRL) $VK_A
Combo @($VK_CTRL) $VK_C
Start-Sleep -Milliseconds 500
$after = Get-Clipboard -Raw
Say ("AFTER  : " + $after)

if ($after -and $after -ne $before) { Say 'RESULT : TRANSFORM WORKED' } else { Say 'RESULT : NO CHANGE' }

Say '--- helper log (last 6) ---'
$lg = Join-Path $dir 'miaohua.log'
if (Test-Path $lg) { Get-Content $lg -Encoding UTF8 | Select-Object -Last 6 | ForEach-Object { Say $_ } }

Cleanup
Finish
