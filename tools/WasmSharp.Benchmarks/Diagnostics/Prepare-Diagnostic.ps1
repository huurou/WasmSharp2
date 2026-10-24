#requires -Version 7.5
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RunDirectory,
    [Parameter(Mandatory)][string]$DestinationDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$AssetManifestPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$run = (Resolve-Path -LiteralPath $RunDirectory).Path
$assets = (Resolve-Path -LiteralPath $AssetManifestPath).Path
$output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw '準備の出力先は新規pathを指定してください。' }
New-Item -ItemType Directory -Path $output | Out-Null
$commands = [Collections.Generic.List[object]]::new()
function Run([string]$Name, [string]$Executable, [string[]]$Arguments, [string]$Directory) {
    $log = Join-Path $output ($Name + '.log')
    Push-Location $Directory
    try {
        & $Executable @Arguments *> $log
        $code = $LASTEXITCODE
    } finally { Pop-Location }
    $commands.Add([ordered]@{ name = $Name; executable = $Executable; arguments = $Arguments; cwd = $Directory; exit_code = $code })
    $commands | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $output 'commands.json') -Encoding utf8
    if ($code -ne 0) { throw "${Name}が終了${code}となりました: $log" }
}
function Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function SourceSet($Set, [string]$Root) {
    $paths = [string[]]@($Set.files.relative_path)
    [Array]::Sort($paths, [StringComparer]::Ordinal)
    $files = [Collections.Generic.List[Collections.Generic.Dictionary[string, string]]]::new()
    foreach ($path in $paths) {
        $file = [Collections.Generic.Dictionary[string, string]]::new()
        $file.Add('relative_path', $path)
        $file.Add('sha256', (Hash (Join-Path $Root $path)))
        $files.Add($file)
    }
    $bytes = [Text.Json.JsonSerializer]::SerializeToUtf8Bytes($files, $files.GetType(), [Text.Json.JsonSerializerOptions]::Default)
    [ordered]@{ sha256 = [Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData($bytes)); files = $files }
}

$manifest = Get-Content (Join-Path $run 'run.json') -Raw | ConvertFrom-Json
$saved = Get-Content (Join-Path $run $manifest.source_file.relative_path) -Raw | ConvertFrom-Json
$environment = Get-Content (Join-Path $run $manifest.environment_file.relative_path) -Raw | ConvertFrom-Json
$dotnet = (Get-Command dotnet).Source
if ((& $dotnet --version) -cne $environment.sdk_version) { throw '採用runのSDK完全版と異なります。' }
& (Join-Path $PSScriptRoot '../Restore-RunSource.ps1') -RunDirectory $run -RepositoryDirectory $repository -DestinationDirectory $DestinationDirectory *> (Join-Path $output 'restore.log')
$commands.Add([ordered]@{ name = 'restore'; executable = (Join-Path $PSScriptRoot '../Restore-RunSource.ps1'); arguments = @('-RunDirectory', $run, '-RepositoryDirectory', $repository, '-DestinationDirectory', $DestinationDirectory); cwd = (Get-Location).Path; exit_code = 0 })
$destination = (Resolve-Path -LiteralPath $DestinationDirectory).Path
Copy-Item (Join-Path $PSScriptRoot 'Workload.cs.template') (Join-Path $destination 'tools/WasmSharp.Benchmarks/Program.cs')
$source = [ordered]@{
    baseline_head = $saved.head
    baseline_runtime_sha256 = $saved.runtime.sha256
    baseline_harness_sha256 = $saved.harness.sha256
    runtime = SourceSet $saved.runtime $destination
    harness = SourceSet $saved.harness $destination
}
if ($source.runtime.sha256 -cne $saved.runtime.sha256) { throw '診断コピーのruntimeが基準値と異なります。' }
$source | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $output 'source-final.json') -Encoding utf8
& git -C $destination diff --binary -- tools/WasmSharp.Benchmarks/Program.cs | Set-Content (Join-Path $output 'diagnostic-program.diff') -Encoding utf8

$toolRoot = Join-Path $output 'tools/trace'
Run 'install-trace' $dotnet @('tool', 'install', 'dotnet-trace', '--version', '10.0.750501', '--tool-path', $toolRoot, '--arch', 'x64') $repository
$trace = Join-Path $toolRoot 'dotnet-trace.exe'
Run 'trace-version' $trace @('--version') $repository
Run 'trace-profiles' $trace @('list-profiles') $repository
Run 'trace-help' $trace @('collect', '--help') $repository
$readerRoot = Join-Path $output 'tools/reader'
New-Item -ItemType Directory -Path $readerRoot | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'Reader.cs.template') (Join-Path $readerRoot 'Program.cs')
Copy-Item (Join-Path $PSScriptRoot 'Reader.csproj.template') (Join-Path $readerRoot 'DiagnosticReader.csproj')
Run 'build-workload' $dotnet @('build', 'tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj', '-c', 'Release', '--warnaserror') $destination
Run 'build-reader' $dotnet @('build', 'DiagnosticReader.csproj', '-c', 'Release', '--warnaserror') $readerRoot
Run 'dotnet-info' $dotnet @('--info') $destination
$workload = Join-Path $destination 'tools/WasmSharp.Benchmarks/bin/Release/net10.0/WasmSharp.Benchmarks.dll'
$reader = Join-Path $readerRoot 'bin/Release/net10.0/DiagnosticReader.dll'
$recipe = Join-Path $output 'recipe'
New-Item -ItemType Directory $recipe | Out-Null
Get-ChildItem $PSScriptRoot -File | ForEach-Object { Copy-Item $_.FullName (Join-Path $recipe $_.Name) }
$files = @($trace, $workload, $reader, $assets, (Join-Path $output 'source-final.json')) + @(Get-ChildItem $toolRoot -Recurse -File | Where-Object Extension -In '.dll', '.exe', '.json', '.nupkg' | ForEach-Object FullName) + @(Get-ChildItem (Split-Path $reader) -File | ForEach-Object FullName) + @(Get-ChildItem (Split-Path $workload) -File | ForEach-Object FullName) + @(Get-ChildItem $readerRoot -File | ForEach-Object FullName) + @(Get-ChildItem $recipe -File | ForEach-Object FullName)
$hashes = @($files | Sort-Object -Unique | ForEach-Object { [ordered]@{ path = $_; sha256 = Hash $_ } })
$preparation = [ordered]@{
    kind = 'Diagnostic'
    created_utc = [DateTime]::UtcNow.ToString('o')
    saved_run = $run
    saved_run_sha256 = Hash (Join-Path $run 'run.json')
    repository = $repository
    diagnostic_copy = $destination
    asset_manifest = $assets
    dotnet = $dotnet
    dotnet_trace = $trace
    dotnet_trace_version = '10.0.750501'
    trace_event_version = '3.1.21'
    workload_assembly = $workload
    reader_assembly = $reader
    sdk_version = $environment.sdk_version
    expected_runtime = $environment.runtime_version
    source_file = (Join-Path $output 'source-final.json')
    fixed_files = $hashes
}
$preparation | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $output 'preparation.json') -Encoding utf8
Write-Output (Join-Path $output 'preparation.json')
