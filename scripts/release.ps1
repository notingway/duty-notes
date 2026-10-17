<#
.SYNOPSIS
  Publishes a release of Duty Notes to its custom plugin repository (see docs/RELEASING.md).

.DESCRIPTION
  1. checks that everything is ready (clean and pushed, the index repository found, gh signed in, the tag is new);
  2. builds in Release and runs the tests;
  3. checks the release package (latest.zip) and the manifest the build generated;
  4. creates a GitHub release with latest.zip attached;
  5. writes the plugin's entry in the index repository's plogonway.json, from the build's own manifest, then commits and pushes it.

  The release is created BEFORE the index is changed, so the index never points at a download that does not exist.
  Use -DryRun to do steps 1 to 3 and print what the rest would do, changing nothing.

.PARAMETER Notes
  The release notes (also the Changelog shown in the plugin installer).

.PARAMETER Prerelease
  Marks the GitHub release as a pre-release (versioned download links are used either way).

.PARAMETER DryRun
  Build, test and verify, then print the plan and the index entry. Creates no release, writes and pushes nothing.

.PARAMETER IndexRepo
  The folder of the plogonway repository (the custom repository). Default: the plogonway folder next to this repository.
#>
[CmdletBinding()]
param(
    [string]$Notes,
    [switch]$Prerelease,
    [switch]$DryRun,
    [string]$IndexRepo
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $IndexRepo) { $IndexRepo = Join-Path $root '..\plogonway' }   # the folder next to this repository

# ---- tools ---------------------------------------------------------------------------------------
function Find-Tool([string]$name, [string[]]$fallbacks) {
    $found = Get-Command $name -ErrorAction SilentlyContinue
    if ($found) { return $found.Source }
    foreach ($path in $fallbacks) { if (Test-Path $path) { return $path } }
    throw "Could not find $name. Install it, or start a new terminal so it is on the PATH."
}
$dotnet = Find-Tool 'dotnet' @('C:\Program Files\dotnet\dotnet.exe')
$git = Find-Tool 'git' @('C:\Program Files\Git\cmd\git.exe')
$gh = Find-Tool 'gh' @('C:\Program Files\GitHub CLI\gh.exe')

function Run([string]$exe, [string[]]$arguments) {
    & $exe @arguments
    if ($LASTEXITCODE -ne 0) { throw "Failed ($LASTEXITCODE): $(Split-Path $exe -Leaf) $($arguments -join ' ')" }
}
function Out-Of([string]$exe, [string[]]$arguments) {
    $text = & $exe @arguments
    if ($LASTEXITCODE -ne 0) { throw "Failed ($LASTEXITCODE): $(Split-Path $exe -Leaf) $($arguments -join ' ')" }
    return ($text -join "`n").Trim()
}
function Step([string]$text) { Write-Host ""; Write-Host "== $text" -ForegroundColor Cyan }
function Problem([string]$text) { if ($DryRun) { Write-Warning "$text (ignored in a dry run)" } else { throw $text } }

# A small JSON writer of our own: Windows PowerShell's ConvertTo-Json has an uneven layout and trips over empty arrays.
function ConvertTo-IndexJson($value, [string]$indent = '') {
    if ($null -eq $value) { return 'null' }
    if ($value -is [bool]) { return $(if ($value) { 'true' } else { 'false' }) }
    if ($value -is [string]) { return ((ConvertTo-Json -InputObject $value -Compress) -replace '\\u0027', "'" -replace '\\u0026', '&' -replace '\\u003c', '<' -replace '\\u003e', '>') }
    if ($value -is [ValueType]) { return [string]::Format([Globalization.CultureInfo]::InvariantCulture, '{0}', $value) }

    $inner = $indent + '  '
    if ($value -is [Collections.IDictionary] -or $value -is [Management.Automation.PSCustomObject]) {
        $pairs = if ($value -is [Collections.IDictionary]) { @($value.Keys | ForEach-Object { @{ K = $_; V = $value[$_] } }) } else { @($value.PSObject.Properties | ForEach-Object { @{ K = $_.Name; V = $_.Value } }) }
        if ($pairs.Count -eq 0) { return '{}' }
        $lines = foreach ($p in $pairs) { $inner + (ConvertTo-IndexJson ([string]$p.K)) + ': ' + (ConvertTo-IndexJson $p.V $inner) }
        return "{`n" + ($lines -join ",`n") + "`n$indent}"
    }
    $items = @($value)
    if ($items.Count -eq 0) { return '[]' }
    if (-not ($items | Where-Object { $_ -isnot [string] -and $_ -isnot [ValueType] })) {
        return '[' + (($items | ForEach-Object { ConvertTo-IndexJson $_ }) -join ', ') + ']'   # a list of plain values stays on one line
    }
    $lines = foreach ($item in $items) { $inner + (ConvertTo-IndexJson $item $inner) }
    return "[`n" + ($lines -join ",`n") + "`n$indent]"
}

# ---- 1. checks -----------------------------------------------------------------------------------
Step '1. Checking that everything is ready'
$csprojPath = Join-Path $root 'DutyNotes\DutyNotes.csproj'
$csproj = [xml](Get-Content $csprojPath -Raw)
$version = ($csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1).Trim()
if (-not $version) { throw "No <Version> in $csprojPath" }
$parts = $version.Split('.')
$tag = 'v' + $(if ($parts.Count -eq 4 -and $parts[3] -eq '0') { $parts[0..2] -join '.' } else { $version })
Write-Host "Version $version, tag $tag"

if (Out-Of $git @('-C', $root, 'status', '--porcelain')) { Problem 'This repository has uncommitted changes. Commit them first.' }
Run $git @('-C', $root, 'fetch', 'origin', '--quiet')
$branch = Out-Of $git @('-C', $root, 'rev-parse', '--abbrev-ref', 'HEAD')
if ($branch -ne 'main') { Problem "On branch '$branch', not main." }
if ((Out-Of $git @('-C', $root, 'rev-parse', 'HEAD')) -ne (Out-Of $git @('-C', $root, 'rev-parse', 'origin/main'))) { Problem 'main is not the same as origin/main. Push (or pull) first.' }

if (-not (Test-Path $IndexRepo)) { throw "The index repository was not found at $IndexRepo. Clone notingway/plogonway next to this repository, or pass -IndexRepo." }
$IndexRepo = (Resolve-Path $IndexRepo).Path
$indexFile = Join-Path $IndexRepo 'plogonway.json'
if (-not (Test-Path $indexFile)) { throw "No plogonway.json in $IndexRepo" }
if (Out-Of $git @('-C', $IndexRepo, 'status', '--porcelain')) { Problem "The index repository ($IndexRepo) has uncommitted changes." }
Run $git @('-C', $IndexRepo, 'fetch', 'origin', '--quiet')
if ((Out-Of $git @('-C', $IndexRepo, 'rev-parse', 'HEAD')) -ne (Out-Of $git @('-C', $IndexRepo, 'rev-parse', 'origin/main'))) { Problem 'The index repository is not up to date with origin/main.' }

& $gh auth status *> $null
if ($LASTEXITCODE -ne 0) { throw 'gh is not signed in. Run: gh auth login' }

# ---- 2. build and test ---------------------------------------------------------------------------
Step '2. Building in Release and running the tests'
Run $dotnet @('build', (Join-Path $root 'DutyNotes\DutyNotes.csproj'), '-c', 'Release', '--nologo', '-v', 'q')
Run $dotnet @('test', (Join-Path $root 'DutyNotes.Tests'), '-c', 'Release', '--nologo', '-v', 'q')

# ---- 3. verify the package -----------------------------------------------------------------------
Step '3. Checking the release package and the generated manifest'
$bin = Join-Path $root 'DutyNotes\bin\Release'
$zip = Join-Path $bin 'DutyNotes\latest.zip'
$manifestPath = Join-Path $bin 'DutyNotes.json'
if (-not (Test-Path $zip)) { throw "No package at $zip. It is only made by a Release build." }
if (-not (Test-Path $manifestPath)) { throw "No generated manifest at $manifestPath" }
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
$names = @($archive.Entries | ForEach-Object { $_.FullName })
$archive.Dispose()
foreach ($needed in @('DutyNotes.dll', 'DutyNotes.json')) {
    if ($names -notcontains $needed) { throw "latest.zip has no $needed (it has: $($names -join ', '))" }
}
if ($manifest.AssemblyVersion -ne $version) { throw "The manifest says version $($manifest.AssemblyVersion) but the csproj says $version. Rebuild." }
if ($manifest.InternalName -ne 'DutyNotes') { throw "InternalName is '$($manifest.InternalName)', not DutyNotes. It must never change." }
if ($manifest.RepoUrl -notmatch '^https://github\.com/([^/]+/[^/]+?)/?$') { throw "RepoUrl '$($manifest.RepoUrl)' is not a GitHub repository address." }
$slug = $Matches[1]
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
Write-Host "Package: $zip"
Write-Host "  contains: $($names -join ', ')"
Write-Host "  SHA-256:  $hash"
Write-Host "Manifest: $($manifest.Name) $($manifest.AssemblyVersion), API level $($manifest.DalamudApiLevel), repository $slug"

if (-not $Notes) { $Notes = "Experimental release $tag." }
$downloadUrl = "https://github.com/$slug/releases/download/$tag/latest.zip"

# ---- the index entry, from the build's manifest ---------------------------------------------------
$entry = [ordered]@{}
foreach ($property in $manifest.PSObject.Properties) { $entry[$property.Name] = $property.Value }
$entry['IsHide'] = $false
$entry['IsTestingExclusive'] = $false
$entry['DownloadLinkInstall'] = $downloadUrl
$entry['DownloadLinkUpdate'] = $downloadUrl
$entry['LastUpdate'] = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$entry['Changelog'] = $Notes

$imageBase = "https://raw.githubusercontent.com/$slug/main/images"
if (Test-Path (Join-Path $root 'images\icon.png')) { $entry['IconUrl'] = "$imageBase/icon.png" }
$shots = @(1..5 | Where-Object { Test-Path (Join-Path $root "images\image$_.png") } | ForEach-Object { "$imageBase/image$_.png" })
if ($shots.Count -gt 0) { $entry['ImageUrls'] = $shots }

# The index as it is now (an empty file is an empty list), minus any old entry for this plugin, plus the new one.
$entries = New-Object System.Collections.Generic.List[object]
$parsed = Get-Content $indexFile -Raw | ConvertFrom-Json
if ($null -ne $parsed) {
    # Windows PowerShell may hand back a JSON array as ONE object holding the list, so unwrap one more level.
    foreach ($candidate in @($parsed)) {
        foreach ($existingEntry in @($candidate)) {
            if ($existingEntry.InternalName -and $existingEntry.InternalName -ne $entry['InternalName']) { $entries.Add($existingEntry) }
        }
    }
}
$entries.Add($entry)
$newIndex = (ConvertTo-IndexJson $entries.ToArray()) + "`n"

if ($DryRun) {
    Step 'DRY RUN: nothing was published. This is what a real run would do'
    Write-Host "1. Create the GitHub release $tag in $slug$(if ($Prerelease) { ' (pre-release)' }) with latest.zip attached, notes: $Notes"
    Write-Host "2. Write this to $indexFile, then commit and push the index repository:"
    Write-Host ''
    Write-Host $newIndex
    $indexSlug = (Out-Of $git @('-C', $IndexRepo, 'remote', 'get-url', 'origin')) -replace '^https://github.com/', '' -replace '\.git$', ''
    Write-Host "Players would then see version $version after adding:"
    Write-Host "  https://raw.githubusercontent.com/$indexSlug/main/plogonway.json"
    return
}

# ---- 4. the GitHub release (first, so the index never points at a missing download) --------------
Step "4. Creating the GitHub release $tag"
& $gh release view $tag --repo $slug *> $null
if ($LASTEXITCODE -eq 0) { throw "A release $tag already exists in $slug. Bump <Version> in DutyNotes.csproj." }
$releaseArgs = @('release', 'create', $tag, $zip, '--repo', $slug, '--target', 'main', '--title', "Duty Notes $tag", '--notes', $Notes)
if ($Prerelease) { $releaseArgs += '--prerelease' }
Run $gh $releaseArgs

# ---- 5. the index ---------------------------------------------------------------------------------
Step '5. Updating the index repository'
try {
    [IO.File]::WriteAllText($indexFile, $newIndex, (New-Object Text.UTF8Encoding($false)))
    Run $git @('-C', $IndexRepo, 'add', 'plogonway.json')
    Run $git @('-C', $IndexRepo, 'commit', '-q', '-m', "Duty Notes $tag")
    Run $git @('-C', $IndexRepo, 'push')
}
catch {
    Write-Warning "The release $tag was created, but updating the index failed: $($_.Exception.Message)"
    Write-Warning "Fix the problem in $IndexRepo and commit and push plogonway.json there. Do not create the release again."
    throw
}

Step "Done: Duty Notes $tag is released"
Write-Host "Players add this address under /xlsettings > Experimental > Custom Plugin Repositories:"
Write-Host "  https://raw.githubusercontent.com/notingway/plogonway/main/plogonway.json"