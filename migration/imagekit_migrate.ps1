param(
    [string]$PictureRoot,
    [string]$OutputDir = ".\migration-output",
    [string]$ImageKitPrivateKey = $env:ImageKitPrivateKey,
    [string]$ImageKitUrlEndpoint = $env:ImageKitUrlEndpoint,
    [string]$ImageKitUploadFolder = $(if ($env:ImageKitUploadFolder) { $env:ImageKitUploadFolder } else { "/3dsgallery" }),
    [int]$TimeoutSeconds = 30,
    [int]$MaxRetries = 3,
    [switch]$DryRun,
    [switch]$Force
)

Set-StrictMode -Version 2
$ErrorActionPreference = "Stop"

function Normalize-Folder([string]$Folder) {
    if ([string]::IsNullOrWhiteSpace($Folder)) {
        return "/3dsgallery"
    }

    $normalized = $Folder.Replace('\\', '/').Trim()
    if (-not $normalized.StartsWith('/')) {
        $normalized = '/' + $normalized
    }

    return $normalized.TrimEnd('/')
}

function Resolve-PictureRootPath([string]$RequestedRoot) {
    if (-not [string]::IsNullOrWhiteSpace($RequestedRoot)) {
        return (Resolve-Path -LiteralPath $RequestedRoot).Path
    }

    $currentPath = (Get-Location).Path
    if ([System.IO.Path]::GetFileName($currentPath).Equals('Picture', [System.StringComparison]::OrdinalIgnoreCase)) {
        return $currentPath
    }

    $childPicture = Join-Path $currentPath 'Picture'
    if (Test-Path -LiteralPath $childPicture -PathType Container) {
        return (Resolve-Path -LiteralPath $childPicture).Path
    }

    throw "Could not find a Picture folder. Run this script from the folder that contains Picture, or pass -PictureRoot explicitly."
}

function Get-ContentType([string]$Extension) {
    switch ($Extension.ToLowerInvariant()) {
        '.mpo' { return 'image/mpo' }
        '.jpg' { return 'image/jpeg' }
        '.jpeg' { return 'image/jpeg' }
        default { return 'application/octet-stream' }
    }
}

function Get-ImageFiles([string]$Root) {
    return Get-ChildItem -LiteralPath $Root -File -Recurse |
        Where-Object { @('.mpo', '.jpg', '.jpeg') -contains $_.Extension.ToLowerInvariant() } |
        Sort-Object FullName
}

function Get-RelativeRemotePath([System.IO.FileInfo]$File, [string]$Root) {
    $relative = $File.FullName.Substring($Root.Length).TrimStart([char[]]([char]92, [char]47))
    $relative = $relative.Replace('\\', '/')
    if ([string]::IsNullOrWhiteSpace($relative)) {
        return 'Picture/' + $File.Name
    }

    return 'Picture/' + $relative
}

function Get-RemoteFolder([string]$RelativeRemotePath, [string]$BaseFolder) {
    $normalizedBase = Normalize-Folder $BaseFolder
    $relativeDirectory = Split-Path -Path $RelativeRemotePath.Replace('\\', '/') -Parent
    if ([string]::IsNullOrWhiteSpace($relativeDirectory)) {
        return $normalizedBase
    }

    return Normalize-Folder ($normalizedBase + '/' + $relativeDirectory)
}

function Get-JournalMap([string]$JournalPath) {
    $map = @{}
    if (-not (Test-Path -LiteralPath $JournalPath)) {
        return $map
    }

    Get-Content -LiteralPath $JournalPath | ForEach-Object {
        if ([string]::IsNullOrWhiteSpace($_)) {
            return
        }

        $entry = $_ | ConvertFrom-Json
        $map[$entry.relativePath] = $entry
    }

    return $map
}

function Append-JournalEntry([string]$JournalPath, [hashtable]$Entry) {
    Add-Content -LiteralPath $JournalPath -Value (($Entry | ConvertTo-Json -Compress))
}

function New-ImageKitClient([string]$PrivateKey, [int]$TimeoutSeconds) {
    Add-Type -AssemblyName System.Net.Http

    $client = New-Object System.Net.Http.HttpClient
    $client.Timeout = [TimeSpan]::FromSeconds($TimeoutSeconds)
    $token = [Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($PrivateKey + ':'))
    $client.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Basic', $token)
    return $client
}

function Get-RetryDelaySeconds($Response, [int]$Attempt) {
    if ($Response -and $Response.Headers -and $Response.Headers.RetryAfter -and $Response.Headers.RetryAfter.Delta) {
        return [Math]::Min([int][Math]::Ceiling($Response.Headers.RetryAfter.Delta.Value.TotalSeconds), 10)
    }

    return [Math]::Min($Attempt * $Attempt, 10)
}

function Invoke-ImageKitUpload($Client, [System.IO.FileInfo]$File, [string]$RelativeRemotePath, [string]$BaseFolder, [int]$MaxRetries) {
    $remoteFolder = Get-RemoteFolder $RelativeRemotePath $BaseFolder
    $contentType = Get-ContentType $File.Extension

    for ($attempt = 1; $attempt -le $MaxRetries; $attempt++) {
        $multipart = New-Object System.Net.Http.MultipartFormDataContent ("----ImageKitBoundary" + [Guid]::NewGuid().ToString('N'))
        try {
            foreach ($field in @{
                fileName = $File.Name
                folder = $remoteFolder
                useUniqueFileName = 'false'
                overwriteFile = 'true'
                overwriteAITags = 'true'
                overwriteTags = 'true'
                overwriteCustomMetadata = 'true'
            }.GetEnumerator()) {
                $multipart.Add((New-Object System.Net.Http.StringContent($field.Value)), $field.Key)
            }

            $fileBytes = [System.IO.File]::ReadAllBytes($File.FullName)
            $fileContent = New-Object System.Net.Http.ByteArrayContent(, $fileBytes)
            $fileContent.Headers.ContentType = New-Object System.Net.Http.Headers.MediaTypeHeaderValue($contentType)
            $multipart.Add($fileContent, 'file', $File.Name)

            $response = $Client.PostAsync('https://upload.imagekit.io/api/v1/files/upload', $multipart).GetAwaiter().GetResult()
            $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            if ($response.IsSuccessStatusCode) {
                return ($responseBody | ConvertFrom-Json)
            }

            if ($attempt -ge $MaxRetries -or (($response.StatusCode.value__ -ne 408) -and ($response.StatusCode.value__ -ne 429) -and ($response.StatusCode.value__ -lt 500))) {
                throw "ImageKit upload failed for '$($File.FullName)': $responseBody"
            }

            Start-Sleep -Seconds (Get-RetryDelaySeconds $response $attempt)
        }
        finally {
            $multipart.Dispose()
        }
    }

    throw "ImageKit upload failed for '$($File.FullName)'."
}

$resolvedPictureRoot = Resolve-PictureRootPath $PictureRoot
if ([string]::IsNullOrWhiteSpace($ImageKitPrivateKey) -and -not $DryRun) {
    throw 'ImageKitPrivateKey is required unless -DryRun is used.'
}
if ([string]::IsNullOrWhiteSpace($ImageKitUrlEndpoint) -and -not $DryRun) {
    throw 'ImageKitUrlEndpoint is required unless -DryRun is used.'
}

$outputPath = if ([System.IO.Path]::IsPathRooted($OutputDir)) {
    [System.IO.Path]::GetFullPath($OutputDir)
} else {
    [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $OutputDir))
}
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$journalPath = Join-Path $outputPath 'journal.jsonl'
$statePath = Join-Path $outputPath 'state.json'
$journalMap = Get-JournalMap $journalPath
$imageFiles = Get-ImageFiles $resolvedPictureRoot
$client = $null
if (-not $DryRun) {
    $client = New-ImageKitClient $ImageKitPrivateKey $TimeoutSeconds
}

$counts = @{
    success = 0
    skipped = 0
    error = 0
    dry_run = 0
}

foreach ($file in $imageFiles) {
    $relativeRemotePath = Get-RelativeRemotePath $file $resolvedPictureRoot
    if (-not $Force -and $journalMap.ContainsKey($relativeRemotePath) -and $journalMap[$relativeRemotePath].status -eq 'success') {
        $counts.skipped++
        continue
    }

    $entry = @{
        relativePath = $relativeRemotePath
        fileName = $file.Name
        size = $file.Length
        timestamp = [DateTime]::UtcNow.ToString('o')
    }

    if ($DryRun) {
        $entry.status = 'dry_run'
        $entry.remoteFolder = Get-RemoteFolder $relativeRemotePath $ImageKitUploadFolder
        $counts.dry_run++
        Append-JournalEntry $journalPath $entry
        $journalMap[$relativeRemotePath] = $entry
        continue
    }

    try {
        $uploaded = Invoke-ImageKitUpload $client $file $relativeRemotePath $ImageKitUploadFolder $MaxRetries
        $entry.status = 'success'
        $entry.fileId = $uploaded.fileId
        $entry.filePath = $uploaded.filePath
        $entry.url = $uploaded.url
        $counts.success++
    }
    catch {
        $entry.status = 'error'
        $entry.message = $_.Exception.Message
        $counts.error++
    }

    Append-JournalEntry $journalPath $entry
    $journalMap[$relativeRemotePath] = $entry
}

@{
    pictureRoot = $resolvedPictureRoot
    uploadFolder = (Normalize-Folder $ImageKitUploadFolder)
    counts = $counts
    completedAt = [DateTime]::UtcNow.ToString('o')
} | ConvertTo-Json | Set-Content -LiteralPath $statePath

Write-Host ('Done. Success={0}, Skipped={1}, DryRun={2}, Error={3}' -f $counts.success, $counts.skipped, $counts.dry_run, $counts.error)
Write-Host ('Journal: ' + $journalPath)
Write-Host ('State:   ' + $statePath)
