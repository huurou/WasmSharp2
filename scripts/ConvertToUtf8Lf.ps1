# -----------------------------------------------------------------------------
# 設定：動作を変更する場合は、このブロック内の変数だけを編集してください。
# -----------------------------------------------------------------------------

# 名前がいずれかのパターンに一致するフォルダーは、配下を含めて変換しません。
$ExcludedDirectoryNamePatterns = @(
    ".agents"
    ".codex"
    ".git"
    ".kiro"
    ".vs"
    "bin"
    "dist"
    "docs"
    "node_modules"
    "obj"
    "TestResults"
)

# ファイル名がいずれかのパターンに一致するファイルだけを変換します。
$TargetFileNamePatterns = @(
    "*.cs"
    "*.csproj"
    "*.css"
    "*.slnx"
    "*.ts"
    "*.tsx"
)

# BOMがなく、UTF-8として読み取れないファイルに適用する変換元文字コードです。
$FallbackSourceEncodingName = "shift_jis"

# -----------------------------------------------------------------------------
# ここから下は処理本体です。ユーザーが変更する設定はありません。
# -----------------------------------------------------------------------------

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Test-NameMatchesAnyPattern {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]]$Patterns
    )

    foreach ($pattern in $Patterns) {
        if ($Name -like $pattern) {
            return $true
        }
    }

    return $false
}

function Test-ByteArraysEqual {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [byte[]]$Left,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [byte[]]$Right
    )

    if ($Left.Length -ne $Right.Length) {
        return $false
    }

    for ($index = 0; $index -lt $Left.Length; $index++) {
        if ($Left[$index] -ne $Right[$index]) {
            return $false
        }
    }

    return $true
}

function Test-IsUtf8WithoutBom {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [byte[]]$Bytes,

        [Parameter(Mandatory = $true)]
        [System.Text.Encoding]$StrictUtf8Encoding
    )

    if ($Bytes.Length -ge 3 -and
        $Bytes[0] -eq 0xEF -and
        $Bytes[1] -eq 0xBB -and
        $Bytes[2] -eq 0xBF) {
        return $false
    }

    try {
        [void]$StrictUtf8Encoding.GetString($Bytes)
        return $true
    }
    catch [System.Text.DecoderFallbackException] {
        return $false
    }
}

function ConvertFrom-FileBytes {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [byte[]]$Bytes,

        [Parameter(Mandatory = $true)]
        [System.Text.Encoding]$FallbackEncoding,

        [Parameter(Mandatory = $true)]
        [System.Text.Encoding]$StrictUtf8Encoding
    )

    # BOMがある場合は、設定された変換元文字コードよりBOMを優先します。
    if ($Bytes.Length -ge 4 -and
        $Bytes[0] -eq 0x00 -and
        $Bytes[1] -eq 0x00 -and
        $Bytes[2] -eq 0xFE -and
        $Bytes[3] -eq 0xFF) {
        $encoding = New-Object System.Text.UTF32Encoding($true, $false, $true)
        return $encoding.GetString($Bytes, 4, $Bytes.Length - 4)
    }

    if ($Bytes.Length -ge 4 -and
        $Bytes[0] -eq 0xFF -and
        $Bytes[1] -eq 0xFE -and
        $Bytes[2] -eq 0x00 -and
        $Bytes[3] -eq 0x00) {
        $encoding = New-Object System.Text.UTF32Encoding($false, $false, $true)
        return $encoding.GetString($Bytes, 4, $Bytes.Length - 4)
    }

    if ($Bytes.Length -ge 3 -and
        $Bytes[0] -eq 0xEF -and
        $Bytes[1] -eq 0xBB -and
        $Bytes[2] -eq 0xBF) {
        return $StrictUtf8Encoding.GetString($Bytes, 3, $Bytes.Length - 3)
    }

    if ($Bytes.Length -ge 2 -and
        $Bytes[0] -eq 0xFE -and
        $Bytes[1] -eq 0xFF) {
        $encoding = New-Object System.Text.UnicodeEncoding($true, $false, $true)
        return $encoding.GetString($Bytes, 2, $Bytes.Length - 2)
    }

    if ($Bytes.Length -ge 2 -and
        $Bytes[0] -eq 0xFF -and
        $Bytes[1] -eq 0xFE) {
        $encoding = New-Object System.Text.UnicodeEncoding($false, $false, $true)
        return $encoding.GetString($Bytes, 2, $Bytes.Length - 2)
    }

    try {
        return $StrictUtf8Encoding.GetString($Bytes)
    }
    catch [System.Text.DecoderFallbackException] {
        return $FallbackEncoding.GetString($Bytes)
    }
}

try {
    $targetRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
    $scriptFilePath = [System.IO.Path]::GetFullPath($PSCommandPath)
    $strictUtf8Encoding = New-Object System.Text.UTF8Encoding($false, $true)
    $utf8WithoutBomEncoding = New-Object System.Text.UTF8Encoding($false)
    $fallbackEncoding = [System.Text.Encoding]::GetEncoding(
        $FallbackSourceEncodingName,
        [System.Text.EncoderFallback]::ExceptionFallback,
        [System.Text.DecoderFallback]::ExceptionFallback)

    $directoryStack = New-Object System.Collections.Stack
    $directoryStack.Push((Get-Item -LiteralPath $targetRoot))

    $encodingScannedFileCount = 0
    $encodingConvertedFileCount = 0
    $lineEndingScannedFileCount = 0
    $lineEndingConvertedFileCount = 0
    $changedFileCount = 0
    $unchangedFileCount = 0
    $excludedDirectoryCount = 0
    $failureCount = 0

    Write-Output "Target root: $targetRoot"

    while ($directoryStack.Count -gt 0) {
        $currentDirectory = $directoryStack.Pop()

        try {
            $childDirectories = @(Get-ChildItem -LiteralPath $currentDirectory.FullName -Directory -Force)
            $files = @(Get-ChildItem -LiteralPath $currentDirectory.FullName -File -Force)
        }
        catch {
            $failureCount++
            Write-Warning "Failed to enumerate directory: $($currentDirectory.FullName) ($($_.Exception.Message))"
            continue
        }

        foreach ($childDirectory in $childDirectories) {
            if (Test-NameMatchesAnyPattern -Name $childDirectory.Name -Patterns $ExcludedDirectoryNamePatterns) {
                $excludedDirectoryCount++
                continue
            }

            # ジャンクションやシンボリックリンクによる循環と対象外への移動を防ぎます。
            if (($childDirectory.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                $excludedDirectoryCount++
                continue
            }

            $directoryStack.Push($childDirectory)
        }

        foreach ($file in $files) {
            # 次回もWindows PowerShellで実行できるよう、このスクリプト自身のBOMは維持します。
            if ([System.IO.Path]::GetFullPath($file.FullName) -eq $scriptFilePath) {
                continue
            }

            if (-not (Test-NameMatchesAnyPattern -Name $file.Name -Patterns $TargetFileNamePatterns)) {
                continue
            }

            if (($file.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                continue
            }

            $encodingScannedFileCount++
            $lineEndingScannedFileCount++

            try {
                $originalBytes = [System.IO.File]::ReadAllBytes($file.FullName)
                $requiresEncodingConversion = -not (Test-IsUtf8WithoutBom `
                    -Bytes $originalBytes `
                    -StrictUtf8Encoding $strictUtf8Encoding)
                [string]$text = ConvertFrom-FileBytes `
                    -Bytes $originalBytes `
                    -FallbackEncoding $fallbackEncoding `
                    -StrictUtf8Encoding $strictUtf8Encoding
                $requiresLineEndingConversion = $text.Contains("`r")
                $normalizedText = $text -replace "`r`n|`r|`n", "`n"
                $convertedBytes = $utf8WithoutBomEncoding.GetBytes($normalizedText)

                if (Test-ByteArraysEqual -Left $originalBytes -Right $convertedBytes) {
                    $unchangedFileCount++
                    continue
                }

                [System.IO.File]::WriteAllBytes($file.FullName, $convertedBytes)
                $changedFileCount++
                if ($requiresEncodingConversion) {
                    $encodingConvertedFileCount++
                }
                if ($requiresLineEndingConversion) {
                    $lineEndingConvertedFileCount++
                }
                Write-Output "Converted: $($file.FullName)"
            }
            catch {
                $failureCount++
                Write-Warning "Failed to convert file: $($file.FullName) ($($_.Exception.Message))"
            }
        }
    }

    Write-Output "Encoding scan:"
    Write-Output "  Scanned files: $encodingScannedFileCount"
    Write-Output "  Converted files: $encodingConvertedFileCount"
    Write-Output "Line-ending scan:"
    Write-Output "  Scanned files: $lineEndingScannedFileCount"
    Write-Output "  Converted files: $lineEndingConvertedFileCount"
    Write-Output "Changed files: $changedFileCount"
    Write-Output "Unchanged files: $unchangedFileCount"
    Write-Output "Excluded directories: $excludedDirectoryCount"

    if ($failureCount -gt 0) {
        throw "Conversion finished with $failureCount failure(s)."
    }
}
catch {
    Write-Error $_
    exit 1
}
