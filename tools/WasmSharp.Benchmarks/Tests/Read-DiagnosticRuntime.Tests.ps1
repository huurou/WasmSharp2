#requires -Version 7.5
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$readScript = Join-Path $PSScriptRoot '../Diagnostics/Read-DiagnosticRuntime.ps1'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('wasmsharp-diagnostic-runtime-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null

function SaveRun([hashtable[]]$Checks) {
    $processes = @()
    for ($i = 0; $i -lt $Checks.Count; $i++) {
        $name = "check-$i.json"
        $path = Join-Path $testRoot $name
        $Checks[$i] | ConvertTo-Json | Set-Content -LiteralPath $path -Encoding utf8
        $processes += @{ check_file = @{ relative_path = $name; sha256 = (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() } }
    }
    @{ runtime_version = '.NET 10.0.13; collector'; sdk_version = '10.0.402' } | ConvertTo-Json |
        Set-Content -LiteralPath (Join-Path $testRoot 'environment.json') -Encoding utf8
    $environmentHash = (Get-FileHash -LiteralPath (Join-Path $testRoot 'environment.json')).Hash.ToLowerInvariant()
    @{ environment_file = @{ relative_path = 'environment.json'; sha256 = $environmentHash }; cases = @(@{ benchmark_report = @{ processes = $processes } }) } |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $testRoot 'run.json') -Encoding utf8
}
function ExpectFailure([string]$Expected) {
    try { & $readScript -RunDirectory $testRoot } catch {
        if ($_.Exception.Message -notlike "*$Expected*") { throw }
        return
    }
    throw "期待した失敗がありません: $Expected"
}

# collectorと子が異なる場合は、子の観測値を選ぶ。
SaveRun @(@{ runtime_version = '.NET 10.0.14; child' }, @{ runtime_version = '.NET 10.0.14; child' })
$actual = & $readScript -RunDirectory $testRoot
if ($actual.version -cne '.NET 10.0.14; child' -or $actual.source -cne 'process_check') { throw '子runtimeを選択できません。' }

# 全件が旧形式の場合だけ、代用元を明示してcollectorへ戻る。
SaveRun @(@{}, @{})
$actual = & $readScript -RunDirectory $testRoot
if ($actual.version -cne '.NET 10.0.13; collector' -or $actual.source -cne 'collector_legacy') { throw '旧形式の代用元が一致しません。' }

SaveRun @(@{}, @{ runtime_version = '.NET 10.0.14; child' })
ExpectFailure '観測が揃っていません'
foreach ($version in @($null, '', 'Unavailable: observation failed')) {
    SaveRun @(@{ runtime_version = $version })
    ExpectFailure 'runtime観測がありません'
}
SaveRun @(@{ runtime_version = '.NET 10.0.13; child' }, @{ runtime_version = '.NET 10.0.14; child' })
ExpectFailure '子process間でruntimeが異なります'

SaveRun @(@{ runtime_version = '.NET 10.0.14; child' })
'{}' | Set-Content -LiteralPath (Join-Path $testRoot 'check-0.json') -Encoding utf8
ExpectFailure '保存時と異なります'
SaveRun @(@{})
'{}' | Set-Content -LiteralPath (Join-Path $testRoot 'environment.json') -Encoding utf8
ExpectFailure '環境記録が保存時と異なります'
Write-Output "PASS: 診断runtimeの選択・旧形式・欠落・混在・検査と環境の保存照合 ($testRoot)"
