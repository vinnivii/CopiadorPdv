[CmdletBinding()]
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$projectPath = Join-Path $repository 'SoftcomSupportAutomation\SoftcomSupportAutomation.csproj'
[xml]$project = Get-Content -LiteralPath $projectPath -Raw -Encoding UTF8
$version = [string]$project.Project.PropertyGroup.Version
if ($version -cnotmatch '^[0-9]+\.[0-9]+\.[0-9]+$') { throw 'Version deve ser MAJOR.MINOR.PATCH.' }
if (!$SkipBuild) {
    & dotnet build $projectPath -c Release | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Build Release falhou; distribuicao nao gerada.' }
}

$source = Join-Path $repository 'SoftcomSupportAutomation\bin\Release\net48\ResetadorDePdvV2.exe'
if (!(Test-Path -LiteralPath $source -PathType Leaf)) { throw 'EXE compilado ausente. Execute o build Release.' }
if ([Reflection.AssemblyName]::GetAssemblyName($source).Version.ToString() -ne "$version.0" -or
    [Diagnostics.FileVersionInfo]::GetVersionInfo($source).FileVersion -ne "$version.0") {
    throw 'Versao do EXE compilado difere do projeto. Execute novamente o build Release.'
}

$fileName = "CentralAutomação($version).exe"
$distribution = Join-Path $repository 'dist'
if (!(Test-Path -LiteralPath $distribution)) { $null = New-Item -ItemType Directory -Path $distribution }
if ((Get-Item -LiteralPath $distribution).Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw 'dist nao pode ser um link ou junction.'
}
$previous = @(Get-ChildItem -LiteralPath $distribution -Force)
foreach ($entry in $previous) {
    if ($entry.PSIsContainer -or ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        $entry.Name -cnotmatch '^CentralAutomação\([0-9]+\.[0-9]+\.[0-9]+\)\.exe$') {
        throw "dist contem arquivo inesperado: $($entry.Name). Nenhum arquivo foi removido."
    }
}
$destination = Join-Path $distribution $fileName
$sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
Copy-Item -LiteralPath $source -Destination $destination -Force
if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $sourceHash) {
    Remove-Item -LiteralPath $destination
    throw 'SHA-256 da copia difere do EXE compilado; copia descartada.'
}
# Remove somente copias de distribuicao antigas deste processo de build.
foreach ($entry in $previous) {
    if ($entry.Name -cne $fileName) { Remove-Item -LiteralPath $entry.FullName }
}
$files = @(Get-ChildItem -LiteralPath $distribution -Force)
if ($files.Count -ne 1 -or $files[0].Name -cne $fileName -or $files[0].PSIsContainer) {
    throw 'dist deve conter somente o EXE de distribuicao.'
}
Write-Host "Distribuicao verificada: $destination"
[pscustomobject]@{ Version=$version; FileName=$fileName; ExecutablePath=$destination; Sha256=$sourceHash.ToLowerInvariant() }
