# 'Continue', not 'Stop': native tools (javac/d8/aapt2) write harmless notes to
# stderr, and with 'Stop' PowerShell 5.1 turns any stderr record into a fatal
# error. Every step below checks $LASTEXITCODE explicitly instead.
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$proj     = 'D:\harness gzq\009\miaohua\android'
$sdk      = 'D:\harness gzq\009\android-sdk'
$bt       = Join-Path $sdk 'build-tools\34.0.0'
$platform = Join-Path $sdk 'platforms\android-34\android.jar'
$jdk      = 'D:\PCL ' + [char]0x6B63 + [char]0x5F0F + [char]0x7248 + ' 2.13.1.1\.minecraft\versions\StarValley_beta1.0.2'

$env:JAVA_HOME = $jdk
$env:PATH = (Join-Path $jdk 'bin') + ';' + $env:PATH

$out = Join-Path $proj 'build'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force -Path "$out\gen"     | Out-Null
New-Item -ItemType Directory -Force -Path "$out\classes" | Out-Null
New-Item -ItemType Directory -Force -Path "$out\dex"     | Out-Null

"=== 1. aapt2 compile ==="
# The 更新公告 page reads the same 更新记录.txt the desktop package ships, copied in here so
# there is one source of truth. It has to be an ASCII file name: aapt2 stores a nested or
# non-ASCII asset path with the OS separator and AssetManager then cannot open it.
# ★ 2026-10-22：安卓公告页也只装精简版 ✗ 完整记录 362KB 太长了 ✓
$changelog = Join-Path (Split-Path -Parent $proj) '更新公告.txt'
if (-not (Test-Path $changelog)) { $changelog = Join-Path (Split-Path -Parent $proj) '更新记录.txt' }
if (Test-Path $changelog) {
    Copy-Item $changelog (Join-Path $proj 'assets\changelog.txt') -Force
    "changelog copied into assets"
}
& "$bt\aapt2.exe" compile --dir "$proj\res" -o "$out\res.zip"
if ($LASTEXITCODE -ne 0) { throw 'aapt2 compile failed' }
"res.zip: $([math]::Round((Get-Item "$out\res.zip").Length/1KB,1)) KB"

"=== 2. aapt2 link ==="
$aaptArgs = @(
    'link',
    '-o', "$out\base.apk",
    '-I', $platform,
    '--manifest', "$proj\AndroidManifest.xml",
    '--java', "$out\gen",
    '--min-sdk-version', '26',
    '--target-sdk-version', '34'
)
# -A ships the assets folder (the 语气风格 word lists live in assets/styles/*.txt).
# Forward slashes here on purpose: aapt2 copies the path into the archive verbatim, so a
# Windows-style "$proj\assets" produced entries named "assets/styles\tsundere.txt". Android's
# AssetManager asks for "styles/tsundere.txt", finds nothing, and every tone button reported
# 找不到风格文件. Passing the path with '/' avoids the problem at the source.
if (Test-Path "$proj\assets") {
    $aaptAssets = "$proj\assets" -replace '\\', '/'
    $aaptArgs += @('-A', $aaptAssets)
}
$aaptArgs += "$out\res.zip"
& "$bt\aapt2.exe" @aaptArgs
if ($LASTEXITCODE -ne 0) { throw 'aapt2 link failed' }
"base.apk: $([math]::Round((Get-Item "$out\base.apk").Length/1KB,1)) KB"

"=== 3. javac ==="
$srcs = @(Get-ChildItem -Path "$proj\java" -Recurse -Filter *.java | Select-Object -ExpandProperty FullName)
$srcs += @(Get-ChildItem -Path "$out\gen"  -Recurse -Filter *.java | Select-Object -ExpandProperty FullName)
"compiling $($srcs.Count) java files"
& "$jdk\bin\javac.exe" -encoding UTF-8 -source 8 -target 8 -nowarn -classpath $platform -d "$out\classes" $srcs 2> "$out\javac.err"
if ($LASTEXITCODE -ne 0) {
    Get-Content "$out\javac.err" -ErrorAction SilentlyContinue | Select-Object -First 20
    throw 'javac failed'
}

"=== 4. jar ==="
& "$jdk\bin\jar.exe" cf "$out\classes.jar" -C "$out\classes" .
if ($LASTEXITCODE -ne 0) { throw 'jar failed' }

"=== 5. d8 (dex) ==="
& "$bt\d8.bat" --lib $platform --min-api 26 --output "$out\dex" "$out\classes.jar"
if ($LASTEXITCODE -ne 0) { throw 'd8 failed' }

"=== 6. rebuild the apk: classes.dex in, asset separators fixed ==="
Add-Type -AssemblyName System.IO.Compression.FileSystem

# classes.dex is ADDED to the apk aapt2 produced, in Update mode, so every existing entry
# keeps its original storage. Do NOT be tempted to rebuild the archive to rename entries:
# .NET's ZipArchive cannot write a STORED entry at all (CompressionLevel.NoCompression means
# "deflate, level 0", method 8, not method 0). Rebuilding therefore compressed resources.arsc,
# and since Android 11 that file must be STORED and 4-byte aligned - the installer rejected
# the whole APK with -124 while the aapt2-built v2.2 was fine. The separator problem that
# prompted the rebuild is fixed at the source instead, by passing -A with forward slashes.
Copy-Item "$out\base.apk" "$out\unsigned.apk" -Force
$zip = [System.IO.Compression.ZipFile]::Open("$out\unsigned.apk", 'Update')
try {
    $dex = Join-Path $out 'dex\classes.dex'
    [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $zip, $dex, 'classes.dex', [IO.Compression.CompressionLevel]::Optimal)
} finally { $zip.Dispose() }
"unsigned.apk      : $([math]::Round((Get-Item "$out\unsigned.apk").Length/1KB,1)) KB"

# Guard the two properties that decide whether the file installs at all. Both were broken
# silently before: the assets came out with backslashes, and resources.arsc came out
# deflated. Parsing the central directory reads the real compression method, which
# ZipArchiveEntry does not expose.
function Test-ApkLayout($path) {
    $b = [IO.File]::ReadAllBytes($path)
    $i = 0; $bad = @(); $arscStored = $false; $backslash = 0
    while ($i -lt $b.Length - 4) {
        if ($b[$i] -eq 0x50 -and $b[$i+1] -eq 0x4B -and $b[$i+2] -eq 0x01 -and $b[$i+3] -eq 0x02) {
            $method  = [BitConverter]::ToUInt16($b, $i + 10)
            $nameLen = [BitConverter]::ToUInt16($b, $i + 28)
            $extraLen= [BitConverter]::ToUInt16($b, $i + 30)
            $cmtLen  = [BitConverter]::ToUInt16($b, $i + 32)
            $name = [Text.Encoding]::UTF8.GetString($b, $i + 46, $nameLen)
            if ($name.Contains('\')) { $backslash++ }
            if ($name -eq 'resources.arsc') { $arscStored = ($method -eq 0) }
            $i += 46 + $nameLen + $extraLen + $cmtLen
        } else { $i++ }
    }
    if (-not $arscStored) { $bad += 'resources.arsc is not STORED (installer will reject it)' }
    if ($backslash -gt 0) { $bad += "$backslash entry name(s) use a backslash (AssetManager cannot find them)" }
    return $bad
}

"=== 7. zipalign ==="
& "$bt\zipalign.exe" -f 4 "$out\unsigned.apk" "$out\aligned.apk"
if ($LASTEXITCODE -ne 0) { throw 'zipalign failed' }

"=== 8. keystore ==="
$ks = Join-Path $proj 'miaohua.keystore'
if (-not (Test-Path $ks)) {
    & "$jdk\bin\keytool.exe" -genkeypair -keystore $ks -alias miaohua `
        -keyalg RSA -keysize 2048 -validity 10000 `
        -storepass miaohua123 -keypass miaohua123 `
        -dname "CN=Miaohua Helper, OU=Local, O=Local, L=Local, ST=Local, C=CN"
    if ($LASTEXITCODE -ne 0) { throw 'keytool failed' }
    "keystore created"
} else { "keystore reused" }

"=== 9. apksigner ==="
$final = Join-Path $proj 'Miaohua.apk'
if (Test-Path $final) { Remove-Item $final -Force }
& "$bt\apksigner.bat" sign --ks $ks --ks-pass pass:miaohua123 --key-pass pass:miaohua123 --out $final "$out\aligned.apk"
if ($LASTEXITCODE -ne 0) { throw 'apksigner failed' }

"=== 10. verify ==="
& "$bt\apksigner.bat" verify --print-certs $final
if ($LASTEXITCODE -ne 0) { throw 'apksigner verify failed' }

# Fail the build rather than ship an APK that cannot install. Both checks below correspond
# to a bug that was found only after a real phone refused the file.
$layoutProblems = Test-ApkLayout $final
if ($layoutProblems.Count -gt 0) {
    foreach ($p in $layoutProblems) { "APK LAYOUT PROBLEM: $p" }
    throw 'apk layout check failed'
}
"layout            : resources.arsc STORED, no backslash entries"

""
"APK READY : $final"
"size      : $([math]::Round((Get-Item $final).Length/1KB,1)) KB"
