$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
[xml]$project = Get-Content -LiteralPath (Join-Path $repository 'SoftcomSupportAutomation\SoftcomSupportAutomation.csproj') -Raw -Encoding UTF8
$version = [string]$project.Project.PropertyGroup.Version
$fileName = "CentralAutomação($version).exe"
$source = Join-Path $repository 'SoftcomSupportAutomation\bin\Release\net48\ResetadorDePdvV2.exe'
$distribution = Join-Path $repository 'dist'
$files = @(Get-ChildItem -LiteralPath $distribution -Force)
if ($files.Count -ne 1 -or $files[0].Name -cne $fileName -or $files[0].PSIsContainer) { throw 'Distribuicao deve conter somente o EXE com o nome oficial.' }
$sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
if ((Get-FileHash -LiteralPath $files[0].FullName -Algorithm SHA256).Hash -ne $sourceHash) { throw 'Hash de distribuicao divergente.' }
if ([Reflection.AssemblyName]::GetAssemblyName($files[0].FullName).Version.ToString() -ne "$version.0" -or
    [Diagnostics.FileVersionInfo]::GetVersionInfo($files[0].FullName).FileVersion -ne "$version.0") { throw 'Metadados de versao divergentes.' }
Write-Output "PASS: dist contem somente $fileName, com SHA-256 e metadados conferidos."

$fixture = Join-Path $env:TEMP ('CopiadorPdv distribuição (QA) ' + [guid]::NewGuid().ToString('N'))
$fixtureScripts = Join-Path $fixture 'scripts'
$fixtureProject = Join-Path $fixture 'SoftcomSupportAutomation'
$fixtureBin = Join-Path $fixtureProject 'bin\Release\net48'
$fixtureDist = Join-Path $fixture 'dist'
$null = New-Item -ItemType Directory -Path $fixtureScripts,$fixtureBin,$fixtureDist
Copy-Item -LiteralPath (Join-Path $repository 'scripts\Build-Distribution.ps1') -Destination $fixtureScripts
Copy-Item -LiteralPath (Join-Path $repository 'SoftcomSupportAutomation\SoftcomSupportAutomation.csproj') -Destination $fixtureProject
Copy-Item -LiteralPath $source -Destination $fixtureBin
$staleFile = Join-Path $fixtureDist 'CentralAutomação(0.0.0).exe'
[IO.File]::WriteAllText($staleFile, 'fixture antiga, nunca executada')
$prepared = & (Join-Path $fixtureScripts 'Build-Distribution.ps1') -SkipBuild
if ($prepared.FileName -cne $fileName -or $prepared.Sha256 -ne $sourceHash -or
    (Test-Path -LiteralPath $staleFile) -or @(Get-ChildItem -LiteralPath $fixtureDist -Force).Count -ne 1) {
    throw 'Falha ao preparar distribuicao em caminho Unicode com espacos e parenteses.'
}
Write-Output 'PASS: preparacao em caminho Unicode/espacos/parenteses e limpeza somente da copia antiga conhecida.'

$unexpected = Join-Path $fixtureDist 'nao-remover.config'
[IO.File]::WriteAllText($unexpected, 'fixture preservada')
$rejected = $false
try { $null = & (Join-Path $fixtureScripts 'Build-Distribution.ps1') -SkipBuild }
catch { if ($_.Exception.Message -like 'dist contem arquivo inesperado:*') { $rejected = $true } else { throw } }
if (!$rejected -or !(Test-Path -LiteralPath $unexpected) -or
    (Get-FileHash -LiteralPath $prepared.ExecutablePath -Algorithm SHA256).Hash -ne $sourceHash) {
    throw 'Arquivo desconhecido em dist nao foi preservado ou nao impediu empacotamento.'
}
Write-Output 'PASS: arquivo inesperado bloqueia distribuicao sem apagar dados.'
Remove-Item -LiteralPath $unexpected

$project.Project.PropertyGroup.Version = '999.0.0'
$project.Save((Join-Path $fixtureProject 'SoftcomSupportAutomation.csproj'))
$rejected = $false
try { $null = & (Join-Path $fixtureScripts 'Build-Distribution.ps1') -SkipBuild }
catch { if ($_.Exception.Message -like 'Versao do EXE compilado difere*') { $rejected = $true } else { throw } }
if (!$rejected -or (Get-FileHash -LiteralPath $prepared.ExecutablePath -Algorithm SHA256).Hash -ne $sourceHash) {
    throw 'EXE desatualizado nao foi recusado antes de alterar a distribuicao.'
}
Write-Output 'PASS: versao do projeto divergente do EXE compilado e recusada sem substituir a copia validada.'
Write-Output ('Fixtures de distribuicao: ' + $fixture)
