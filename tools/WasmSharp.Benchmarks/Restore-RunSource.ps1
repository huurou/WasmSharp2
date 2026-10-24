#requires -Version 7.5
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RunDirectory,
    [Parameter(Mandatory)][string]$DestinationDirectory,
    [string]$RepositoryDirectory = (Join-Path $PSScriptRoot '../..')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath $RepositoryDirectory).Path
$runRoot = (Resolve-Path -LiteralPath $RunDirectory).Path
$destinationRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($DestinationDirectory)
if (Test-Path -LiteralPath $destinationRoot) { throw "復元先は既に存在します: $destinationRoot" }
$relativeDestination = [IO.Path]::GetRelativePath($repositoryRoot, $destinationRoot)
if (-not [IO.Path]::IsPathRooted($relativeDestination) -and
    $relativeDestination -ne '..' -and
    -not $relativeDestination.StartsWith('..' + [IO.Path]::DirectorySeparatorChar)) {
    throw '復元先は元リポジトリ外の新規ディレクトリを指定してください。'
}

function ContainedPath([string]$Root, [string]$RelativePath) {
    $path = [IO.Path]::GetFullPath((Join-Path $Root $RelativePath))
    $relative = [IO.Path]::GetRelativePath($Root, $path)
    if ([IO.Path]::IsPathRooted($RelativePath) -or [IO.Path]::IsPathRooted($relative) -or
        $relative -eq '..' -or $relative.StartsWith('..' + [IO.Path]::DirectorySeparatorChar) -or
        $relative.Split([IO.Path]::DirectorySeparatorChar) -contains '.git') {
        throw "復元用の相対pathが範囲外です: $RelativePath"
    }
    return $path
}

function VerifiedFile([string]$Root, $Reference) {
    $path = ContainedPath $Root $Reference.relative_path
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $Reference.sha256) {
        throw "保存ファイルのhashが一致しません: $path"
    }
    return $path
}

function VerifySourceSet($Set, [string]$Root) {
    $paths = [string[]]@($Set.files | ForEach-Object { $_.relative_path })
    [Array]::Sort($paths, [StringComparer]::Ordinal)
    $files = [Collections.Generic.List[Collections.Generic.Dictionary[string, string]]]::new()
    foreach ($path in $paths) {
        $reference = $Set.files | Where-Object relative_path -CEQ $path
        if ($Root) { [void](VerifiedFile $Root $reference) }
        $file = [Collections.Generic.Dictionary[string, string]]::new()
        $file.Add('relative_path', $path)
        $file.Add('sha256', $reference.sha256)
        $files.Add($file)
    }
    # SourceCapture.Setと同じ順序・プロパティ名・System.Text.JsonのUTF-8形式で集合hashを作る。
    $bytes = [Text.Json.JsonSerializer]::SerializeToUtf8Bytes($files, $files.GetType(), [Text.Json.JsonSerializerOptions]::Default)
    if ([Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData($bytes)) -cne $Set.sha256) {
        throw 'source集合hashが保存内容と一致しません。'
    }
}

function Git([string]$Directory, [string[]]$Arguments, [switch]$AllowMissingConfig) {
    $start = [Diagnostics.ProcessStartInfo]::new('git')
    $start.WorkingDirectory = $Directory
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    $output = $process.StandardOutput.ReadToEnd()
    $errorText = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0 -and -not ($AllowMissingConfig -and $process.ExitCode -eq 1)) {
        throw "git $($Arguments -join ' ') が失敗しました: $errorText"
    }
    Write-Verbose $errorText
    return $output.TrimEnd("`r", "`n")
}

function CloneRevision([string]$Original, [string]$Target, [string]$Revision) {
    $autocrlf = Git $Original @('config', '--get', 'core.autocrlf') -AllowMissingConfig
    if (-not $autocrlf) { $autocrlf = 'false' }
    [void](Git $repositoryRoot @('clone', '--no-hardlinks', '--no-checkout', '--config', "core.autocrlf=$autocrlf", '--', $Original, $Target))
    [void](Git $Target @('checkout', '--detach', $Revision))
    [void](Git $Target @('submodule', 'init'))
}

$manifest = Get-Content -LiteralPath (Join-Path $runRoot 'run.json') -Raw | ConvertFrom-Json
if ($manifest.schema_version -ne 1) { throw '未対応のrun schema_versionです。' }
$sourcePath = VerifiedFile $runRoot $manifest.source_file
$source = Get-Content -LiteralPath $sourcePath -Raw | ConvertFrom-Json
if ($source.changes.Count -ne 0) { throw '測定途中で使用内容が変わったrunは復元基準にできません。' }
if (@($source.submodules | Where-Object is_dirty).Count -ne 0) { throw '未コミット変更のあるsubmoduleは復元できません。' }
$indexDiff = VerifiedFile $runRoot $source.index_diff
$worktreeDiff = VerifiedFile $runRoot $source.worktree_diff
foreach ($file in $source.untracked_files) { [void](VerifiedFile $runRoot $file) }
VerifySourceSet $source.runtime ''
VerifySourceSet $source.harness ''

# 元のworktree/index/branchには触れず、objectも共有しない独立cloneだけを変更する。
CloneRevision $repositoryRoot $destinationRoot $source.head
if ((Get-Item -LiteralPath $indexDiff).Length -ne 0) {
    [void](Git $destinationRoot @('apply', '--index', '--binary', '--whitespace=nowarn', '--', $indexDiff))
}
if ((Get-Item -LiteralPath $worktreeDiff).Length -ne 0) {
    [void](Git $destinationRoot @('apply', '--binary', '--whitespace=nowarn', '--', $worktreeDiff))
}
foreach ($file in $source.untracked_files) {
    $target = ContainedPath $destinationRoot $file.relative_path
    New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
    Copy-Item -LiteralPath (VerifiedFile $runRoot $file) -Destination $target
}
foreach ($submodule in $source.submodules) {
    $original = ContainedPath $repositoryRoot $submodule.relative_path
    $target = ContainedPath $destinationRoot $submodule.relative_path
    # 未初期化submoduleは保存されたgitlinkを使う。使用ファイルの欠落は最後のhash照合で拒否する。
    if (Test-Path -LiteralPath (Join-Path $original '.git')) {
        CloneRevision $original $target $submodule.revision
    }
}
VerifySourceSet $source.runtime $destinationRoot
VerifySourceSet $source.harness $destinationRoot
Write-Output "復元完了: $destinationRoot"
Write-Output "HEAD=$($source.head) runtime=$($source.runtime.sha256) harness=$($source.harness.sha256)"
