# 看当前 MuMu 前台应用的包名 —— 用来填「生效程序」白名单
#
# 安卓版认的是【包名】（com.xxx.yyy 那种），不是电脑版那种进程名。
# 用法：在 MuMu 里把目标应用切到前台，然后跑这个脚本。

$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$adb  = 'D:\harness gzq\009\android-sdk\platform-tools\adb.exe'
$mgr  = 'D:\MuMuPlayer\nx_main\MuMuManager.exe'
$port = 16384

# 端口以 MuMu 的配置为准（万一你改过）
$cfg = "$env:APPDATA\Netease\MuMuPlayer-12.0\vms\0\configs\vm_config.json"
if (Test-Path $cfg) {
    try { $j = Get-Content $cfg -Raw -Encoding UTF8 | ConvertFrom-Json
          if ($j.adb_port) { $port = $j.adb_port } } catch { }
}
$dev = "127.0.0.1:$port"

# 没连上就先启动 + 连接
$devs = & $adb devices 2>&1 | Out-String
if ($devs -notmatch [regex]::Escape($dev)) {
    Write-Host "MuMu 没连上，正在启动…" -ForegroundColor Yellow
    & $mgr launch 0 2>&1 | Out-Null
    Start-Sleep -Seconds 45
    & $adb connect $dev 2>&1 | Out-Null
    Start-Sleep -Seconds 3
}

Write-Host ""
Write-Host "当前前台应用：" -ForegroundColor Cyan
$focus = & $adb -s $dev shell dumpsys window 2>&1 | Select-String 'mCurrentFocus' | Select-Object -First 1
if (-not $focus) { Write-Host "  读不到（模拟器没开或者没连上）" -ForegroundColor Red; exit 1 }

$line = $focus.Line
Write-Host "  $($line.Trim())"
Write-Host ""

# 从 mCurrentFocus=Window{hash u0 包名/Activity} 里抠包名
$m = [regex]::Match($line, 'u0\s+([A-Za-z0-9_.]+)/')
if ($m.Success) {
    $pkg = $m.Groups[1].Value
    Write-Host "★ 包名 = $pkg" -ForegroundColor Green
    Write-Host ""
    Write-Host "把它填进喵化助手的「生效程序」里（手机版是包名，和电脑版不一样）："
    Write-Host "    程序 = $pkg" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "已经装好的第三方应用（可以直接拿来测）：" -ForegroundColor Cyan
    & $adb -s $dev shell pm list packages -3 2>&1 |
        Where-Object { $_ -match '^package:' } |
        ForEach-Object { '    ' + $_.Trim().Replace('package:','') }
} else {
    Write-Host "  没解析出包名，把上面那行发我看看" -ForegroundColor Red
}
Write-Host ""