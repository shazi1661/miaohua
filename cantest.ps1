# 检查当前 MuMu 前台软件能不能用来演示喵化助手
#
# 原理：安卓的无障碍服务只能操作【标准可编辑控件】（android.widget.EditText 那类）。
# 自绘的编辑器（比如 MT 管理器的）不向系统报告自己是输入框，所以读不到也写不进去。
# 这个脚本用 uiautomator 把界面结构抓下来，数一数有几个可编辑节点。

$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$adb  = 'D:\harness gzq\009\android-sdk\platform-tools\adb.exe'
$mgr  = 'D:\MuMuPlayer\nx_main\MuMuManager.exe'
$port = 16384
$cfg = "$env:APPDATA\Netease\MuMuPlayer-12.0\vms\0\configs\vm_config.json"
if (Test-Path $cfg) {
    try { $j = Get-Content $cfg -Raw -Encoding UTF8 | ConvertFrom-Json
          if ($j.adb_port) { $port = $j.adb_port } } catch { }
}
$dev = "127.0.0.1:$port"

$devs = & $adb devices 2>&1 | Out-String
if ($devs -notmatch [regex]::Escape($dev)) {
    Write-Host "MuMu 没连上，正在启动…" -ForegroundColor Yellow
    & $mgr launch 0 2>&1 | Out-Null
    Start-Sleep -Seconds 45
    & $adb connect $dev 2>&1 | Out-Null
    Start-Sleep -Seconds 3
}

$focus = & $adb -s $dev shell dumpsys window 2>&1 | Select-String 'mCurrentFocus' | Select-Object -First 1
if (-not $focus) { Write-Host "读不到前台窗口" -ForegroundColor Red; exit 1 }
$pkg = ''
$m = [regex]::Match($focus.Line, 'u0\s+([A-Za-z0-9_.]+)/')
if ($m.Success) { $pkg = $m.Groups[1].Value }

Write-Host ""
Write-Host "当前前台：$pkg" -ForegroundColor Cyan
Write-Host ""

& $adb -s $dev shell uiautomator dump /sdcard/_ct.xml 2>&1 | Out-Null
Start-Sleep -Milliseconds 800
$xml = & $adb -s $dev shell cat /sdcard/_ct.xml 2>&1 | Out-String

if ($xml.Length -lt 100) {
    Write-Host "抓不到界面结构 —— 这个软件可能屏蔽了无障碍扫描" -ForegroundColor Red
    Write-Host "换一个软件再试" -ForegroundColor Red
    exit 1
}

# 两个条件任一都算可编辑：uiautomator 有时不给 editable 属性，但类名一定是 EditText
$ed = [regex]::Matches($xml, '<node[^>]*(?:class="android\.widget\.EditText"|editable="true")[^>]*>')
Write-Host "可编辑输入框数量：$($ed.Count)" -ForegroundColor $(if ($ed.Count -gt 0) { 'Green' } else { 'Red' })
Write-Host ""

if ($ed.Count -gt 0) {
    Write-Host "★ 可以拿来演示 ★" -ForegroundColor Green
    Write-Host ""
    Write-Host "  找到这些输入框："
    $i = 0
    foreach ($x in $ed) {
        $i++
        $cls = [regex]::Match($x.Value, 'class="([^"]*)"').Groups[1].Value
        $tx  = [regex]::Match($x.Value, 'text="([^"]*)"').Groups[1].Value
        if ($tx.Length -gt 24) { $tx = $tx.Substring(0,24) + '…' }
        Write-Host "    $i) $cls   [$tx]"
    }
    Write-Host ""
    Write-Host "  记得把它加进白名单（手机版认【包名】）：" -ForegroundColor Yellow
    Write-Host "      包名 = $pkg" -ForegroundColor Yellow
    Write-Host "  填在喵化助手 →「规则」页 → 只在这些应用里生效 那一段" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  然后在这个输入框里打字，停一下（或按 Ctrl+Shift+M）就应该变了"
} else {
    Write-Host "✗ 不能用来演示 ✗" -ForegroundColor Red
    Write-Host ""
    Write-Host "  这个软件的输入框是【自绘的】，不向系统报告自己是输入框，"
    Write-Host "  安卓的无障碍服务读不到也写不进去。换一个软件试。"
    Write-Host ""
    Write-Host "  界面里出现的控件类名："
    [regex]::Matches($xml, 'class="([^"]*)"') |
        ForEach-Object { $_.Groups[1].Value } |
        Sort-Object -Unique |
        Select-Object -First 15 |
        ForEach-Object { Write-Host "      $_" }
}
Write-Host ""
& $adb -s $dev shell rm -f /sdcard/_ct.xml 2>&1 | Out-Null