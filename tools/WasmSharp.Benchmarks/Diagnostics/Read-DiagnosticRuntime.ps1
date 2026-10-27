#requires -Version 7.5
[CmdletBinding()]
param([Parameter(Mandatory)][string]$RunDirectory)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$run = (Resolve-Path -LiteralPath $RunDirectory).Path
$manifest = Get-Content -LiteralPath (Join-Path $run 'run.json') -Raw | ConvertFrom-Json
$environmentPath = Join-Path $run $manifest.environment_file.relative_path
if ((Get-FileHash -LiteralPath $environmentPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $manifest.environment_file.sha256) {
    throw '診断対象の環境記録が保存時と異なります。'
}
$environment = Get-Content -LiteralPath $environmentPath -Raw | ConvertFrom-Json
$versions = [Collections.Generic.List[string]]::new()
$legacyCount = 0
foreach ($case in $manifest.cases) {
    foreach ($process in $case.benchmark_report.processes) {
        if ($null -eq $process.check_file) { throw '診断対象の子process検査がありません。' }
        $reference = $process.check_file
        $path = Join-Path $run $reference.relative_path
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $reference.sha256) {
            throw '診断対象の子process検査が保存時と異なります。'
        }
        $check = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
        if (!$check.ContainsKey('runtime_version')) {
            $legacyCount++
            continue
        }
        $version = $check.runtime_version
        if ([string]::IsNullOrWhiteSpace($version) -or $version.Contains('Unavailable:', [StringComparison]::Ordinal)) {
            throw '診断対象の子processのruntime観測がありません。'
        }
        $versions.Add($version)
    }
}

if ($versions.Count -eq 0 -and $legacyCount -gt 0) {
    # 旧形式だけはcollectorの版を代用し、準備記録で明示する。
    return [pscustomobject]@{ version = $environment.runtime_version; source = 'collector_legacy'; sdk_version = $environment.sdk_version }
}
if ($legacyCount -gt 0 -or $versions.Count -eq 0) { throw '診断対象の子processのruntime観測が揃っていません。' }
$distinct = @($versions | Sort-Object -Unique -CaseSensitive)
if ($distinct.Count -ne 1) { throw '診断対象の子process間でruntimeが異なります。単一runtimeのrunを指定してください。' }
[pscustomobject]@{ version = $distinct[0]; source = 'process_check'; sdk_version = $environment.sdk_version }
