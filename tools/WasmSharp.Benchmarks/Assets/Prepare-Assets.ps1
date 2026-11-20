#requires -Version 7.5
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$WasmToolsPath,
    [Parameter(Mandatory)][string]$ClangPath,
    [Parameter(Mandatory)][string]$WasmLdPath,
    [Parameter(Mandatory)][string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$outputRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
if (Test-Path -LiteralPath $outputRoot) {
    throw "出力先は既に存在します。新しいディレクトリを指定してください: $outputRoot"
}

$wasmTools = (Resolve-Path -LiteralPath $WasmToolsPath).Path
$clang = (Resolve-Path -LiteralPath $ClangPath).Path
$wasmLd = (Resolve-Path -LiteralPath $WasmLdPath).Path
$profilePath = Join-Path $repositoryRoot 'tools/WasmSharp.TestSuiteRunner/Corpus/core2-profile.json'
$profile = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
$featureArguments = @('--features', 'wasm2')
New-Item -ItemType Directory -Path $outputRoot | Out-Null
$commands = [Collections.Generic.List[object]]::new()

function Invoke-RecordedTool([string]$Name, [string]$Executable, [string[]]$ToolArguments) {
    $logPath = Join-Path $outputRoot "$Name.log"
    $lines = @(& $Executable @ToolArguments 2>&1)
    $exitCode = $LASTEXITCODE
    Set-Content -LiteralPath $logPath -Value ($lines -join [Environment]::NewLine) -Encoding utf8
    $commands.Add([ordered]@{ name = $Name; executable = $Executable; arguments = $ToolArguments; exit_code = $exitCode; log = $logPath })
    $commands.ToArray() | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $outputRoot 'commands.json') -Encoding utf8
    if ($exitCode -ne 0) { throw "${Name}が終了値${exitCode}で失敗しました: $logPath" }
    return $lines -join "`n"
}

function Write-LargeSource([string]$largeSource) {
$largeWat = [Text.StringBuilder]::new()
[void]$largeWat.Append(@'
(module
  (import "bench" "add_one" (func $add_one (param i32) (result i32)))
  (memory (export "memory") 1 1)
  (table (export "functions") 4 4 funcref)
  (global $seed (export "seed") i32 (i32.const 41))
  (global $start_result (export "start_result") (mut i32) (i32.const -1))
'@.Replace("`r`n", "`n"))
[void]$largeWat.Append("`n")
# importは含めず、255個の加算関数とstartで256定義関数にする。
for ($index = 0; $index -lt 255; $index++) {
    $functionName = 'f{0:D3}' -f $index
    $export = if ($index -in @(0, 127, 254)) { ' (export "{0}")' -f $functionName } else { '' }
    [void]$largeWat.Append(('  (func ${0}{1} (param i32) (result i32) local.get 0 i32.const {2} i32.add)' -f $functionName, $export, $index) + "`n")
}
[void]$largeWat.Append(@'
  (func $start global.get $seed call $add_one global.set $start_result)
  (start $start)
  (elem (i32.const 0) $f000 $f127 $f254)
'@.Replace("`r`n", "`n"))
[void]$largeWat.Append("`n")
# 0〜255の全byteを256回並べる。入力書込は不要で、data初期化もInstantiateの仕事量になる。
$dataBlock = (0..255 | ForEach-Object { '\{0:x2}' -f $_ }) -join ''
[void]$largeWat.Append('  (data (i32.const 0) "' + ($dataBlock * 256) + '")' + "`n)`n")
[IO.File]::WriteAllText($largeSource, $largeWat.ToString(), [Text.UTF8Encoding]::new($false))
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-ManifestFile([string]$Path) {
    return [ordered]@{
        relative_path = [IO.Path]::GetRelativePath($outputRoot, $Path).Replace('\', '/')
        sha256 = Get-Sha256 $Path
    }
}

function Get-ToolRecord([string]$Name, [string]$Path, [string]$Pattern, [string]$Revision) {
    $version = Invoke-RecordedTool "$Name-version" $Path @('--version')
    if ($version -cnotmatch $Pattern) { throw "${Name}の版が固定版と一致しません: $version" }
    return [ordered]@{
        name = $Name
        version = $version
        revision = $Revision
        executable = Get-ManifestFile $Path
        build_configuration = 'prebuilt executable'
    }
}

$wasmToolsPin = $profile.wasm_tools
$wasmToolsVersion = [regex]::Escape($wasmToolsPin.version)
$wasmToolsCommit = $wasmToolsPin.commit.Substring(0, 9)
$wasmToolsRecord = Get-ToolRecord 'wasm-tools' $wasmTools "^wasm-tools $wasmToolsVersion \($wasmToolsCommit " $wasmToolsPin.commit
$clangRecord = Get-ToolRecord 'Clang' $clang '(?m)^clang version 21\.1\.8(?:\s|$)' 'llvmorg-21.1.8'
$lldRecord = Get-ToolRecord 'LLD' $wasmLd '^LLD 21\.1\.8(?:\s|$)' 'llvmorg-21.1.8'

function Get-AssetSource([string]$Path) {
    $file = Get-ManifestFile $Path
    return [ordered]@{
        relative_path = $file.relative_path
        # HEADは基点だけを示し、未コミット・未追跡を含む使用内容はhashで識別する。
        revision = "base:${baseRevision};sha256:$($file.sha256)"
        sha256 = $file.sha256
    }
}

function Invoke-AssetStep([string]$Name, $Tool, [string[]]$ToolArguments, $Steps) {
    $result = Invoke-RecordedTool $Name ([IO.Path]::GetFullPath($Tool.executable.relative_path, $outputRoot)) $ToolArguments
    $json = [Text.Json.JsonSerializer]::Serialize([object]$ToolArguments, [string[]], [Text.Json.JsonSerializerOptions]::new())
    $argumentHash = [Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($json)))
    $Steps.Add([ordered]@{
        tool = $Tool
        arguments = $ToolArguments
        arguments_sha256 = $argumentHash
    })
    return $result
}

$baseRevision = & git -C $repositoryRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'sourceの基点revisionを取得できません。' }
$sourceRoot = Join-Path $outputRoot 'sources'
$binaryRoot = Join-Path $outputRoot 'wasm'
New-Item -ItemType Directory -Path $sourceRoot, $binaryRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'sources.json') -Destination (Join-Path $outputRoot 'source-definitions.json')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'NOTICE.md') -Destination (Join-Path $outputRoot 'NOTICE.md')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'cases.json') -Destination (Join-Path $outputRoot 'cases.json')
Copy-Item -LiteralPath $profilePath -Destination (Join-Path $outputRoot 'core2-profile.json')
Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $outputRoot 'Prepare-Assets.ps1')
$definitions = Get-Content -LiteralPath (Join-Path $outputRoot 'source-definitions.json') -Raw | ConvertFrom-Json
$cases = @(Get-Content -LiteralPath (Join-Path $outputRoot 'cases.json') -Raw | ConvertFrom-Json)
if ($definitions.assets.Count -ne 6 -or $cases.Count -ne 15 -or @($cases.case_id | Sort-Object -Unique).Count -ne 15) {
    throw '固定集合は6素材・重複のない15ケースである必要があります。'
}
if (@(Compare-Object @($definitions.assets.asset_id | Sort-Object -Unique) @($cases.asset_id | Sort-Object -Unique)).Count -ne 0) {
    throw 'source定義とケースの素材IDが一致しません。'
}
foreach ($asset in $definitions.assets) {
    if ($asset.kind -ne 'generated_wat') {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $asset.source) -Destination (Join-Path $outputRoot $asset.source)
    }
}
Write-LargeSource (Join-Path $sourceRoot 'preparation-large.wat')

$assets = [Collections.Generic.List[object]]::new()
$generated = [Collections.Generic.List[object]]::new()
Push-Location $outputRoot
try {
    foreach ($definition in $definitions.assets) {
        $id = $definition.asset_id
        $binary = "wasm/$id.wasm"
        $steps = [Collections.Generic.List[object]]::new()
        if ($definition.kind -eq 'c') {
            $null = Invoke-AssetStep "$id-clang" $clangRecord (@($definitions.kernel.compile_arguments) + @($definition.source, '-o', 'kernel.o')) $steps
            $null = Invoke-AssetStep "$id-link" $lldRecord (@($definitions.kernel.link_arguments) + @('kernel.o', '-o', $binary)) $steps
        }
        else {
            $null = Invoke-AssetStep "$id-parse" $wasmToolsRecord @('parse', $definition.source, '-o', $binary) $steps
            # parseがWATの識別子から生成するnameセクションを除き、従来と同じ測定バイト列に揃える。
            $null = Invoke-AssetStep "$id-strip-names" $wasmToolsRecord @('strip', '--delete', '^name$', $binary, '-o', $binary) $steps
        }
        $null = Invoke-AssetStep "$id-validate" $wasmToolsRecord (@('validate') + $featureArguments + @($binary)) $steps
        $sections = Invoke-AssetStep "$id-imports" $wasmToolsRecord @('objdump', $binary) $steps
        $importCount = if ($sections -match '(?m)^\s*imports\s*\|[^\r\n]*\|\s*(\d+) count\s*$') { [int]$Matches[1] } else { 0 }
        if ($importCount -ne $definition.import_count) {
            throw "import数が固定値と一致しません: $id ($importCount)"
        }
        $binaryFile = Get-ManifestFile (Join-Path $outputRoot $binary)
        $assetCases = @($cases | Where-Object asset_id -eq $id)
        foreach ($case in $assetCases) {
            if ($case.case_revision -lt 1 -or $case.asset_sha256 -cne $binaryFile.sha256) {
                throw "採用binary hashが一致しません。ケース版と固定採用値を確認してください: $($case.case_id)"
            }
        }
        $sources = @(
            (Get-AssetSource (Join-Path $outputRoot $definition.source)),
            (Get-AssetSource (Join-Path $outputRoot 'source-definitions.json')),
            (Get-AssetSource (Join-Path $outputRoot 'Prepare-Assets.ps1'))
        )
        $assets.Add([ordered]@{ asset_id = $id; binary = $binaryFile; sources = $sources; generation_steps = $steps.ToArray() })
        $generated.Add([ordered]@{
            asset_id = $id; binary = $binaryFile; import_count = $importCount
            cases = @($assetCases | ForEach-Object { [ordered]@{
                case_id = $_.case_id; case_revision = $_.case_revision
                category = $definition.case_categories.($_.case_id)
                measurement_stage = $_.measurement_stage
            } })
        })
    }
}
finally { Pop-Location }

$manifest = [ordered]@{
    schema_version = 1
    profile = Get-ManifestFile (Join-Path $outputRoot 'core2-profile.json')
    cases = Get-ManifestFile (Join-Path $outputRoot 'cases.json')
    assets = $assets.ToArray()
}
# 全生成・Core2検査・固定採用hash照合に成功した場合だけ、成功集合のmanifestを保存する。
$manifestPath = Join-Path $outputRoot 'asset-manifest.json'
$manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
$record = [ordered]@{
    scope = 'runtime-performance-fixed-assets'
    created_at = [DateTimeOffset]::UtcNow.ToString('O')
    base_revision = $baseRevision
    source_identity = '基点revisionは使用内容そのものを示さない。コピーした各ファイルのSHA-256で未コミット変更・未追跡sourceを含む内容を識別する。'
    asset_manifest = Get-ManifestFile $manifestPath
    origins = $definitions
    source_definitions = Get-AssetSource (Join-Path $outputRoot 'source-definitions.json')
    notice = Get-ManifestFile (Join-Path $outputRoot 'NOTICE.md')
    script = Get-AssetSource (Join-Path $outputRoot 'Prepare-Assets.ps1')
    powershell = @{ version = $PSVersionTable.PSVersion.ToString(); executable = Get-ManifestFile (Join-Path $PSHOME 'pwsh.exe') }
    generation_arguments = [ordered]@{
        wasm_tools_path = $wasmToolsRecord.executable.relative_path
        clang_path = $clangRecord.executable.relative_path
        wasm_ld_path = $lldRecord.executable.relative_path
        output_directory = '.'
    }
    profile = @{ file = $manifest.profile; id = $profile.id; arguments = $featureArguments }
    cases = $manifest.cases
    wasm_tools = $wasmToolsPin
    generated = $generated.ToArray()
    preparation_large = @{
        defined_function_count = 256
        function_shape = 'f000..f254: (i32)->i32、引数+関数番号。start: ()->()'
        imports = @('bench.add_one: (i32)->i32、引数+1、副作用なし')
        memory = @{ pages = 1; maximum_pages = 1; data_offset = 0; data_bytes = 65536; data_pattern = 'i mod256' }
        table = @{ length = 4; maximum_elements = 4; elements = @('f000', 'f127', 'f254', $null) }
        globals = @{ seed = 41; start_result_before = -1; start_result_after = 42 }
        start = '自instanceのseedをbench.add_oneへ渡し、自instanceのstart_resultへ設定する'
    }
}
$record | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $outputRoot 'sources.json') -Encoding utf8NoBOM
Write-Output "6素材・15ケースの生成、固定Core2検査、採用hash照合が完了しました: $outputRoot"
