# 读 MuMu 里喵化助手的运行记录（记录现在会落盘，不用截图）
$adb = 'D:\harness gzq\009\android-sdk\platform-tools\adb.exe'
$dev = '127.0.0.1:16384'
& $adb -s $dev root 2>&1 | Out-Null
Start-Sleep -Seconds 2
$tr = & $adb -s $dev shell "cat /data/data/com.miaohua.helper/files/trace.txt 2>/dev/null || echo NOFILE" 2>&1 | Out-String
if ($tr -match 'NOFILE') { Write-Host "还没有记录（去别的应用里打几个字再来）" -ForegroundColor Yellow }
else { $tr -split "`n" | Where-Object { $_.Trim().Length -gt 0 } | Select-Object -Last 40 | ForEach-Object { Write-Host $_ } }