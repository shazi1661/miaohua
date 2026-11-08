# Builds Miaohua.exe with the in-box .NET Framework compiler.
#
# /codepage:65001 is REQUIRED: Miaohua.cs is UTF-8 without a BOM and contains
# Chinese string literals. Without it, csc decodes the file as GBK, the Chinese
# turns to mojibake, and mangled quotes can even break the parse.

$ErrorActionPreference = 'Continue'

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' }

$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = @(Join-Path $dir 'Miaohua.cs'), @(Join-Path $dir 'Ui.cs'), @(Join-Path $dir 'Lang.cs')
$ico = Join-Path $dir 'Miaohua.ico'
$exe = Join-Path $dir 'Miaohua.exe'

# a running copy locks the file; Stop-Process -Force has proven unreliable here
Get-Process -Name Miaohua -ErrorAction SilentlyContinue | ForEach-Object {
    try { $_.Kill() } catch { }
}
Start-Sleep -Milliseconds 700

if (-not (Test-Path $ico)) { throw "missing $ico - run gen-icon.py first" }

& $csc /nologo /codepage:65001 /target:winexe /out:"$exe" /win32icon:"$ico" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationClient.dll" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationTypes.dll" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" $src 2>&1 |
    ForEach-Object { "  csc: $_" }

if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) { throw 'compile failed' }
"built: $exe  ($((Get-Item $exe).Length) bytes)"
