param([switch]$RebuildCurrent)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$versionFile = Join-Path $projectRoot 'release-version.json'
$state = Get-Content -LiteralPath $versionFile -Raw | ConvertFrom-Json
$version = if ($RebuildCurrent -and $state.lastReleased) { $state.lastReleased } elseif ($state.lastReleased) { $previous = [version]$state.lastReleased; '{0}.{1}.{2}' -f $previous.Major, $previous.Minor, ($previous.Build + 1) } else { '1.0.0' }
$releaseFolder = Join-Path $projectRoot "dist/Mizu-License-Manager_v$version"
$archive = "$releaseFolder.zip"
if (!$RebuildCurrent -and ((Test-Path -LiteralPath $releaseFolder) -or (Test-Path -LiteralPath $archive))) { throw "Output already exists: $releaseFolder" }
& dotnet publish (Join-Path $projectRoot 'MizuLicenseManager.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true "-p:Version=$version" -o $releaseFolder
if ($LASTEXITCODE -ne 0) { throw 'Publish failed. Version was not advanced.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $releaseFolder 'README.md')
Compress-Archive -Path (Join-Path $releaseFolder '*') -DestinationPath $archive -Force:$RebuildCurrent
& (Join-Path $projectRoot 'Build-Installer.ps1') -Version $version -PayloadDirectory $releaseFolder
@{ lastReleased = $version } | ConvertTo-Json | Set-Content -LiteralPath $versionFile -Encoding UTF8
Write-Output "Released v$version : $archive"
