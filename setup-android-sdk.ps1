$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$sdkRoot = 'D:\harness gzq\009\android-sdk'
$jdkHome = 'D:\PCL ' + [char]0x6B63 + [char]0x5F0F + [char]0x7248 + ' 2.13.1.1\.minecraft\versions\StarValley_beta1.0.2'

"=== disk space on D: ==="
Get-PSDrive D | Select-Object Used, Free | Format-List

New-Item -ItemType Directory -Force -Path $sdkRoot | Out-Null
$dl = Join-Path $sdkRoot '_download'
New-Item -ItemType Directory -Force -Path $dl | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $sdkRoot 'cmdline-tools') | Out-Null

$zip = Join-Path $dl 'cmdline-tools.zip'
if (-not (Test-Path $zip)) {
    "=== downloading cmdline-tools ==="
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    Invoke-WebRequest -Uri 'https://dl.google.com/android/repository/commandlinetools-win-11076708_latest.zip' -OutFile $zip -UseBasicParsing
    "downloaded in $($sw.Elapsed.TotalSeconds) s"
}
"zip size: $((Get-Item $zip).Length) bytes"

$tmp = Join-Path $dl 'extract'
if (Test-Path $tmp) { Remove-Item -Recurse -Force $tmp }
"=== extracting ==="
Expand-Archive -Path $zip -DestinationPath $tmp -Force

$latest = Join-Path $sdkRoot 'cmdline-tools\latest'
if (Test-Path $latest) { Remove-Item -Recurse -Force $latest }
Move-Item -Path (Join-Path $tmp 'cmdline-tools') -Destination $latest
"cmdline-tools -> $latest"

$env:JAVA_HOME = $jdkHome
$env:PATH = (Join-Path $jdkHome 'bin') + ';' + $env:PATH
$sdkmanager = Join-Path $latest 'bin\sdkmanager.bat'
"JAVA_HOME = $env:JAVA_HOME"
"java on PATH: $((Get-Command java -ErrorAction SilentlyContinue).Source)"

"=== sdkmanager version ==="
& $sdkmanager --version

"=== installing packages (this is the big download) ==="
$pkgs = @('platform-tools', 'platforms;android-34', 'build-tools;34.0.0')
$answers = ("y`n" * 20)
$answers | & $sdkmanager --sdk_root=$sdkRoot @pkgs

"=== installed components ==="
& $sdkmanager --sdk_root=$sdkRoot --list_installed
"=== DONE ==="
