param([string]$ExecutablePath)
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
[xml]$project = Get-Content -LiteralPath (Join-Path $repository 'SoftcomSupportAutomation\SoftcomSupportAutomation.csproj') -Raw -Encoding UTF8
$version = [string]$project.Project.PropertyGroup.Version
$fileName = "CentralAutomação($version).exe"
if (!$ExecutablePath) { $ExecutablePath = Join-Path $repository (Join-Path 'dist' $fileName) }
if (!(Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) { throw 'EXE ausente. Execute scripts\Build-Distribution.ps1 antes deste teste.' }
Add-Type -AssemblyName System.Windows.Forms,System.Drawing,System.IO.Compression
$testRoot = Join-Path $env:TEMP ('CopiadorPdv testes (portátil) ' + [guid]::NewGuid().ToString('N'))
$appRoot = Join-Path $testRoot 'aplicação (EXE isolado)'
$dataRoot = Join-Path $testRoot 'data'
$null = New-Item -ItemType Directory -Path $appRoot,$dataRoot
$exe = Join-Path $appRoot $fileName
Copy-Item -LiteralPath $ExecutablePath -Destination $exe
if ((Get-FileHash -LiteralPath $ExecutablePath -Algorithm SHA256).Hash -ne
    (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash) { throw 'SHA-256 divergente na copia isolada.' }
$assembly = [Reflection.Assembly]::LoadFile($exe)
$type = $assembly.GetType('SoftcomSupportAutomation.SupportForm',$true)
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$statics = [Reflection.BindingFlags]'Static,NonPublic'
if ($assembly.GetName().Version.ToString() -ne ($version+'.0')) { throw 'AssemblyVersion divergente' }
if ([Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion -ne ($version+'.0')) { throw 'FileVersion divergente' }
if ($type.GetField('ApplicationVersion',$statics).GetValue($null) -ne $version) { throw 'Versao visual divergente' }
if ($type.GetField('AutomationUserAgent',$statics).GetValue($null) -ne ('SoftcomSupportAutomation/'+$version)) { throw 'User-Agent divergente' }
if (@(Get-ChildItem -LiteralPath $appRoot -Force).Count -ne 1 -or
    (Get-Item -LiteralPath $exe).Name -cne $fileName) { throw 'EXE nao esta isolado com o nome oficial' }
$form = [Activator]::CreateInstance($type,$true)
$null = $form.Handle
$type.GetField('logFile',$flags).SetValue($form,(Join-Path $dataRoot 'test.log'))
try {
    $resources = @($assembly.GetManifestResourceNames())
    foreach ($name in @('Atualizar-SoftcomBackup-v6.ps1','AJUSTE_UPDATE_RDP.bat','Finalizar e Reiniciar o Softconect.bat','LimparFilaDeImpressao.bat')) {
        $stream = $assembly.GetManifestResourceStream('SoftcomSupportAutomation.Scripts.'+$name)
        if ($null -eq $stream) { throw 'Script incorporado ausente' }
        try { if ($stream.Length -eq 0) { throw 'Script incorporado vazio' } } finally { $stream.Dispose() }
    }
    $zipPath = Join-Path $dataRoot 'sample.zip'
    $zipStream = [IO.File]::Create($zipPath)
    $payload = [Text.Encoding]::UTF8.GetBytes(('teste de extracao, nao e instalador' + [Environment]::NewLine)*500)
    try {
        $zip = [IO.Compression.ZipArchive]::new($zipStream,[IO.Compression.ZipArchiveMode]::Create,$true)
        try {
            $entry = $zip.CreateEntry('subpasta/SetupSoftshopFrenteLoja.msi',[IO.Compression.CompressionLevel]::Optimal)
            $stream = $entry.Open()
            try { $stream.Write($payload,0,$payload.Length) } finally { $stream.Dispose() }
        } finally { $zip.Dispose() }
    } finally { $zipStream.Dispose() }
    $extract = $type.GetMethod('ExtractMsiFromArchive',$flags)
    $zipOutput = Join-Path $dataRoot 'SetupSoftshopFrenteLoja.msi'
    $null = $extract.Invoke($form,[object[]]@([string]$zipPath,[string]$zipOutput,[Threading.CancellationToken]::None))
    if ([Convert]::ToBase64String([IO.File]::ReadAllBytes($zipOutput)) -ne [Convert]::ToBase64String($payload)) { throw 'ZIP extraido divergente' }
    $rarPath = Join-Path $PSScriptRoot 'fixtures\sample.rar'
    $rarOutput = Join-Path $dataRoot 'payload.txt'
    $null = $extract.Invoke($form,[object[]]@([string]$rarPath,[string]$rarOutput,[Threading.CancellationToken]::None))
    if ([IO.File]::ReadAllText($rarOutput) -ne "hello from RAR!`n") { throw 'RAR extraido divergente' }
    $type.GetField('stopRequested',$flags).SetValue($form,$true)
    $cancelled = $false
    try { $null = $extract.Invoke($form,[object[]]@([string]$zipPath,[string]$zipOutput,[Threading.CancellationToken]::None)) }
    catch { if ($_.Exception.GetBaseException() -is [OperationCanceledException]) { $cancelled=$true } else { throw } }
    if (!$cancelled) { throw 'Extracao ignorou cancelamento' }
    $type.GetField('stopRequested',$flags).SetValue($form,$false)
    Write-Output 'PASS: EXE isolado, metadados/UI/User-Agent, recursos, ZIP, RAR e cancelamento de extracao.'
    Add-Type -Path (Join-Path $PSScriptRoot 'DownloadTests.cs') -ReferencedAssemblies System,System.Core,System.Windows.Forms
    [DownloadTests]::Run($form,$dataRoot)
    $installerFixtureExe = Join-Path $dataRoot 'InstallerHost.exe'
    Add-Type -Path (Join-Path $PSScriptRoot 'fixtures\InstallerHost.cs') -OutputAssembly $installerFixtureExe -OutputType ConsoleApplication -ReferencedAssemblies System
    Add-Type -Path (Join-Path $PSScriptRoot 'InstallerTests.cs') -ReferencedAssemblies System,System.Core
    [InstallerTests]::Run($form,$dataRoot,$installerFixtureExe)
    Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,WindowsBase
    $fixtureExe = Join-Path $dataRoot 'Softshop.exe'
    Add-Type -Path (Join-Path $PSScriptRoot 'fixtures\DialogHost.cs') -OutputAssembly $fixtureExe -OutputType WindowsApplication -ReferencedAssemblies System,System.Windows.Forms,System.Drawing
    $automationReferences = @('System','System.Core','System.Windows.Forms',
        [Windows.Automation.AutomationElement].Assembly.Location,
        [Windows.Automation.ControlType].Assembly.Location,
        [Windows.Rect].Assembly.Location)
    Add-Type -Path (Join-Path $PSScriptRoot 'AutomationTests.cs') -ReferencedAssemblies $automationReferences
    [AutomationTests]::Run($form,$dataRoot,$fixtureExe)
    [Windows.Forms.Application]::DoEvents()
    Write-Output 'LIMITACAO: nenhum instalador, senha ou servico real foi alterado; homologacao de cliente continua separada.'
    Write-Output ('Resultados locais: '+$testRoot)
} finally { $form.Dispose() }
