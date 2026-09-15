param(
    [Parameter(Mandatory)][string]$Version,
    [string]$PayloadDirectory,
    [string]$CompilerPath = (Join-Path $PSScriptRoot '.tools\InnoSetup\ISCC.exe'),
    [switch]$TestBuild
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must be major.minor.patch.' }
if (!$PayloadDirectory) { $PayloadDirectory = Join-Path $PSScriptRoot "dist\Mizu-License-Manager_v$Version" }
$PayloadDirectory = (Resolve-Path -LiteralPath $PayloadDirectory).Path
if (!(Test-Path -LiteralPath $CompilerPath)) { throw 'Inno Setup 6.7 or newer is required. Supply -CompilerPath pointing to ISCC.exe.' }
if (!(Test-Path -LiteralPath (Join-Path $PayloadDirectory 'Mizu-License-Manager.exe'))) { throw 'Application payload is missing.' }
$outputDirectory = Join-Path $PSScriptRoot 'dist'
$arguments = @("/DAppVersion=$Version", "/DPayloadDir=$PayloadDirectory", "/DOutputDir=$outputDirectory")
if ($TestBuild) {
    $outputDirectory = Join-Path $PSScriptRoot 'tests\installer-output'
    $arguments[2] = "/DOutputDir=$outputDirectory"
    $arguments += '/DTestBuild=1'
}
& $CompilerPath @arguments (Join-Path $PSScriptRoot 'installer\Mizu-License-Manager.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
Write-Output (Join-Path $outputDirectory "Mizu-License-Manager_Setup_v$Version.exe")
