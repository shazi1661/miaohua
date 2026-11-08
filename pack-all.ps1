# Builds BOTH release packages with the SAME top-level folder, which is what makes the
# update package usable at all.
#
# The problem it fixes: the model package used to have no top-level folder (its entries were
# 电脑版\, 安卓版\ at the root) while the small package had one (喵化助手\). Extracting the
# small one into the folder the big one produced therefore created a NESTED 喵化助手\
# instead of overwriting - so "send only the update next time" did not actually work.
#
# Now both are 喵化助手\<...>, so extracting the update into the installed folder merges:
# programs and docs are replaced, 电脑版\模型\ is left completely alone.
#
# TWO RULES FOR EDITING THIS FILE, both learned the hard way:
#
#   1. It MUST keep its UTF-8 BOM. Without it PowerShell reads the file as ANSI, the Chinese
#      in the comments turns to mojibake, and the mojibake swallows the following code line.
#      That silently emptied $AND and $MODEL once and produced two broken 2 GB packages.
#   2. Never edit it with an inline PowerShell command containing Chinese - the text gets
#      mangled on the way in. Write the Chinese to a source file and read it as UTF-8, which
#      is what release-src\升级说明.txt is for.
#
#   pwsh -File pack-all.ps1

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function U([int[]]$codes) { -join ($codes | ForEach-Object { [char]$_ }) }

$PKG    = U 0x55B5,0x5316,0x52A9,0x624B           # 喵化助手
$PC     = U 0x7535,0x8111,0x7248                  # 电脑版
$AND    = U 0x5B89,0x5353,0x7248                  # 安卓版
$MODEL  = U 0x6A21,0x578B                         # 模型
$UPDN   = U 0x5347,0x7EA7,0x8BF4,0x660E           # 升级说明 (note inside the package)
$MANN   = U 0x4F7F,0x7528,0x8BF4,0x660E,0x4E66    # 使用说明书
$LOGN   = U 0x66F4,0x65B0,0x8BB0,0x5F55           # 更新记录
$QUICKN = U 0x5FEB,0x901F,0x5F00,0x59CB           # 快速开始
$MODELPKG = U 0x81EA,0x5E26,0x6A21,0x578B         # 自带模型
$UPDATEPKG = U 0x65E0,0x6A21,0x578B,0x7248         # 无模型版
$RELDIR = U 0x81EA,0x5E26,0x6A21,0x578B
$ANDNAME = U 0x5B89,0x5353
$NEWSN  = U 0x66F4,0x65B0,0x516C,0x544A              # 更新公告                     # 安卓 (no 版 - the stamp is PC2.11_安卓3.0)

$root    = 'D:\harness gzq\009'
$proj    = Join-Path $root 'miaohua'
$staging = Join-Path $root 'pack-staging'
$inner   = Join-Path $staging $PKG
$pcDir   = Join-Path $inner $PC
$andDir  = Join-Path $inner $AND

# --- versions, read from the artefacts ---------------------------------------------------
$verTmp = Join-Path $env:TEMP 'miaohua-ver.txt'
Start-Process -FilePath (Join-Path $proj 'pc\Miaohua.exe') -ArgumentList '--version' `
    -NoNewWindow -Wait -RedirectStandardOutput $verTmp | Out-Null
$pcVer = (Get-Content $verTmp -Raw).Trim()
Remove-Item $verTmp -Force -ErrorAction SilentlyContinue

$andVer = '0.0'
$aapt = Join-Path $root 'android-sdk\build-tools\34.0.0\aapt2.exe'
if (Test-Path $aapt) {
    $badging = & $aapt dump badging (Join-Path $proj 'android\Miaohua.apk') 2>$null
    $m = [regex]::Match(($badging -join "`n"), "versionName='([^']+)'")
    if ($m.Success) { $andVer = $m.Groups[1].Value }
}
$stamp = "PC$pcVer`_$ANDNAME$andVer"
$date  = Get-Date -Format 'yyyyMMdd'
"versions : $stamp  ($date)"

# --- staging -----------------------------------------------------------------------------
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Force -Path $pcDir  | Out-Null
New-Item -ItemType Directory -Force -Path $andDir | Out-Null
foreach ($d in @($inner, $pcDir, $andDir)) {
    if (-not (Test-Path $d)) { throw "staging folder was not created: $d" }
}

function Put($src, $dstDir, $label) {
    if (-not (Test-Path $src)) { "  skip    ($label)"; return }
    if (-not (Test-Path $dstDir)) { throw "destination folder missing for $label : $dstDir" }
    Copy-Item $src $dstDir -Force
    $landed = Join-Path $dstDir (Split-Path $src -Leaf)
    if (-not (Test-Path $landed)) { throw "copy did not land where expected: $landed" }
    "  copy    ($label) -> $landed"
}

"stage    : $staging"
Put (Join-Path $proj 'pc\Miaohua.exe')       $pcDir  'exe'
Put (Join-Path $proj 'pc\rules.txt')         $pcDir  'rules'
Put (Join-Path $proj 'pc\rules-en.txt')         $pcDir  'rules'
Put (Join-Path $proj 'pc\logo.png')          $pcDir  'logo'
Put (Join-Path $proj 'pc\Miaohua.ico')      $pcDir  'icon'
Put (Join-Path $proj 'pc\bg.png')            $pcDir  'bg'
Put (Join-Path $proj 'pc\style-*.txt')       $pcDir  'styles'
Put (Join-Path $proj 'android\Miaohua.apk')  $andDir 'apk'

# ★★★ 2026-10-18：小模型 clf.bin 两个包都要带 ★★★
#   以前只在「自带模型」那个包里拷整个 模型\ 文件夹 ✗
#   而 release-* 里那个文件夹又没有 clf.bin ✗
#   结果两个包都没有小模型 ✗ 用户从来没用上过 ✓
$clfDir = Join-Path $pcDir $MODEL
New-Item -ItemType Directory -Force -Path $clfDir | Out-Null
Put (Join-Path $proj ('pc\' + $MODEL + '\clf.bin')) $clfDir 'clf.bin (tiny classifier)'
Put (Join-Path $proj ($MANN + '.html'))      $inner  'manual'
Put (Join-Path $proj ($LOGN + '.txt'))       $inner  'changelog'
Put (Join-Path $proj ($NEWSN + '.txt')) $inner  'news'   # ★ 精简版公告
Put (Join-Path $proj '手机版说明.txt')  $inner  'mobiledoc'
Put (Join-Path $proj '权限说明.txt')    $inner  'permdoc'

# docs that live in release-src\ as UTF-8, copied out with a BOM + CRLF so Notepad is happy
foreach ($pair in @(@($UPDN, 'note'), @($QUICKN, 'quickstart'))) {
    $src = Join-Path $proj ('release-src\' + $pair[0] + '.txt')
    if (-not (Test-Path $src)) { "  skip    ($($pair[1]))"; continue }
    $text = [IO.File]::ReadAllText($src, [Text.Encoding]::UTF8)
    $text = $text.Replace("`r`n", "`n").Replace("`r", "`n").Replace("`n", "`r`n")
    [IO.File]::WriteAllText((Join-Path $inner ($pair[0] + '.txt')), $text,
                            (New-Object Text.UTF8Encoding($true)))
    "  copy    ($($pair[1])) -> $(Join-Path $inner ($pair[0] + '.txt'))"
}

# --- the update package ------------------------------------------------------------------
Add-Type -AssemblyName System.IO.Compression.FileSystem

$updateZip = Join-Path $root ($PKG + '_' + $stamp + '_' + $UPDATEPKG + '_' + $date + '.zip')
if (Test-Path $updateZip) { Remove-Item $updateZip -Force }
[IO.Compression.ZipFile]::CreateFromDirectory($staging, $updateZip,
    [IO.Compression.CompressionLevel]::Optimal, $false, [Text.Encoding]::UTF8)
"update   : $(Split-Path $updateZip -Leaf)  ($([math]::Round((Get-Item $updateZip).Length/1KB,0)) KB)"

# --- the full package: the same staging plus the model -----------------------------------
$modelSrcRoot = Join-Path $root ('release-' + $RELDIR)
$modelDir = Get-ChildItem $modelSrcRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like ('*' + $stamp + '*') } | Select-Object -First 1
if (-not $modelDir) { throw "no model folder matching $stamp under $modelSrcRoot" }

$srcModel = Join-Path $modelDir.FullName ($PKG + '\' + $PC + '\' + $MODEL)  # PKG\PC\MODEL
$dstModel = Join-Path $pcDir $MODEL          # under 电脑版, NOT at the package root
if (-not (Test-Path $srcModel)) { throw "model source not found: $srcModel" }
New-Item -ItemType Directory -Force -Path $dstModel | Out-Null
Copy-Item (Join-Path $srcModel '*') $dstModel -Recurse -Force
$n  = @(Get-ChildItem $dstModel -Recurse -File).Count
$mb = [math]::Round((Get-ChildItem $dstModel -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 0)
"model    : $n files, $mb MB"
if ($n -lt 10) { throw "model copy looks wrong ($n files)" }

$fullZip = Join-Path $root ($PKG + '_' + $stamp + '_' + $MODELPKG + '_' + $date + '.zip')
if (Test-Path $fullZip) { Remove-Item $fullZip -Force }
[IO.Compression.ZipFile]::CreateFromDirectory($staging, $fullZip,
    [IO.Compression.CompressionLevel]::Fastest, $false, [Text.Encoding]::UTF8)
"full     : $(Split-Path $fullZip -Leaf)  ($([math]::Round((Get-Item $fullZip).Length/1MB,0)) MB)"

# --- layout guard: refuse to ship a package whose structure is wrong ----------------------
function Assert-Layout($zipPath, [string[]]$required) {
    $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try { $names = @($zip.Entries | ForEach-Object { $_.FullName }) } finally { $zip.Dispose() }
    $bad = @($required | Where-Object { $names -notcontains $_ })
    if ($bad.Count -gt 0) {
        "!! LAYOUT WRONG in $(Split-Path $zipPath -Leaf)"
        foreach ($b in $bad) { "     missing: $b" }
        "   actual entries (first 12):"
        $names | Select-Object -First 12 | ForEach-Object { "     $_" }
        throw "refusing to ship a package with the wrong layout"
    }
    "   layout ok: $($required.Count) key entries checked in $(Split-Path $zipPath -Leaf)"
}
Assert-Layout $updateZip @("$PKG\$PC\Miaohua.exe", "$PKG\$AND\Miaohua.apk",
                           "$PKG\$PC\$MODEL\clf.bin")
Assert-Layout $fullZip   @("$PKG\$PC\Miaohua.exe", "$PKG\$AND\Miaohua.apk",
                           "$PKG\$PC\$MODEL\llama-server.exe",
                           "$PKG\$PC\$MODEL\clf.bin")

Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue

# --- copies for handing over -------------------------------------------------------------
$hand = 'D:\' + $PKG
# ★ 2026-10-18：只删旧 zip / apk ✗ 不再清空整个交付目录 ✓
#   原来是删所有 ✗ 把 模型\ 说明\ 开发笔记\ 视频素材\ 全删了 ✗
if (Test-Path $hand) { Get-ChildItem $hand -File | Where-Object { $_.Extension -eq '.zip' -or $_.Extension -eq '.apk' } | Remove-Item -Force -ErrorAction SilentlyContinue }
  #   ★ 注意：-ChildItem -Include 不加 -Recurse 是不生效的 ✗ 必须用 Where-Object 筛 ✓
New-Item -ItemType Directory -Force -Path $hand | Out-Null
Copy-Item $updateZip $hand -Force
Copy-Item $fullZip   $hand -Force



""
"OK. Both packages share the top-level folder '$PKG', so the update extracts straight"
"over an existing install and leaves $PC\$MODEL\ untouched."
