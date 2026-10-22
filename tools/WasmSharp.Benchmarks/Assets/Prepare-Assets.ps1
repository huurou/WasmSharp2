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

$generated = @(foreach ($asset in @(@{ source = $watPath; path = $watOutput }, @{ source = $cPath; path = $cOutput })) {
    $name = [IO.Path]::GetFileNameWithoutExtension($asset.path)
    $validation = Invoke-RecordedTool "$name-validate" $wasmValidate (@($asset.path, '--verbose') + $featureArguments)
    # 固定WABTのverbose出力を利用し、独自のWasmバイナリ解析を追加しない。
    if ($validation -notmatch 'EndModule' -or $validation -match 'OnImport\(') {
        throw "検査完了またはimportなしを確認できません: $($asset.path)"
    }
    [ordered]@{
        source = $asset.source
        source_sha256 = (Get-FileHash -LiteralPath $asset.source -Algorithm SHA256).Hash
        path = $asset.path
        sha256 = (Get-FileHash -LiteralPath $asset.path -Algorithm SHA256).Hash
        import_count = 0
    }
})

# 成功時にだけ記録する。これは最小変換の確認用で、測定素材のmanifestではない。
$record = [ordered]@{
    scope = 'toolchain-smoke'
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
}
$record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputRoot 'sources.json') -Encoding utf8
Write-Output "固定ツールとCore2 profileによる最小WAT/Cの検査が完了しました: $outputRoot"
