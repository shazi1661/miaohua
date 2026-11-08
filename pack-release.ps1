# Builds the release zip: runtime files only, no sources, no build scripts,
# no tests, no keystore, no log.
#
# Layout inside the archive:
#   喵化助手\先读我.txt
#   喵化助手\使用说明.md
#   喵化助手\电脑版\{Miaohua.exe, rules.txt, logo.png, bg.png}
#   喵化助手\安卓版\Miaohua.apk

$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$proj    = Split-Path -Parent $MyInvocation.MyCommand.Path
$pkgName = '喵化助手'
$stage   = Join-Path $proj 'release'
$root    = Join-Path $stage $pkgName

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $root '电脑版') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $root '安卓版') | Out-Null

Copy-Item (Join-Path $proj 'pc\Miaohua.exe')      (Join-Path $root '电脑版') -Force
Copy-Item (Join-Path $proj 'pc\rules.txt')        (Join-Path $root '电脑版') -Force
Copy-Item (Join-Path $proj 'pc\logo.png')         (Join-Path $root '电脑版') -Force
Copy-Item (Join-Path $proj 'pc\bg.png')           (Join-Path $root '电脑版') -Force
Copy-Item (Join-Path $proj 'android\Miaohua.apk') (Join-Path $root '安卓版') -Force
Copy-Item (Join-Path $proj '使用说明.md')          $root -Force
if (Test-Path (Join-Path $proj '使用说明书.html')) {
    Copy-Item (Join-Path $proj '使用说明书.html')  $root -Force
}
if (Test-Path (Join-Path $proj '更新记录.txt')) {
    Copy-Item (Join-Path $proj '更新记录.txt')     $root -Force
}

# quick start as UTF-8 WITH BOM + CRLF, so any Windows editor opens it correctly
$qs = [IO.File]::ReadAllText((Join-Path $proj 'release-src\先读我.txt'), [Text.Encoding]::UTF8)
$qs = $qs.Replace("`r`n", "`n").Replace("`r", "`n").Replace("`n", "`r`n")
[IO.File]::WriteAllText((Join-Path $root '先读我.txt'), $qs, (New-Object Text.UTF8Encoding($true)))

Add-Type -AssemblyName System.IO.Compression.FileSystem
# Both versions go into the name, and both are read from the artefacts - never written
# here. The PC and the APK version themselves independently: an Android-only change must
# not move the PC number, or "PC 2.3" means nothing when the exe is identical to 2.1.
#
# Start-Process -Wait, NOT "& exe": this is a GUI-subsystem binary, and PowerShell
# launches those without waiting, so "&" returns an empty string instantly.
$verTmp = Join-Path $env:TEMP 'miaohua-ver.txt'
Start-Process -FilePath (Join-Path $proj 'pc\Miaohua.exe') -ArgumentList '--version' `
    -NoNewWindow -Wait -RedirectStandardOutput $verTmp | Out-Null
$pcVer = (Get-Content $verTmp -Raw -ErrorAction SilentlyContinue)
if ($pcVer) { $pcVer = $pcVer.Trim() }
if (-not $pcVer) { $pcVer = '0.0' }
Remove-Item $verTmp -Force -ErrorAction SilentlyContinue

$andVer = ''
$aapt = 'D:\harness gzq\009\android-sdk\build-tools\34.0.0\aapt2.exe'
$apk  = Join-Path $proj 'android\Miaohua.apk'
if ((Test-Path $aapt) -and (Test-Path $apk)) {
    $badging = & $aapt dump badging $apk 2>$null | Select-String "versionName='" | Select-Object -First 1
    if ($badging) {
        $m = [regex]::Match($badging.Line, "versionName='([^']+)'")
        if ($m.Success) { $andVer = $m.Groups[1].Value }
    }
}
if (-not $andVer) { $andVer = '?' }

$zip = Join-Path $proj ('喵化助手_PC' + $pcVer + '_安卓' + $andVer + '_' + (Get-Date -Format 'yyyyMMdd') + '.zip')
if (Test-Path $zip) { Remove-Item $zip -Force }

# IMPORTANT: the source directory must be $root, NOT $stage. Passing $stage puts
# an extra "release" level at the top of the archive.
# CreateFromDirectory needs .NET 4.5+ for the encoding overload; PS 5.1 has it.
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $root, $zip,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $true,
    [System.Text.Encoding]::UTF8)

Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue

"packed: $(Split-Path $zip -Leaf)  ($([math]::Round((Get-Item $zip).Length / 1KB, 1)) KB)"
$z = [System.IO.Compression.ZipFile]::OpenRead($zip)
$z.Entries | Sort-Object FullName | ForEach-Object { "   {0,-46} {1,9:N0} B" -f $_.FullName, $_.Length }
$z.Dispose()
