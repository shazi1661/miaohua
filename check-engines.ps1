# 比较【安卓引擎 Rules.java】和【电脑版引擎 Miaohua.cs】的核心行为是否一致。
#
# 为什么不能直接比输出：
#   语气风格每次会【随机挑】一个前缀、一个颜文字、一个后缀 —— 同一个引擎连跑两次，
#   57 个用例里也有 27 个不一样。直接 diff 两个引擎的输出，看到的全是随机性。
#
# 所以这里的做法是：
#   把两个引擎所有"样式池"里的短语（726 条）从输出里全部删掉，只留下确定性的核心
#   —— 词替换、颜文字插入、重复装饰清理。这一部分必须逐字一致，也正是"电脑版修了、
#   忘了搬到安卓"那类 bug 会出现的地方。
#
# 用法：  .\check-engines.ps1
# 输出：  屏幕摘要 + android\build\engine-compare.txt

$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$proj     = Split-Path -Parent $MyInvocation.MyCommand.Path
$sdk      = 'D:\harness gzq\009\android-sdk'
$platform = Join-Path $sdk 'platforms\android-34\android.jar'
$jdk      = 'D:\PCL ' + [char]0x6B63 + [char]0x5F0F + [char]0x7248 + ' 1.0'   # 占位，下面会用真实路径覆盖

# JDK 路径里带空格和中文，写死在这里（和 build-apk.ps1 保持一致）
$jdk = 'D:\PCL ' + [char]0x6B63 + [char]0x5F0F + [char]0x7248 + ' 2.13.1.1\.minecraft\versions\StarValley_beta1.0.2'

if (-not (Test-Path "$jdk\bin\java.exe")) { throw "找不到 JDK: $jdk" }
if (-not (Test-Path $platform))           { throw "找不到 android.jar: $platform" }

# 引擎的编译产物放在 android\build\ 里，而 build-apk.ps1 每次都会清空那个目录
# （所以"先跑过 check-engine.ps1"这个前提随时会失效）。这里自己把前置跑掉。
$engineOut = Join-Path $proj 'android\build\enginecheck'
if (-not (Test-Path (Join-Path $engineOut 'RulesCheck.class'))) {
    "=== 0. 前置：编译引擎到桌面 JVM ==="
    & (Join-Path $proj 'android\check-engine.ps1') | Select-Object -Last 3
}
if (-not (Test-Path (Join-Path $engineOut 'RulesCheck.class'))) { throw "引擎没能编译到 $engineOut" }

"=== 1. 用电脑版规则跑安卓引擎 ==="
$pcRules = Join-Path $proj 'pc\rules.txt'
$cases   = Join-Path $proj 'pc\selftest-in.txt'
$android = Join-Path $proj 'android\build\engine-compare-android.txt'
& "$jdk\bin\java.exe" -classpath "$engineOut;$platform" RulesCheck $pcRules $android $cases | Out-Null
if ($LASTEXITCODE -ne 0) { throw '安卓引擎跑失败' }
"  写出 $android"

"=== 2. 用同一份规则、同一批用例跑电脑版 --trace ==="
#
# 这里【不能用 --selftest】。自检输出里有很多个章节，每个章节的规则和目的都不同
# （比如幂等性章节会把结果再跑一遍），从里面按 IN 取第一次出现的 OUT，拿到的是别的
# 章节的结果 —— 之前就是这么比出 5 条不存在的"差异"的。
#
# --trace 用的是真实的 rules.txt、真实的代码路径，而且带固定随机种子（new Random(7)），
# 每次跑结果都一样。它的 RESULT 行就是可靠的基准。
Get-Process -Name Miaohua -ErrorAction SilentlyContinue | ForEach-Object { try { $_.Kill() } catch {} }
Start-Sleep -Milliseconds 800
$pcCases = @(Get-Content $cases -Encoding UTF8 | Where-Object { $_.Trim().Length -gt 0 })
& (Join-Path $proj 'pc\Miaohua.exe') --trace @pcCases
Start-Sleep -Seconds 3
Get-Process -Name Miaohua -ErrorAction SilentlyContinue | ForEach-Object { try { $_.Kill() } catch {} }
$traceFile = Join-Path $proj '..\界面试验\trace-pc.txt'
if (-not (Test-Path $traceFile)) { throw "电脑版 --trace 没写出 $traceFile" }

# 从 trace 里取 (输入, RESULT) 对
$P = @{}
$curIn = $null
foreach ($l in (Get-Content $traceFile -Encoding UTF8)) {
    if ($l -match '^\s*000 input \| (.*)$') { $curIn = $Matches[1]; continue }
    if ($l -match '^\s*RESULT \| (.*)$' -and $null -ne $curIn) { $P[$curIn] = $Matches[1]; $curIn = $null }
}
"  电脑版基准 $($P.Count) 条（来自 --trace）"

"=== 3. 收集两边的样式池 ==="
$pool = New-Object System.Collections.Generic.HashSet[string]
$files = @()
$files += Get-ChildItem (Join-Path $proj 'android\assets') -Filter '*.txt' -File |
          Where-Object { $_.Name -ne 'changelog.txt' }
$files += Get-ChildItem (Join-Path $proj 'pc') -Filter 'style-*.txt' -File

# ★ 当前生效的 rules.txt 里的【颜文字 / 后缀】也必须收进剔除池 ✗
#   否则它们没被剔除 ✗ 两个引擎各随机挑一个 ✗ 就会显示成"同一输入结果不同"✓
#   （2026-10-15 修：之前只收 assets 和 style-* ✗ 于是随 rules.txt 的颜文字
#     不同 ✗ 一致数会在 25~31 之间乱跳 ✓）
#   只取这两类 ✗ 不要把 => 右边的替换词也加进来 ✗ 那会过度剔除 ✓
$rl = Join-Path $proj 'pc\rules.txt'
if (Test-Path $rl) {
    foreach ($l in (Get-Content $rl -Encoding UTF8)) {
        if ($l -match '^\s*(颜文字|后缀)\s*=') {
            $v = ($l -split '=', 2)[1].Trim()
            if ($v.Length -ge 2) { [void]$pool.Add($v) }
        }
    }
}

foreach ($f in $files) {
    foreach ($l in (Get-Content $f.FullName -Encoding UTF8)) {
        $s = $l.Trim()
        if ($s.Length -lt 2) { continue }
        if ($s.StartsWith('#') -or $s.StartsWith('//')) { continue }
        if ($s -match '=') { $s = ($s -split '=', 2)[1].Trim() }
        foreach ($piece in ($s -split '\s{2,}|\t+')) {
            $q = $piece.Trim()
            if ($q.Length -ge 2) { [void]$pool.Add($q) }
        }
    }
}
"  样式池短语 $($pool.Count) 条"

function Strip([string]$s) {
    $r = $s
    # 从长到短删，否则短串会先吃掉长串的一部分
    foreach ($p in ($pool | Sort-Object Length -Descending)) { $r = $r.Replace($p, '') }
    return ($r -replace '\s+', '')
}

function Pairs([string]$file) {
    $h = @{}
    $cur = $null
    foreach ($l in (Get-Content $file -Encoding UTF8 -ErrorAction SilentlyContinue)) {
        $m = [regex]::Match($l, '^\s*IN\s*:\s*(.*)$')
        if ($m.Success) { $cur = $m.Groups[1].Value.Trim(); continue }
        $m2 = [regex]::Match($l, '^\s*OUT\s*:\s*(.*)$')
        if ($m2.Success -and $null -ne $cur) {
            if (-not $h.ContainsKey($cur)) { $h[$cur] = $m2.Groups[1].Value.Trim() }
            $cur = $null
        }
    }
    return $h
}

"=== 4. 去掉随机装饰后逐条比对 ==="
$A = Pairs $android
$report = New-Object System.Collections.Generic.List[string]
$report.Add("android cases : $($A.Count)")
$report.Add("pc cases      : $($P.Count)")
$same = 0; $diff = 0
foreach ($k in $A.Keys) {
    if (-not $P.ContainsKey($k)) { continue }
    $sa = Strip $A[$k]
    $sp = Strip $P[$k]
    if ($sa -eq $sp) { $same++ }
    else {
        $diff++
        $report.Add("")
        $report.Add("IN      : $k")
        $report.Add("android : $sa")
        $report.Add("pc      : $sp")
    }
}
$report.Insert(2, "core same     : $same")
$report.Insert(3, "core differs  : $diff")
$outFile = Join-Path $proj 'android\build\engine-compare.txt'
[IO.File]::WriteAllText($outFile, ($report -join "`r`n"), (New-Object Text.UTF8Encoding($false)))

""
"  核心一致 : $same"
"  核心不同 : $diff"
"  明细     : $outFile"
if ($diff -gt 0) { exit 1 }
