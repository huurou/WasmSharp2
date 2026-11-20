#requires -Version 7.5
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$restoreScript = Join-Path $PSScriptRoot '../Restore-RunSource.ps1'
$gitExecutable = (Get-Command git -CommandType Application -TotalCount 1).Source
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('wasmsharp-source-test-' + [guid]::NewGuid().ToString('N'))
$repository = Join-Path $testRoot 'repository'
$run = Join-Path $testRoot 'run'
New-Item -ItemType Directory -Path $repository, $run | Out-Null

function Git([string[]]$Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new('git')
    $start.WorkingDirectory = $repository
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    $output = $process.StandardOutput.ReadToEnd()
    $errorText = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw $errorText }
    return $output
}

function FileReference([string]$Root, [string]$RelativePath) {
    return [ordered]@{
        relative_path = $RelativePath
        sha256 = (Get-FileHash -LiteralPath (Join-Path $Root $RelativePath) -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

function SourceSet([string[]]$Paths) {
    $files = [Collections.Generic.List[Collections.Generic.Dictionary[string, string]]]::new()
    foreach ($path in ($Paths | Sort-Object -CaseSensitive)) {
        $reference = FileReference $repository $path
        $file = [Collections.Generic.Dictionary[string, string]]::new()
        $file.Add('relative_path', $reference.relative_path)
        $file.Add('sha256', $reference.sha256)
        $files.Add($file)
    }
    $bytes = [Text.Json.JsonSerializer]::SerializeToUtf8Bytes($files, $files.GetType(), [Text.Json.JsonSerializerOptions]::Default)
    return [ordered]@{ revision = $head; sha256 = [Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData($bytes)); files = $files }
}

function ExpectFailure([scriptblock]$Action, [string]$Expected) {
    try { & $Action } catch {
        if ($_.Exception.Message -notlike "*$Expected*") { throw }
        return
    }
    throw "期待した失敗がありません: $Expected"
}

function SaveSource {
    $source | ConvertTo-Json -Depth 20 -Compress | Set-Content -LiteralPath (Join-Path $run 'source.json') -Encoding utf8NoBOM
    @{ schema_version = 1; source_file = (FileReference $run 'source.json') } | ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath (Join-Path $run 'run.json') -Encoding utf8NoBOM
}

# Gitの書込みは、この検証で作った隔離fixtureだけに行う。
[void](Git @('init', '--quiet'))
[void](Git @('config', 'core.autocrlf', 'false'))
$commit = @('-c', 'user.name=fixture', '-c', 'user.email=fixture@example.invalid', 'commit', '--quiet')
$spec = Join-Path $testRoot 'spec'
$legacySpecPath = 'thirdParties/WebAssembly-spec'
$currentSpecPath = 'thirdParties/WebAssembly-spec-core2'
$appendix = 'document/core/appendix/index-instructions.py'
$katex = Join-Path $testRoot 'katex'
$katexPath = 'document/core/util/katex'
[void](Git @('init', '--quiet', $katex))
'saved KaTeX' | Set-Content -LiteralPath (Join-Path $katex 'README.md') -Encoding utf8NoBOM
[void](Git @('-C', $katex, 'add', 'README.md'))
[void](Git (@('-C', $katex) + $commit + @('-m', '保存時のKaTeX')))
$katexRevision = (Git @('-C', $katex, 'rev-parse', 'HEAD')).Trim()
[void](Git @('init', '--quiet', $spec))
[void](Git @('-C', $spec, '-c', 'protocol.file.allow=always', 'submodule', 'add', '--', $katex, $katexPath))
New-Item -ItemType Directory -Path (Split-Path (Join-Path $spec $appendix)) -Force | Out-Null
'saved' | Set-Content -LiteralPath (Join-Path $spec $appendix) -Encoding utf8NoBOM
[void](Git @('-C', $spec, 'add', '.'))
[void](Git (@('-C', $spec) + $commit + @('-m', '保存時のCore 2.0仕様')))
$specRevision = (Git @('-C', $spec, 'rev-parse', 'HEAD')).Trim()
[void](Git @('-c', 'protocol.file.allow=always', 'submodule', 'add', '--', $spec, $legacySpecPath))
'before' | Set-Content -LiteralPath (Join-Path $repository 'source.cs') -Encoding utf8NoBOM
'<Project />' | Set-Content -LiteralPath (Join-Path $repository 'harness.csproj') -Encoding utf8NoBOM
[void](Git @('add', '.'))
[void](Git ($commit + @('-m', '復元検証の初期状態')))
$head = (Git @('rev-parse', 'HEAD')).Trim()
[void](Git @('mv', '--', $legacySpecPath, $currentSpecPath))
[void](Git @('-C', $currentSpecPath, '-c', 'protocol.file.allow=always', 'submodule', 'update', '--init', '--', $katexPath))
'current' | Set-Content -LiteralPath (Join-Path $repository "$currentSpecPath/$appendix") -Encoding utf8NoBOM
[void](Git (@('-C', $currentSpecPath) + $commit + @('-am', '現在のCore 2.0仕様')))
[void](Git @('add', '--', $currentSpecPath))
[void](Git ($commit + @('-m', 'Core 2.0仕様のフォルダ名を変更する')))
$headBefore = (Git @('rev-parse', 'HEAD')).Trim()
'index' | Set-Content -LiteralPath (Join-Path $repository 'source.cs') -Encoding utf8NoBOM
[void](Git @('add', 'source.cs'))
[IO.File]::WriteAllText((Join-Path $run 'index.diff'), (Git @('diff', '--cached', '--binary', '--full-index')))
'worktree' | Set-Content -LiteralPath (Join-Path $repository 'source.cs') -Encoding utf8NoBOM
[IO.File]::WriteAllText((Join-Path $run 'worktree.diff'), (Git @('diff', '--binary', '--full-index')))
'untracked' | Set-Content -LiteralPath (Join-Path $repository 'extra.cs') -Encoding utf8NoBOM
Copy-Item -LiteralPath (Join-Path $repository 'extra.cs') -Destination $run
$source = [ordered]@{
    head = $head
    index_diff = FileReference $run 'index.diff'
    worktree_diff = FileReference $run 'worktree.diff'
    untracked_files = @((FileReference $run 'extra.cs'))
    submodules = @(
        @{ relative_path = $legacySpecPath; revision = $specRevision; is_dirty = $false }
        @{ relative_path = "$legacySpecPath/$katexPath"; revision = $katexRevision; is_dirty = $false }
    )
    runtime = SourceSet @('source.cs', 'extra.cs')
    harness = SourceSet @('harness.csproj')
    changes = @()
}
SaveSource
$statusBefore = Git @('status', '--porcelain=v1', '--untracked-files=all')
$indexBefore = (Get-FileHash -LiteralPath (Join-Path $repository '.git/index')).Hash
$destination = Join-Path $testRoot 'restored'
& $restoreScript -RunDirectory $run -RepositoryDirectory $repository -DestinationDirectory $destination
foreach ($file in @($source.runtime.files) + @($source.harness.files)) {
    if ((FileReference $destination $file.relative_path).sha256 -ne $file.sha256) { throw '復元内容のhashが異なります。' }
}
$restoredSpec = Join-Path $destination $legacySpecPath
if ((Get-Content -LiteralPath (Join-Path $restoredSpec $appendix)) -cne 'saved' -or
    (Git @('-C', $restoredSpec, 'rev-parse', 'HEAD')).Trim() -cne $specRevision) { throw '旧フォルダに復元したCore 2.0仕様が保存時と異なります。' }
$restoredKatex = Join-Path $restoredSpec $katexPath
if ((Get-Content -LiteralPath (Join-Path $restoredKatex 'README.md')) -cne 'saved KaTeX' -or
    (Git @('-C', $restoredKatex, 'rev-parse', 'HEAD')).Trim() -cne $katexRevision) { throw '旧フォルダに復元したKaTeXが保存時と異なります。' }
if (((& $gitExecutable -C $destination show ':source.cs') -join "`n").Trim() -ne 'index') { throw '復元先のindexが保存時と異なります。' }
if ((Git @('status', '--porcelain=v1', '--untracked-files=all')) -cne $statusBefore -or
    (Get-FileHash -LiteralPath (Join-Path $repository '.git/index')).Hash -ne $indexBefore -or
    (Git @('rev-parse', 'HEAD')).Trim() -ne $headBefore) { throw '元リポジトリが変更されました。' }
ExpectFailure { & $restoreScript -RunDirectory $run -RepositoryDirectory $repository -DestinationDirectory $destination } '既に存在'
ExpectFailure { & $restoreScript -RunDirectory $run -RepositoryDirectory $repository -DestinationDirectory (Join-Path $repository 'nested') } 'リポジトリ外'
'tampered' | Add-Content -LiteralPath (Join-Path $run 'extra.cs')
ExpectFailure { & $restoreScript -RunDirectory $run -RepositoryDirectory $repository -DestinationDirectory (Join-Path $testRoot 'tampered') } 'hash'
Copy-Item -LiteralPath (Join-Path $repository 'extra.cs') -Destination (Join-Path $run 'extra.cs') -Force
$source.runtime.sha256 = '0' * 64
SaveSource
ExpectFailure { & $restoreScript -RunDirectory $run -RepositoryDirectory $repository -DestinationDirectory (Join-Path $testRoot 'bad-set') } '集合hash'
Write-Output "PASS: 差分・未追跡の復元とhash、旧名でのCore 2.0仕様の復元、元Git状態維持、上書き・元配下・改変拒否。fixture=$testRoot"
