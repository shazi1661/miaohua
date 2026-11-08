# 通过 MuMu 模拟器给喵化助手安卓版截图。
#
# 为什么需要它：改安卓界面时，光看代码和算尺寸是不够的 —— 这一天的教训是
# 「我算得对」和「屏幕上真的对」是两件事。有个能自己跑起来看的通道，
# 就不用每次都让用户装一遍再截图。
#
# 前置：
#   1. MuMu 模拟器已安装（D:\MuMuPlayer）
#   2. 不需要手动启动 —— 这个脚本会自己拉起虚拟机
#
# 用法：
#   .\安卓截图.ps1                 # 装当前 APK，截首页
#   .\安卓截图.ps1 -NoInstall      # 不重装，只截图（改完代码已经装过时用）
#   .\安卓截图.ps1 -Tap 90,1180    # 先点一下再截（切换 tab 用）
#   .\安卓截图.ps1 -Out 我的页      # 输出文件名后缀

param(
    [switch]$NoInstall,
    [string]$Tap = '',
    [string]$Out = '首页',
    [int]$Wait = 6
)

$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$mumu    = 'D:\MuMuPlayer\nx_main\MuMuManager.exe'
$adb     = 'D:\harness gzq\009\android-sdk\platform-tools\adb.exe'
$apk     = 'D:\harness gzq\009\miaohua\android\Miaohua.apk'
$outDir  = 'D:\harness gzq\009\界面试验'
$pkg     = 'com.miaohua.helper'

if (-not (Test-Path $mumu)) { throw "找不到 MuMuManager: $mumu" }
if (-not (Test-Path $adb))  { throw "找不到 adb: $adb" }

# --- 1. 确保虚拟机在跑 ---
$info = & $mumu info -v 0 2>&1 | Out-String
if ($info -notmatch '"is_android_started":\s*true') {
    "启动 MuMu…"
    & $mumu control -v 0 launch | Out-Null
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 5
        $info = & $mumu info -v 0 2>&1 | Out-String
        if ($info -match '"is_android_started":\s*true') { break }
    }
}
if ($info -notmatch '"is_android_started":\s*true') { throw 'MuMu 没起来' }

# adb 端口从 info 里读，不写死
$port = '16384'
if ($info -match '"adb_port":\s*(\d+)') { $port = $Matches[1] }

# --- 2. 连接 ---
& $adb connect "127.0.0.1:$port" | Out-Null
Start-Sleep -Seconds 2
$devices = & $adb devices 2>&1 | Out-String
if ($devices -notmatch [regex]::Escape("127.0.0.1:$port") + '\s+device') {
    throw "adb 没连上 127.0.0.1:$port"
}

# 竖屏
& $adb shell settings put system accelerometer_rotation 0 | Out-Null
& $adb shell settings put system user_rotation 0 | Out-Null

# --- 3. 装（可选）---
if (-not $NoInstall) {
    if (-not (Test-Path $apk)) { throw "找不到 APK: $apk" }
    "安装 $apk"
    & $adb install -r $apk 2>&1 | Select-String 'Success|Failure' | ForEach-Object { '  ' + $_.Line.Trim() }
}

# --- 4. 拉起 + 可选点击 ---
& $adb shell am force-stop $pkg | Out-Null
Start-Sleep -Seconds 2
& $adb shell am start -n "$pkg/.MainActivity" | Out-Null
Start-Sleep -Seconds $Wait

if ($Tap -ne '') {
    "点击 $Tap"
    & $adb shell input tap $Tap | Out-Null
    Start-Sleep -Seconds 3
}

# --- 5. 截图 ---
# ★ 必须【先存到设备再 pull】。
#   直接 `adb exec-out screencap -p > 文件` 在 PowerShell 里会把二进制当文本处理，
#   出来的 PNG 是坏的（我就这么踩过一次）。
$remote = '/sdcard/miaohua-shot.png'
& $adb shell rm -f $remote | Out-Null
& $adb shell screencap -p $remote | Out-Null
Start-Sleep -Seconds 2

if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }
$local = Join-Path $outDir ("模拟器-" + $Out + ".png")
Remove-Item $local -Force -ErrorAction SilentlyContinue
& $adb pull $remote $local 2>&1 | Select-Object -Last 1 | ForEach-Object { '  ' + $_ }

if (-not (Test-Path $local)) { throw '截图失败' }
$b = [IO.File]::ReadAllBytes($local)
$ok = ($b.Length -gt 8 -and $b[0] -eq 0x89 -and $b[1] -eq 0x50)
"截图: $local  ({0} KB, PNG={1})" -f ([math]::Round($b.Length / 1KB, 0)), $ok
if (-not $ok) { throw 'PNG 头不对，截图是坏的' }
