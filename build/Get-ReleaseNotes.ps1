<#
.SYNOPSIS
    Verifies a release version against AppVersion.Current and writes that version's changelog
    entry as markdown release notes.
.DESCRIPTION
    Used by the release workflow before anything is built: a tag that doesn't match the app's
    version, or a version with no changelog entry, stops the release.
#>
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$OutFile = 'release-notes.md',
    [string]$RepoRoot
)

$ErrorActionPreference = 'Stop'

# PS 5.1 leaves $PSScriptRoot unset while evaluating a later parameter's default value when the
# param block also has a Mandatory parameter, so the default is computed here instead of inline.
if (-not $RepoRoot) {
    $RepoRoot = Split-Path -Parent $PSScriptRoot
}

function Fail([string]$message) {
    [Console]::Error.WriteLine($message)
    exit 1
}

# PS 5.1 reads BOM-less UTF-8 as ANSI unless told otherwise; -Encoding UTF8 fixes that for both
# PS 5.1 and pwsh 7, keeping non-ASCII characters (em dashes, etc.) intact.
$appVersionSource = Get-Content -Raw -Encoding UTF8 (Join-Path $RepoRoot 'src/ThiefManager/AppVersion.cs')
$appVersionMatch = [regex]::Match($appVersionSource, 'Current\s*=\s*"([^"]+)"')
if (-not $appVersionMatch.Success) {
    Fail "Couldn't read AppVersion.Current from src/ThiefManager/AppVersion.cs."
}
$appVersion = $appVersionMatch.Groups[1].Value
if ($appVersion -ne $Version) {
    Fail "Release version $Version doesn't match AppVersion.Current ($appVersion). Bump AppVersion.cs or fix the tag."
}

$changelogSource = Get-Content -Raw -Encoding UTF8 (Join-Path $RepoRoot 'src/ThiefManager/ChangelogEntry.cs')
$entryPattern = 'new\("' + [regex]::Escape($Version) + '",\s*"[^"]*",\s*new\[\]\s*\{(?<body>.*?)\}\)'
$entry = [regex]::Match($changelogSource, $entryPattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $entry.Success) {
    Fail "No ChangelogData entry for version $Version in src/ThiefManager/ChangelogEntry.cs."
}

# Each change is a C# string literal; unescape \" and \\ so the notes read exactly like the app's Changelog window.
$bullets = @([regex]::Matches($entry.Groups['body'].Value, '"((?:[^"\\]|\\.)*)"') | ForEach-Object {
    '- ' + ($_.Groups[1].Value -replace '\\"', '"' -replace '\\\\', '\')
})
if ($bullets.Count -eq 0) {
    Fail "The ChangelogData entry for version $Version has no changes listed."
}

$fullOutPath = if ([System.IO.Path]::IsPathRooted($OutFile)) { $OutFile } else { Join-Path (Get-Location) $OutFile }
# WriteAllLines writes UTF-8 without a BOM, which vpk reads cleanly as markdown.
[System.IO.File]::WriteAllLines($fullOutPath, [string[]]$bullets)
Write-Host "Release notes for $Version written to $fullOutPath"
