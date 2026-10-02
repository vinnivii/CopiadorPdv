#requires -version 5.1
# SoftcomBackup - Remocao e reinstalacao automatica
# Compatibilidade: Windows 10 x64, Windows 11 x64, Windows Server 2016 x64 e Windows Server 2022 x64

$ErrorActionPreference = "Stop"
$ScriptVersion = "6.0 - DOWNLOAD COMPATIVEL COM SERVER 2016"

# ============================================================
# CONFIGURACOES
# ============================================================

$DownloadUrl = "http://177.43.232.2:25123/helptools2/public/core/arquivo/download/id/403/"
$TempDir     = "C:\SoftcomBackupInstaller"
$MsiName     = "SoftcomBackup-Setup.msi"
$MsiPath     = Join-Path $TempDir $MsiName

$InstallDir  = "C:\Program Files (x86)\Softcom Tecnologia\SoftcomBackup"
$ManagerExe  = Join-Path $InstallDir "SoftcomBackup.Manager.exe"

$ServiceNames = @(
    "SoftcomBackup",
    "SoftcomBackupServiceUpdate"
)

$ProcessImages = @(
    "softcombackup.service.exe",
    "softcombackup.serviceupdate.exe",
    "SoftcomBackup.Manager.exe"
)

# ============================================================
# FUNCOES
# ============================================================

function Show-Step {
    param([string]$Message)

    Write-Host ""
    Write-Host "============================================================"
    Write-Host $Message
    Write-Host "============================================================"
}

function Fail-Script {
    param([string]$Message)

    Write-Host ""
    Write-Host "ERRO: $Message" -ForegroundColor Red
    Write-Host ""
    Write-Host "O processo foi interrompido."
    Write-Host "Pressione qualquer tecla para fechar esta janela..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

function Test-IsAdministrator {
    $identity  = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)

    return $principal.IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator
    )
}

function Test-MsiFile {
    param([Parameter(Mandatory=$true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }

    $file = Get-Item -LiteralPath $Path

    # Evita aceitar uma pagina HTML/erro muito pequena como se fosse instalador.
    if ($file.Length -lt 10240) {
        return $false
    }

    # MSI usa o formato Compound File Binary.
    # Assinatura esperada: D0 CF 11 E0 A1 B1 1A E1
    $expected = [byte[]](0xD0,0xCF,0x11,0xE0,0xA1,0xB1,0x1A,0xE1)

    try {
        $stream = [System.IO.File]::OpenRead($Path)
        try {
            $header = New-Object byte[] 8
            $read = $stream.Read($header, 0, 8)

            if ($read -ne 8) {
                return $false
            }

            for ($i = 0; $i -lt 8; $i++) {
                if ($header[$i] -ne $expected[$i]) {
                    return $false
                }
            }

            return $true
        }
        finally {
            $stream.Dispose()
        }
    }
    catch {
        return $false
    }
}

function Invoke-MsiExec {
    param(
        [Parameter(Mandatory=$true)]
        [string[]]$Arguments,

        [Parameter(Mandatory=$true)]
        [string]$Description
    )

    Write-Host $Description

    $process = Start-Process `
        -FilePath "$env:WINDIR\System32\msiexec.exe" `
        -ArgumentList $Arguments `
        -Wait `
        -PassThru

    # 0    = sucesso
    # 1641 = sucesso, reinicializacao solicitada/iniciada
    # 3010 = sucesso, reinicializacao necessaria
    if ($process.ExitCode -notin @(0, 1641, 3010)) {
        throw "$Description falhou. Codigo de retorno do Windows Installer: $($process.ExitCode)"
    }

    if ($process.ExitCode -in @(1641, 3010)) {
        Write-Host "A operacao informou que uma reinicializacao pode ser necessaria. O script continuara sem reiniciar."
    }
}

function Get-SoftcomBackupUninstallEntries {
    $registryPaths = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
    )

    $entries = foreach ($path in $registryPaths) {
        Get-ItemProperty -Path $path -ErrorAction SilentlyContinue |
            Where-Object {
                $_.DisplayName -and
                $_.DisplayName.Trim() -like "SoftcomBackup*"
            }
    }

    # Remove eventuais duplicidades.
    $entries | Sort-Object PSPath -Unique
}

function Invoke-RegisteredUninstall {
    param(
        [Parameter(Mandatory=$true)]
        $Entry
    )

    $displayName = $Entry.DisplayName

    if ([string]::IsNullOrWhiteSpace($displayName)) {
        $displayName = "SoftcomBackup"
    }

    Write-Host "Encontrado: $displayName"

    # --------------------------------------------------------
    # CASO 1: MSI / ProductCode
    # --------------------------------------------------------

    $productCode = $null

    if ($Entry.PSChildName -match '^\{[0-9A-Fa-f\-]{36}\}$') {
        $productCode = $Entry.PSChildName
    }
    elseif ($Entry.UninstallString -match '\{[0-9A-Fa-f\-]{36}\}') {
        $productCode = $Matches[0]
    }

    if ($productCode) {
        Invoke-MsiExec `
            -Arguments @("/x", $productCode, "/qn", "/norestart") `
            -Description "Desinstalando $displayName silenciosamente..."
        return
    }

    # --------------------------------------------------------
    # CASO 2: desinstalador EXE com QuietUninstallString
    # --------------------------------------------------------

    $command = $Entry.QuietUninstallString

    if (-not [string]::IsNullOrWhiteSpace($command)) {
        Write-Host "Executando desinstalacao silenciosa registrada pelo software..."

        $proc = Start-Process `
            -FilePath "$env:WINDIR\System32\cmd.exe" `
            -ArgumentList @("/d", "/s", "/c", "`"$command`"") `
            -Wait `
            -PassThru

        if ($proc.ExitCode -notin @(0, 1641, 3010)) {
            throw "Falha ao desinstalar $displayName. Codigo de retorno: $($proc.ExitCode)"
        }

        return
    }

    # --------------------------------------------------------
    # CASO 3: EXE sem QuietUninstallString
    # --------------------------------------------------------
    #
    # Como nao conhecemos o empacotador de todas as versoes antigas,
    # primeiro usamos a string de desinstalacao registrada e adicionamos
    # parametros silenciosos comuns. Se o desinstalador nao os reconhecer,
    # ele podera retornar erro e o script sera interrompido antes de apagar
    # a pasta antiga.
    # --------------------------------------------------------

    $command = $Entry.UninstallString

    if ([string]::IsNullOrWhiteSpace($command)) {
        throw "Nao foi encontrada uma rotina de desinstalacao para $displayName."
    }

    Write-Host "Executando desinstalador EXE..."

    $silentCommand = "$command /S /silent /VERYSILENT /SUPPRESSMSGBOXES /NORESTART"

    $proc = Start-Process `
        -FilePath "$env:WINDIR\System32\cmd.exe" `
        -ArgumentList @("/d", "/s", "/c", "`"$silentCommand`"") `
        -Wait `
        -PassThru

    if ($proc.ExitCode -notin @(0, 1641, 3010)) {
        throw "Falha ao desinstalar $displayName. Codigo de retorno: $($proc.ExitCode)"
    }
}


function Stop-SoftcomProcessSafe {
    param(
        [Parameter(Mandatory=$true)]
        [string]$ImageName
    )

    # IMPORTANTE:
    # Esta funcao NUNCA interrompe o script.
    # Se o processo nao existir, TASKKILL normalmente retorna codigo de erro.
    # Aqui esse codigo e tratado apenas como informacao e a execucao continua.

    try {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = "$env:WINDIR\System32\taskkill.exe"
        $psi.Arguments = "/F /IM `"$ImageName`" /T"
        $psi.UseShellExecute = $false
        $psi.CreateNoWindow = $true
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true

        $proc = New-Object System.Diagnostics.Process
        $proc.StartInfo = $psi

        $null = $proc.Start()

        $stdout = $proc.StandardOutput.ReadToEnd()
        $stderr = $proc.StandardError.ReadToEnd()

        $proc.WaitForExit()
        $exitCode = $proc.ExitCode
        $proc.Dispose()

        if ($exitCode -eq 0) {
            Write-Host "Processo encerrado: $ImageName"
        }
        else {
            Write-Host "Processo nao encontrado ou ja estava fechado: $ImageName - continuando."
        }
    }
    catch {
        # Mesmo se o TASKKILL falhar por qualquer motivo, esta etapa
        # nao e considerada critica e o restante do script continua.
        Write-Host "Nao foi possivel encerrar/verificar $ImageName - continuando."
    }

    return
}


function Download-Installer {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Url,

        [Parameter(Mandatory=$true)]
        [string]$Destination
    )

    # User-Agent de navegador para evitar servidores que rejeitam
    # o User-Agent padrao do Windows PowerShell em sistemas antigos.
    $browserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36"

    # Garante que nao exista arquivo parcial de uma tentativa anterior.
    if (Test-Path -LiteralPath $Destination) {
        Remove-Item -LiteralPath $Destination -Force -ErrorAction SilentlyContinue
    }

    # --------------------------------------------------------
    # METODO 1 - System.Net.WebClient
    # Compativel com Windows PowerShell 5.1 / Server 2016.
    # --------------------------------------------------------
    try {
        Write-Host "Tentativa 1: download via .NET WebClient..."

        $webClient = New-Object System.Net.WebClient

        try {
            $webClient.Headers.Add("User-Agent", $browserUserAgent)
            $webClient.Headers.Add("Accept", "*/*")

            if ($null -ne $webClient.Proxy) {
                $webClient.Proxy.Credentials = [System.Net.CredentialCache]::DefaultCredentials
            }

            $webClient.DownloadFile($Url, $Destination)
        }
        finally {
            $webClient.Dispose()
        }

        if ((Test-Path -LiteralPath $Destination -PathType Leaf) -and
            ((Get-Item -LiteralPath $Destination).Length -gt 0)) {

            Write-Host "Download concluido pelo metodo WebClient."
            return
        }

        throw "O WebClient terminou sem criar um arquivo valido."
    }
    catch {
        Write-Host "Metodo WebClient falhou: $($_.Exception.Message)" -ForegroundColor Yellow

        if (Test-Path -LiteralPath $Destination) {
            Remove-Item -LiteralPath $Destination -Force -ErrorAction SilentlyContinue
        }
    }

    # --------------------------------------------------------
    # METODO 2 - BITS
    # --------------------------------------------------------
    try {
        Write-Host "Tentativa 2: download via BITS..."

        Import-Module BitsTransfer -ErrorAction Stop

        Start-BitsTransfer `
            -Source $Url `
            -Destination $Destination `
            -ErrorAction Stop

        if ((Test-Path -LiteralPath $Destination -PathType Leaf) -and
            ((Get-Item -LiteralPath $Destination).Length -gt 0)) {

            Write-Host "Download concluido pelo metodo BITS."
            return
        }

        throw "O BITS terminou sem criar um arquivo valido."
    }
    catch {
        Write-Host "Metodo BITS falhou: $($_.Exception.Message)" -ForegroundColor Yellow

        if (Test-Path -LiteralPath $Destination) {
            Remove-Item -LiteralPath $Destination -Force -ErrorAction SilentlyContinue
        }
    }

    # --------------------------------------------------------
    # METODO 3 - Invoke-WebRequest com User-Agent explicito
    # --------------------------------------------------------
    try {
        Write-Host "Tentativa 3: download via Invoke-WebRequest..."

        Invoke-WebRequest `
            -Uri $Url `
            -OutFile $Destination `
            -UseBasicParsing `
            -UserAgent $browserUserAgent `
            -Headers @{ Accept = "*/*" } `
            -TimeoutSec 300 `
            -ErrorAction Stop

        if ((Test-Path -LiteralPath $Destination -PathType Leaf) -and
            ((Get-Item -LiteralPath $Destination).Length -gt 0)) {

            Write-Host "Download concluido pelo metodo Invoke-WebRequest."
            return
        }

        throw "O Invoke-WebRequest terminou sem criar um arquivo valido."
    }
    catch {
        if (Test-Path -LiteralPath $Destination) {
            Remove-Item -LiteralPath $Destination -Force -ErrorAction SilentlyContinue
        }

        throw "Todos os metodos de download falharam. Ultimo erro: $($_.Exception.Message)"
    }
}

# ============================================================
# INICIO
# ============================================================

try {
    Clear-Host

    Write-Host "SOFTCOMBACKUP - ATUALIZACAO AUTOMATICA"
    Write-Host "VERSAO DO SCRIPT: $ScriptVersion" -ForegroundColor Cyan
    Write-Host "POWERSHELL: $($PSVersionTable.PSVersion.ToString())" -ForegroundColor Cyan
    Write-Host ""

    if (-not (Test-IsAdministrator)) {
        Fail-Script "Este script precisa ser executado como Administrador."
    }

    # --------------------------------------------------------
    # 1. PREPARA DIRETORIO TEMPORARIO
    # --------------------------------------------------------

    Show-Step "Preparando download..."

    if (-not (Test-Path -LiteralPath $TempDir)) {
        New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
    }

    # Remove somente eventual MSI antigo com o mesmo nome.
    if (Test-Path -LiteralPath $MsiPath) {
        Remove-Item -LiteralPath $MsiPath -Force
    }

    # --------------------------------------------------------
    # 2. DOWNLOAD
    # --------------------------------------------------------

    Show-Step "Baixando o novo instalador..."

    Write-Host "Origem:  $DownloadUrl"
    Write-Host "Destino: $MsiPath"
    Write-Host ""

    Download-Installer `
        -Url $DownloadUrl `
        -Destination $MsiPath

    # --------------------------------------------------------
    # 3. VALIDACAO DO DOWNLOAD
    # --------------------------------------------------------

    Show-Step "Validando o instalador baixado..."

    if (-not (Test-MsiFile -Path $MsiPath)) {
        throw "O arquivo baixado nao parece ser um MSI valido. Nada foi desinstalado."
    }

    $downloadedFile = Get-Item -LiteralPath $MsiPath

    Write-Host "Arquivo validado."
    Write-Host ("Tamanho: {0:N2} MB" -f ($downloadedFile.Length / 1MB))

    # --------------------------------------------------------
    # 4. PARA SERVICOS
    # --------------------------------------------------------

    Show-Step "Parando servicos do SoftcomBackup..."

    foreach ($serviceName in $ServiceNames) {
        try {
            $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue

            if ($null -eq $service) {
                Write-Host "Servico nao encontrado: $serviceName - continuando."
                continue
            }

            if ($service.Status -eq "Stopped") {
                Write-Host "Servico ja esta parado: $serviceName"
                continue
            }

            Write-Host "Parando: $serviceName"

            Stop-Service `
                -Name $serviceName `
                -Force `
                -ErrorAction SilentlyContinue
        }
        catch {
            Write-Host "Nao foi possivel parar o servico $serviceName - continuando."
        }
    }

    Start-Sleep -Seconds 2

    # --------------------------------------------------------
    # 5. TASKKILL COMO GARANTIA
    # --------------------------------------------------------

    Show-Step "Encerrando processos restantes..."

    foreach ($imageName in $ProcessImages) {
        Stop-SoftcomProcessSafe -ImageName $imageName
    }

    Start-Sleep -Seconds 2

    # --------------------------------------------------------
    # 6. DESINSTALA TODAS AS VERSOES ENCONTRADAS
    # --------------------------------------------------------

    Show-Step "Procurando versoes instaladas do SoftcomBackup..."

    $uninstallEntries = @(Get-SoftcomBackupUninstallEntries)

    if ($uninstallEntries.Count -eq 0) {
        Write-Host "Nenhuma instalacao registrada do SoftcomBackup foi encontrada."
        Write-Host "Continuando normalmente."
    }
    else {
        Write-Host "$($uninstallEntries.Count) instalacao(oes) encontrada(s)."
        Write-Host ""

        foreach ($entry in $uninstallEntries) {
            Invoke-RegisteredUninstall -Entry $entry
        }
    }

    Start-Sleep -Seconds 2

    # --------------------------------------------------------
    # 7. GARANTE QUE OS PROCESSOS CONTINUAM FECHADOS
    # --------------------------------------------------------

    foreach ($imageName in $ProcessImages) {
        Stop-SoftcomProcessSafe -ImageName $imageName
    }

    # --------------------------------------------------------
    # 8. APAGA PASTA RESIDUAL
    # --------------------------------------------------------

    Show-Step "Removendo pasta antiga..."

    if (Test-Path -LiteralPath $InstallDir) {
        Write-Host "Excluindo: $InstallDir"

        Remove-Item `
            -LiteralPath $InstallDir `
            -Recurse `
            -Force

        Start-Sleep -Seconds 1

        if (Test-Path -LiteralPath $InstallDir) {
            throw "Nao foi possivel excluir completamente a pasta: $InstallDir"
        }
    }
    else {
        Write-Host "A pasta antiga nao existe. Continuando."
    }

    # --------------------------------------------------------
    # 9. INSTALA NOVA VERSAO
    # --------------------------------------------------------

    Show-Step "Instalando BackupCloud / SoftcomBackup..."

    Invoke-MsiExec `
        -Arguments @("/i", "`"$MsiPath`"", "/qn", "/norestart") `
        -Description "Executando instalacao silenciosa..."

    # Da um pequeno tempo para o instalador finalizar criacao de arquivos.
    Start-Sleep -Seconds 3

    # --------------------------------------------------------
    # 10. CONFIRMA EXECUTAVEL
    # --------------------------------------------------------

    Show-Step "Validando a nova instalacao..."

    if (-not (Test-Path -LiteralPath $ManagerExe -PathType Leaf)) {
        throw "A instalacao terminou, mas o executavel nao foi encontrado em: $ManagerExe"
    }

    Write-Host "Executavel encontrado:"
    Write-Host $ManagerExe

    # --------------------------------------------------------
    # 11. INICIA O MANAGER
    # --------------------------------------------------------

    Show-Step "Iniciando SoftcomBackup Manager..."

    Start-Process -FilePath $ManagerExe

    # --------------------------------------------------------
    # FINAL
    # --------------------------------------------------------

    Write-Host ""
    Write-Host "============================================================"
    Write-Host "PROCESSO CONCLUIDO COM SUCESSO" -ForegroundColor Green
    Write-Host "============================================================"
    Write-Host ""
    Write-Host "O instalador foi mantido em:"
    Write-Host $MsiPath
    Write-Host ""
    Write-Host "O computador NAO sera reiniciado automaticamente."

    Start-Sleep -Seconds 3
    exit 0
}
catch {
    Fail-Script $_.Exception.Message
}
