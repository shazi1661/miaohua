# 一次打出【自带3B】和【自带7B】两个大包 ✗ 再把三个包归位到交付目录 ✓
#
# 为什么要跑两遍 pack-all：
#   pack-all 每次都把交付目录清空 ✗ 而且大包文件名固定 ✗ 后一次会盖掉前一次 ✓
#   所以：跑 3B → 存到 packed\3b.zip ✗ 换成 7B 再跑 → 存 packed\7b.zip ✓
#        最后统一复制回交付目录 ✓
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$proj  = 'D:\harness gzq\009\miaohua'
$lvl   = 'D:\harness gzq\009'
$rel   = Join-Path $lvl 'release-自带模型\喵化助手_PC2.15.6_安卓3.4.11\喵化助手\电脑版\模型'
$store = Join-Path $lvl 'models'
$safe  = Join-Path $lvl 'packed'
$hand  = 'D:\喵化助手'
$py    = 'C:\Users\ZJC\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe'

New-Item -ItemType Directory -Force -Path $safe | Out-Null
if (-not (Test-Path $rel)) { throw "发布目录里的模型文件夹不存在: $rel" }

$g3 = Join-Path $store '3b-test-only.gguf'
$g7 = Join-Path $proj  'pc\模型\qwen2.5-7b-instruct-q4_k_m.gguf'
if (-not (Test-Path $g3)) { throw "找不到 3B: $g3" }
if (-not (Test-Path $g7)) { throw "找不到 7B: $g7" }

function Set-Model([string]$src, [string]$name) {
    Get-ChildItem $rel -Filter '*.gguf' -ErrorAction SilentlyContinue | Remove-Item -Force
    Copy-Item $src (Join-Path $rel $name) -Force
    $sz = [math]::Round((Get-Item (Join-Path $rel $name)).Length/1MB, 0)
    "  >>> 模型已换成 $name  ($sz MB)"
}

"=== 第一遍：自带 3B 模型 ==="
Set-Model $g3 'qwen2.5-3b-instruct-q4_k_m.gguf'
& (Join-Path $proj 'pack-all.ps1') 2>&1 | Select-String 'update   :|full     :' | ForEach-Object { "  " + $_.Line.Trim() }
Copy-Item (Join-Path $lvl '喵化助手_PC2.15.6_安卓3.4.11_自带模型_20261017.zip') (Join-Path $safe '3b.zip') -Force
"  ✓ 存到 packed\3b.zip"

""
"=== 第二遍：自带 7B 模型 ==="
Set-Model $g7 'qwen2.5-7b-instruct-q4_k_m.gguf'
& (Join-Path $proj 'pack-all.ps1') 2>&1 | Select-String 'update   :|full     :' | ForEach-Object { "  " + $_.Line.Trim() }
Copy-Item (Join-Path $lvl '喵化助手_PC2.15.6_安卓3.4.11_自带模型_20261017.zip') (Join-Path $safe '7b.zip') -Force
"  ✓ 存到 packed\7b.zip"

""
"=== 还原成 3B（默认发布用小的 ✗ 兼容性好）==="
Set-Model $g3 'qwen2.5-3b-instruct-q4_k_m.gguf'

""
"=== 重建交付目录（文档 / APK）==="
& $py 'D:\harness gzq\009\界面试验\i18n\redeliver.py' 2>&1 | Select-Object -Last 1 | ForEach-Object { "  " + $_ }

""
"=== 三个包归位 ==="
Copy-Item (Join-Path $safe '3b.zip') (Join-Path $hand '喵化助手_PC2.15.6_安卓3.4.11_自带模型_20261017.zip') -Force
Copy-Item (Join-Path $safe '7b.zip') (Join-Path $hand '喵化助手_PC2.15.6_安卓3.4.11_自带7B模型_20261017.zip') -Force
Copy-Item (Join-Path $lvl '喵化助手_PC2.15.6_安卓3.4.11_无模型版_20261017.zip') $hand -Force

""
"=== 交付目录里的包 ==="
Get-ChildItem $hand -Filter '*.zip' | Sort-Object Name | ForEach-Object {
    "  {0,-52} {1} MB" -f $_.Name, [math]::Round($_.Length/1MB, 0)
}
""
"全部完成 ✓"
