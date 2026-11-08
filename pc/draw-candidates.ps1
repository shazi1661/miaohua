# Draws the kaomoji candidates exactly as the app would render them, so the choice is made
# from the real thing rather than from a description.
#
# ASCII-only PowerShell: the candidate text lives in a UTF-8 file and is read with an
# explicit encoding, which is the only reliable way to get non-ASCII through on this setup.

$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName System.Drawing

$dir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$list = Join-Path $dir ([char]0x5019 + [char]0x9009 + [char]0x989C + [char]0x6587 + [char]0x5B57 + '.txt')
$out  = Join-Path $dir 'kaomoji-candidates.png'

$items = @()
foreach ($line in ([IO.File]::ReadAllText($list, [Text.Encoding]::UTF8) -split "`r?`n")) {
    $t = $line.Trim()
    if ($t.Length -gt 0) { $items += $t }
}

$font = New-Object System.Drawing.Font('Microsoft YaHei UI', 16)
$small = New-Object System.Drawing.Font('Microsoft YaHei UI', 10)
$rowH = 40
$w = 620
$h = 40 + $items.Count * $rowH

$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(255, 250, 253))
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

$g.DrawString('candidates - same font and size the app uses', $small,
              [System.Drawing.Brushes]::Gray, 10, 10)

$y = 40
$i = 0
foreach ($t in $items) {
    $i++
    # the row background matches the card the kaomoji would sit on
    $g.FillRectangle((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)), 60, $y, $w - 80, $rowH - 6)
    $g.DrawString(('{0,2}.' -f $i), $small, [System.Drawing.Brushes]::Silver, 14, ($y + 10))
    $g.DrawString($t, $font, (New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(206, 96, 152))), 66, $y + 4)
    $y += $rowH
}

$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose(); $font.Dispose(); $small.Dispose()
"wrote $out  ($($items.Count) candidates)"
