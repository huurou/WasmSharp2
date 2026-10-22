#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Wat2WasmPath,
    [Parameter(Mandatory)][string]$WasmValidatePath,
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

$wat2wasm = (Resolve-Path -LiteralPath $Wat2WasmPath).Path
$wasmValidate = (Resolve-Path -LiteralPath $WasmValidatePath).Path
$clang = (Resolve-Path -LiteralPath $ClangPath).Path
$wasmLd = (Resolve-Path -LiteralPath $WasmLdPath).Path
$profilePath = Join-Path $repositoryRoot 'tools/WasmSharp.TestSuiteRunner/Corpus/core2-profile.json'
$profile = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
$wabtSource = Join-Path $repositoryRoot 'thirdParties/wabt'
$wabtCommit = & git -C $wabtSource rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $wabtCommit -ne $profile.wabt.commit) {
    throw 'WABTのcheckoutが既存profileの固定commitと一致しません。'
}
& git -C $wabtSource diff --quiet HEAD --
if ($LASTEXITCODE -ne 0) { throw 'WABTの固定sourceに変更があります。' }

# 両実行ファイルとCMakeCacheを同じReleaseビルドへ対応付ける。
$releaseDirectory = Split-Path $wat2wasm
if ((Split-Path $releaseDirectory -Leaf) -ne 'Release' -or (Split-Path $wasmValidate) -ne $releaseDirectory) {
    throw 'WABTの2実行ファイルは同じビルド先のReleaseディレクトリを指定してください。'
}
$cachePath = Join-Path (Split-Path $releaseDirectory) 'CMakeCache.txt'
$cache = @{}
foreach ($line in Get-Content -LiteralPath $cachePath) {
    if ($line -match '^([^#/:][^:]*):[^=]+=(.*)$') { $cache[$Matches[1]] = $Matches[2] }
}
$requiredBuildOptions = @{ BUILD_TESTS = 'OFF'; BUILD_LIBWASM = 'OFF'; WITH_WASI = 'OFF'; USE_INTERNAL_SHA256 = 'ON'; WERROR = 'ON' }
foreach ($key in $requiredBuildOptions.Keys) {
    if ($cache[$key] -ne $requiredBuildOptions[$key]) { throw "WABTのビルド条件が異なります: $key" }
}
if ([IO.Path]::GetFullPath($cache.CMAKE_HOME_DIRECTORY) -ne $wabtSource -or $cache.CMAKE_GENERATOR_PLATFORM -ne 'x64') {
    throw 'WABTのビルド元またはarchitectureが異なります。'
}

# profileの既定値から変更が必要なfeatureだけを引数にする。
$featureArguments = @(foreach ($feature in $profile.features) {
    if ($feature.enabled -ne $feature.default_enabled) {
        if ($feature.enabled) { "--enable-$($feature.name)" } else { "--disable-$($feature.name)" }
    }
})
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

$toolRecords = @(
    @{ name = 'wat2wasm'; path = $wat2wasm; pattern = ('^1\.0\.42 .*g' + $wabtCommit.Substring(0, 7) + '[0-9a-f]*\)$') },
    @{ name = 'wasm-validate'; path = $wasmValidate; pattern = ('^1\.0\.42 .*g' + $wabtCommit.Substring(0, 7) + '[0-9a-f]*\)$') },
    @{ name = 'Clang'; path = $clang; pattern = '(?m)^clang version 21\.1\.8(?:\s|$)' },
    @{ name = 'LLD'; path = $wasmLd; pattern = '^LLD 21\.1\.8(?:\s|$)' }
)
foreach ($tool in $toolRecords) {
    $version = Invoke-RecordedTool "$($tool.name)-version" $tool.path @('--version')
    if ($version -notmatch $tool.pattern) { throw "$($tool.name)の版が固定版と一致しません: $version" }
    $tool.version = $version
    $tool.sha256 = (Get-FileHash -LiteralPath $tool.path -Algorithm SHA256).Hash
    $tool.Remove('pattern')
}
Copy-Item -LiteralPath $cachePath -Destination (Join-Path $outputRoot 'WABT-CMakeCache.txt')

$watPath = Join-Path $outputRoot 'smoke.wat'
$cPath = Join-Path $outputRoot 'smoke.c'
$watOutput = Join-Path $outputRoot 'smoke-wat.wasm'
$cObject = Join-Path $outputRoot 'smoke-c.o'
$cOutput = Join-Path $outputRoot 'smoke-c.wasm'
'(module (func (export "add") (param i32 i32) (result i32) local.get 0 local.get 1 i32.add))' |
    Set-Content -LiteralPath $watPath -Encoding utf8NoBOM
'unsigned int add(unsigned int a, unsigned int b) { return a + b; }' |
    Set-Content -LiteralPath $cPath -Encoding utf8NoBOM
$null = Invoke-RecordedTool 'wat2wasm' $wat2wasm (@($watPath) + $featureArguments + @('-o', $watOutput))
$clangArguments = @(
    '--target=wasm32-unknown-unknown', '-O2', '-ffreestanding', '-nostdlib',
    '-fno-vectorize', '-fno-slp-vectorize', '-Werror', '-c', $cPath, '-o', $cObject
)
$null = Invoke-RecordedTool 'clang' $clang $clangArguments
$linkArguments = @('--no-entry', '--export=add', '--unresolved-symbols=report-all', $cObject, '-o', $cOutput)
$null = Invoke-RecordedTool 'wasm-ld' $wasmLd $linkArguments

$assets = @(@{ source = $watPath; path = $watOutput; import_count = 0 }, @{ source = $cPath; path = $cOutput; import_count = 0 })
$largeSource = Join-Path $outputRoot 'preparation-large.wat'
$largeOutput = Join-Path $outputRoot 'preparation-large.wasm'
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
$null = Invoke-RecordedTool 'preparation-large-wat2wasm' $wat2wasm (@($largeSource) + $featureArguments + @('-o', $largeOutput))
$assets += @{ source = $largeSource; path = $largeOutput; import_count = 1 }

$generated = @(foreach ($asset in $assets) {
    $name = [IO.Path]::GetFileNameWithoutExtension($asset.path)
    $validation = Invoke-RecordedTool "$name-validate" $wasmValidate (@($asset.path, '--verbose') + $featureArguments)
    # 固定WABTのverbose出力を利用し、独自のWasmバイナリ解析を追加しない。
    $importCount = [regex]::Matches($validation, 'OnImport\(').Count
    if ($validation -notmatch 'EndModule' -or $importCount -ne $asset.import_count) {
        throw "検査完了または固定import数を確認できません: $($asset.path)"
    }
    [ordered]@{
        source = $asset.source
        source_sha256 = (Get-FileHash -LiteralPath $asset.source -Algorithm SHA256).Hash
        path = $asset.path
        sha256 = (Get-FileHash -LiteralPath $asset.path -Algorithm SHA256).Hash
        import_count = $importCount
    }
})

# 成功時にだけ記録する。全測定素材のmanifestへの統合は別工程で行う。
$record = [ordered]@{
    scope = 'toolchain-smoke-and-preparation-large'
    created_at = [DateTimeOffset]::UtcNow.ToString('O')
    script = @{ path = $PSCommandPath; sha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash }
    profile = @{
        path = $profilePath
        id = $profile.id
        sha256 = (Get-FileHash -LiteralPath $profilePath -Algorithm SHA256).Hash
        enabled_count = @($profile.features | Where-Object enabled).Count
        disabled_count = @($profile.features | Where-Object { -not $_.enabled }).Count
        arguments = $featureArguments
    }
    wabt = @{
        commit = $wabtCommit
        configuration = 'Release'
        generator = $cache.CMAKE_GENERATOR
        platform = $cache.CMAKE_GENERATOR_PLATFORM
        options = $requiredBuildOptions
        cmake_cache_sha256 = (Get-FileHash -LiteralPath $cachePath -Algorithm SHA256).Hash
    }
    tools = $toolRecords
    generated = $generated
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
$record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputRoot 'sources.json') -Encoding utf8
Write-Output "固定ツールとCore2 profileによるsmokeとpreparation-largeの検査が完了しました: $outputRoot"
