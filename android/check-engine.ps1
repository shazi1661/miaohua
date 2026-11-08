# Rebuilds the APK, then runs the Android transform engine on a desktop JVM to
# verify it behaves the same as the PC engine (same rules file format, same
# idempotency guarantees). Writes ..\android\build\enginecheck-out.txt

$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$proj     = 'D:\harness gzq\009\miaohua\android'
$sdk      = 'D:\harness gzq\009\android-sdk'
$platform = Join-Path $sdk 'platforms\android-34\android.jar'
$jdk      = 'D:\PCL ' + [char]0x6B63 + [char]0x5F0F + [char]0x7248 + ' 2.13.1.1\.minecraft\versions\StarValley_beta1.0.2'

"=== 1. rebuild APK ==="
& (Join-Path $proj '..\build-apk.ps1') | Select-Object -Last 4

"=== 2. compile engine for a desktop JVM ==="
$out = Join-Path $proj 'build\enginecheck'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force -Path $out | Out-Null

$srcs  = @(Join-Path $proj 'java\com\miaohua\helper\Rules.java')
$srcs += @(Get-ChildItem -Path (Join-Path $proj 'build\gen') -Recurse -Filter *.java | Select-Object -ExpandProperty FullName)
$srcs += @(Join-Path $proj 'tools\RulesCheck.java')
"compiling $($srcs.Count) file(s)"

& "$jdk\bin\javac.exe" -encoding UTF-8 -nowarn -classpath $platform -d $out $srcs 2> "$out\javac.err"
if ($LASTEXITCODE -ne 0) {
    Get-Content "$out\javac.err" -ErrorAction SilentlyContinue | Select-Object -First 15
    throw 'javac failed'
}

"=== 3. run the engine on the Android default rules ==="
$rules = Join-Path $proj 'res\raw\default_rules.txt'
$res   = Join-Path $proj 'build\enginecheck-out.txt'
# the same case file the desktop --selftest uses, so both engines are compared on
# identical input
$cases = Join-Path $proj '..\pc\selftest-in.txt'
& "$jdk\bin\java.exe" -classpath "$out;$platform" RulesCheck $rules $res $cases
if ($LASTEXITCODE -ne 0) { throw 'engine check failed' }
"wrote $res"
