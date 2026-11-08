# Keeps only the newest N packages in the build folders and deletes the rest.
#
# Written after finding 13 stale 2 GB releases stacked up in release-.../: every packaging
# round removed the file it was ABOUT to write and never the older ones, so the folder grew
# by ~2 GB per round. 19.7 GB had accumulated.
#
# ASCII only on purpose - a .ps1 containing Chinese needs a UTF-8 BOM or PowerShell reads it
# as ANSI and the Chinese turns to mojibake (which has bitten this project four times).
#
#   pwsh -File clean-packages.ps1          keep 3
#   pwsh -File clean-packages.ps1 -KeepCount 5  keep 5
#   pwsh -File clean-packages.ps1 -WhatIf  list only, delete nothing

param(
    [int]$KeepCount = 3,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$root  = 'D:\harness gzq\009'
$dirs  = @(
    (Join-Path $root 'release-zi-dai-mo-xing-placeholder'),   # replaced below
    (Join-Path $root 'miaohua'),
    (Join-Path $root 'miaohua\android')
)

# find the build folders by pattern so a renamed folder is still covered
$dirs = @(
    (Get-ChildItem $root -Directory | Where-Object { $_.Name -like 'release-*' } | Select-Object -ExpandProperty FullName)
    (Join-Path $root 'miaohua')
    $root
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique

$freed = 0
$removed = 0

foreach ($d in $dirs) {
    $zips = @(Get-ChildItem -Path $d -Filter '*.zip' -File -ErrorAction SilentlyContinue |
              Sort-Object LastWriteTime -Descending)
    if ($zips.Count -le $KeepCount) {
        "  {0}  ({1} zip, nothing to do)" -f $d, $zips.Count
        continue
    }

    $keepList = @($zips | Select-Object -First $KeepCount)
    $killList = @($zips | Select-Object -Skip $KeepCount)
    "  {0}" -f $d
    foreach ($k in $keepList) { "      keep   {0}" -f $k.Name }
    foreach ($k in $killList) {
        # never delete anything that is not a plain file sitting directly in this folder
        if ($k.PSIsContainer -or $k.DirectoryName -ne $d) {
            "      SKIP   {0}  (not a plain file in this folder)" -f $k.FullName
            continue
        }
        if (-not $k.Name.EndsWith('.zip')) {
            "      SKIP   {0}  (not a .zip)" -f $k.Name
            continue
        }
        "      delete {0}  ({1} MB)" -f $k.Name, [math]::Round($k.Length / 1MB, 0)
        if (-not $WhatIf) { Remove-Item $k.FullName -Force -ErrorAction SilentlyContinue }
        $freed += $k.Length
        $removed++
    }
}

""
if ($WhatIf) {
    "would free {0:N2} GB across {1} file(s)" -f ($freed / 1GB), $removed
} else {
    "freed {0:N2} GB, removed {1} file(s)" -f ($freed / 1GB), $removed
}
