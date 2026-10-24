#requires -Version 7.5
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PreparationPath,
    [Parameter(Mandatory)][string]$CaseId,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateRange(1, 300)][int]$Seconds = 5
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$preparationFile = (Resolve-Path -LiteralPath $PreparationPath).Path
$prepared = Get-Content -LiteralPath $preparationFile -Raw | ConvertFrom-Json
$output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw '診断出力は新規pathを指定してください。' }
function Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function VerifyFiles {
    foreach ($file in $prepared.fixed_files) {
        if ((Hash $file.path) -cne $file.sha256) { throw "準備時と内容が異なります: $($file.path)" }
    }
    $source = Get-Content $prepared.source_file -Raw | ConvertFrom-Json
    foreach ($file in @($source.runtime.files) + @($source.harness.files)) {
        if ((Hash (Join-Path $prepared.diagnostic_copy $file.relative_path)) -cne $file.sha256) {
            throw "診断sourceが変わっています: $($file.relative_path)"
        }
    }
}
VerifyFiles
if ((& $prepared.dotnet --version) -cne $prepared.sdk_version) { throw '準備時とSDK完全版が異なります。' }
New-Item -ItemType Directory -Path $output | Out-Null
Copy-Item $preparationFile (Join-Path $output 'preparation.json')
Copy-Item $prepared.source_file (Join-Path $output 'source-final.json')
Get-ChildItem $PSScriptRoot -File | ForEach-Object { Copy-Item $_.FullName (Join-Path $output $_.Name) }
$commands = [Collections.Generic.List[object]]::new()
function Run([string]$Name, [string]$Executable, [string[]]$Arguments) {
    Push-Location $prepared.diagnostic_copy
    try {
        & $Executable @Arguments *> (Join-Path $output ($Name + '.log'))
        $code = $LASTEXITCODE
    } finally { Pop-Location }
    $commands.Add([ordered]@{ name = $Name; executable = $Executable; arguments = $Arguments; cwd = $prepared.diagnostic_copy; exit_code = $code })
    $commands | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $output 'commands.json') -Encoding utf8
    if ($code -ne 0) { throw "${Name}が終了${code}となりました。ログ: $output" }
}

$conditions = [ordered]@{
    kind = 'Diagnostic'
    preparation_sha256 = Hash $preparationFile
    cpu = @(Get-CimInstance Win32_Processor | Select-Object Name, NumberOfLogicalProcessors)
    operating_system = [Runtime.InteropServices.RuntimeInformation]::OSDescription
    power_plan = @(powercfg /getactivescheme)
    # 取得できる場合のみ給電状態を記録する 変更は行わない
    battery = @(Get-CimInstance Win32_Battery | Select-Object BatteryStatus)
    build_configuration = 'Release'
    profile = 'dotnet-sampled-thread-time,gc-verbose'
    buffersize_mb = 256
    buffering_mode = 'Drop'
    warmup_seconds = 1
    requested_seconds = $Seconds
    case_id = $CaseId
    retention = '未実施: 再利用方式は未選定'
}
$conditions | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $output 'conditions.json') -Encoding utf8
$trace = Join-Path $output 'trace.nettrace'
$workload = Join-Path $output 'workload.json'
Run 'collect' $prepared.dotnet_trace @('collect', '--profile', $conditions.profile, '--buffersize', '256', '--buffering-mode', 'Drop', '--format', 'NetTrace', '--output', $trace, '--show-child-io', '--', $prepared.dotnet, 'exec', $prepared.workload_assembly, $prepared.asset_manifest, $CaseId, "$Seconds", $workload)
$result = Get-Content $workload -Raw | ConvertFrom-Json
if (($result.runtime + '; ' + $result.corelib_version) -cne $prepared.expected_runtime) { throw '採用runのruntime完全版と異なります。' }
Run 'read' $prepared.dotnet @('exec', $prepared.reader_assembly, $trace, $workload, (Join-Path $output 'readability.json'))
Run 'top-methods' $prepared.dotnet_trace @('report', $trace, 'topN', '--number', '15', '--inclusive', '--verbose')
VerifyFiles
$files = @(Get-ChildItem $output -File | ForEach-Object { [ordered]@{ path = $_.Name; sha256 = Hash $_.FullName } })
[ordered]@{ kind = 'Diagnostic'; status = 'Succeeded'; case_id = $CaseId; process_id = $result.process_id; files = $files } | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $output 'diagnostic.json') -Encoding utf8
Write-Output (Join-Path $output 'diagnostic.json')
