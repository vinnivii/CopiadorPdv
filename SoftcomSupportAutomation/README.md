# Central de Automações Softcom V2

Aplicação desktop para centralizar rotinas de suporte técnico da Softcom em um único executável administrativo. A interface utiliza um tema escuro corporativo monocromático, com variações de preto, branco e cinza, e separa as automações por aplicação e por utilitários rápidos.

Versão atual: **2.11.0**
Responsável: **Marcus Silva - Teresina**

## Automações disponíveis

A interface possui uma barra lateral para selecionar **Softshop Caixa**, **Softcom Backup**, **Utilitários** ou **Central Downloads**. Cada página exibe suas próprias opções. O log permanece disponível na área inferior da janela durante todo o processo.

O botão global **Parar execução** fica disponível enquanto uma automação está ativa. Ele cancela downloads, interrompe o processo externo iniciado pela central e impede a execução das etapas seguintes. Arquivos de download ou extração incompletos são removidos. Fechar a central pelo `X` durante uma automação solicita essa mesma parada e aguarda a rotina encerrar antes de fechar a janela. A parada não desfaz automaticamente alterações que já tenham sido concluídas por um instalador ou script.

| Automação | Finalidade |
| --- | --- |
| Reset Completo / Instalação do PDV | Inicia o download em paralelo à captura. Só desinstala e renomeia a instalação e o LocalAppData após o novo pacote estar validado e o MSI extraído. Sem PDV existente, segue em instalação nova. |
| Reinstalação Limpa | Inicia o download em paralelo à desinstalação e instala a versão selecionada. Sem capturas, backups, renomeação de pastas, atualização de DLLs ou abertura automática do PDV. |
| Atualização do SetupSoftcomDLLs | Opção exclusiva do Reset Completo que remove versões anteriores e instala o pacote mais recente antes da instalação do caixa. |
| Atualização do SoftcomBackup | Extrai e executa o script PowerShell oficial incorporado ao EXE. |
| Ajuste Update RDP | Executa o ajuste de confirmação de dados do cliente RDP no Windows 11. |
| Reinicialização do Softconnect | Finaliza o processo atual e abre o Softconnect novamente. |
| Limpeza da fila de impressão | Reinicia o serviço Spooler e remove documentos travados na fila. |

Os scripts PowerShell e Batch são incorporados ao executável sem alterações. Durante a execução, eles são extraídos para `C:\Softcom\ResetCaixa\Scripts` e abertos em uma janela administrativa visível, preservando as mensagens e pausas dos arquivos originais.

## Fluxo do PDV

Na página **Softshop Caixa**, selecione **Reset Completo / Instalação do PDV** ou **Reinstalação Limpa**. Nos dois modos, o download da versão selecionada é a primeira etapa do atendimento e ocorre em background, com recursos nativos do .NET Framework. A preparação valida o arquivo, confere o SHA-256 quando cadastrado e extrai `SetupSoftshopFrenteLoja.msi`. Download, SHA-256 e extração são executados uma única vez por atendimento; a instalação reutiliza o MSI já preparado.

### Reset Completo

1. Inicia a preparação do pacote do PDV em background e encerra os processos `Softshop.exe`.
2. Localiza a instalação existente nos caminhos de 32 e 64 bits e abre o Softshop Caixa.
3. Localiza o campo **Senha**, preenche a senha de suporte e envia `F11`, sem clicar em **Logar**.
4. Trata o aviso de RPC conforme a rotina existente, percorre as abas e subabas e salva os prints das configurações.
5. Exibe as senhas das telas SQL Server e E-Mail quando possível e salva as credenciais Pix e Quero Bônus em `Credenciais_APIs_Softcom.txt`.
6. Encerra o Softshop e aguarda a preparação do pacote caso ainda esteja em andamento. Se o pacote ficar pronto durante a captura, registra isso e continua capturando normalmente.
7. Somente após confirmar download, SHA-256 quando aplicável e MSI extraído, desinstala todas as entradas registradas de **Softshop Caixa**.
8. Preserva por renomeação a pasta da instalação e `%LOCALAPPDATA%\Softcom Tecnologia` e `%LOCALAPPDATA%\Softcom_Tecnologia`. Usa os sufixos `_`, `_1`, `_2` etc., sem sobrescrever backups; mantém as novas tentativas por até 30 segundos em caso de bloqueio temporário.
9. Quando selecionado, atualiza `SetupSoftcomDLLs`.
10. Instala silenciosamente o MSI já preparado, sem repetir o download.
11. Abre a nova instalação, preenche a senha, envia `F11` e deixa as Configurações abertas.
12. Abre a pasta dos prints e do log.

Se o download, o SHA-256 ou a extração falharem, a captura pode terminar, mas o PDV existente **não é desinstalado**. O log e a mensagem final informam: “Não foi possível preparar o novo pacote do PDV. A instalação atual foi mantida.”

Quando `Softshop.exe` não existe nos caminhos verificados, o Reset entra em **instalação nova**: pula captura, desinstalação e renomeação de pastas, aguarda o pacote, respeita a atualização opcional das DLLs e instala e abre o caixa nas Configurações.

### Reinstalação Limpa

1. Inicia a preparação do pacote em background.
2. Encerra os processos `Softshop.exe`, verifica os caminhos da instalação e desinstala todas as entradas registradas de **Softshop Caixa**, enquanto o pacote é preparado.
3. Aguarda o resultado da preparação, incluindo validação do arquivo, SHA-256 quando cadastrado e extração do MSI.
4. Instala a versão selecionada e informa a conclusão.

A Central não abre o PDV antigo ou o novo, não preenche senha, não envia `F11`, não tira prints e não salva credenciais. Não faz backup, não renomeia ou move a pasta da instalação e não altera as duas pastas do LocalAppData. Não atualiza `SetupSoftcomDLLs` e não abre pasta de prints.

O checkbox de DLLs fica desabilitado nesse modo, mantendo sua seleção para quando o técnico voltar ao Reset Completo. O log da Reinstalação Limpa fica separado dos prints do Reset.

É intencional que a desinstalação aconteça antes de o pacote ficar pronto. Se a preparação falhar após essa etapa, o técnico é informado e nenhum arquivo inválido é instalado.

### Cancelamento da preparação em paralelo

**Parar execução** e o fechamento da janela cancelam o workflow e a preparação do pacote. Arquivos parciais de download ou extração são removidos, e a instalação seguinte não começa. Se outra etapa do workflow falhar, a preparação em andamento também é cancelada. A Central aguarda a tarefa de preparação encerrar antes de liberar outra execução ou fechar, evitando tarefas órfãs e dois downloads no mesmo arquivo.

Antes de cada preparação, apenas as saídas antigas `PDV_SoftshopCaixa_Selecionado.zip`, `PDV_SoftshopCaixa_Selecionado.rar` e `SetupSoftshopFrenteLoja.msi` são removidas da pasta `C:\Softcom\ResetCaixa\Download`.

### Caminho do PDV por arquitetura

O caminho não é fixado no código. A rotina sempre testa `C:\Program Files\Softcom Tecnologia\Softshop Caixa\Softshop.exe` e `C:\Program Files (x86)\Softcom Tecnologia\Softshop Caixa\Softshop.exe`. Em Windows de 32 bits, prioriza `Program Files`; em Windows de 64 bits, prioriza `Program Files (x86)`. Diretórios inexistentes são simplesmente ignorados. No Reset Completo, a localização prioritária encontrada é usada para a captura; se houver cópias nas duas pastas, ambas são registradas no log e preservadas por renomeação após a desinstalação. Depois da instalação, os dois caminhos são verificados novamente para abrir o caixa. A Reinstalação Limpa não renomeia pastas nem abre o caixa.

### Roteiro de capturas do Reset Completo

- Configurações Iniciais: Web Service ou SQL Server;
- Tela de Vendas;
- Funções;
- TEF;
- Balança;
- NFC-e/NFe: NFC-e e NF-e;
- Impressões;
- E-Mail;
- Restaurante/Delivery: Configurações Gerais, Parâmetros de Impressão e Configurações Iniciais;
- Funções Caixa;
- Outros;
- Integrações APIs Softcom: Pix, Quero Bônus e SoftDelivery;
- Integrações APIs Delivery: IFood e Meu Carrinho.

Abas opcionais que não existirem são registradas no log e ignoradas. Os nomes são comparados sem considerar acentos, espaços, hífens ou diferenças entre maiúsculas e minúsculas.

## Identificação do campo Senha

Desde a versão 2.2.1, o campo é localizado usando, nesta ordem:

1. propriedade de campo protegido fornecida pelo Windows;
2. nome técnico, identificador ou associação com o rótulo **Senha**;
3. alinhamento visual com o rótulo **Senha**;
4. segunda linha lógica da tela como último recurso.

O `Edit` interno do `ComboBox` de usuário é descartado antes da análise das linhas. Isso evita que a senha seja escrita no campo Usuário em máquinas com DPI, escala ou tema diferentes e também contempla telas que possuem o terceiro campo **Turno**.

## Seleção da versão do PDV

Os dois modos possuem um `ComboBox` para escolher a versão que será instalada. A opção **Mais recente disponível (servidor atual)** mantém o endereço tradicional, e as seguintes versões estão disponíveis pelo GitHub Releases, nesta ordem:

- 8.40.3.0;
- 8.40;
- 8.39.4;
- 8.38.2;
- 8.38.0.0;
- 8.37.3;
- 8.37.2;
- 8.36.0;
- 8.35.1.0;
- 8.34.14.0;
- 8.33.8.

Os pacotes do GitHub são ZIPs e têm o SHA-256 validado antes da extração. O MSI é localizado pelo nome `SetupSoftshopFrenteLoja.msi`, mesmo quando estiver dentro de uma subpasta do arquivo compactado. Se o hash divergir, a instalação é cancelada.

A versão **8.40.3.0 (GitHub)** utiliza o pacote `https://github.com/vinnivii/PDVs/releases/download/pdvs2/PDV_SoftshopCaixa.8.40.3.0.zip`, com SHA-256 `96ae04de6b32615c89975e9ec7aa590f4d98ec6b94aafd7f6335510036db06c3`.

## Central Downloads

A **Central Downloads** reúne 11 atalhos: Backup Utility, Softshop Caixa (PDV), Setup DLLs, Emissor, Nuvem Fiscal, Setup Softshop, SQL Server 2014, QR Code DLL, SPED.NET, Office 2003 e WinRAR.

Cada botão **Baixar** apenas abre a URL cadastrada no navegador padrão do Windows. O navegador realiza o download e decide onde salvar o arquivo. Essa página não baixa arquivos internamente, não cria pastas, não controla progresso e não inicia uma automação. Se não for possível abrir o link, a Central exibe uma mensagem de erro.

Os nomes e links são compilados no próprio EXE, sem arquivo externo de configuração. Não há consultas de rede ou verificação de disponibilidade na abertura da aplicação; o endereço só é aberto quando o técnico clica.

## Stack do projeto

| Camada | Tecnologia |
| --- | --- |
| Linguagem | C# |
| Plataforma | .NET Framework 4.8 (`net48`) |
| Interface desktop | Windows Forms |
| Automação de interface | Microsoft UI Automation (`UIAutomationClient` e `UIAutomationTypes`) |
| Integração nativa | Win32/PInvoke para ativação de janelas, teclado e mouse |
| Imagens | `System.Drawing`, `Graphics.CopyFromScreen` e saída PNG |
| Compactação ZIP/RAR | SharpCompress 0.50.4 |
| Executável único | Costura.Fody 6.2.0 para incorporar dependências gerenciadas |
| Referências do framework | Microsoft.NETFramework.ReferenceAssemblies 1.0.3, somente durante o build |
| Registro do Windows | `Microsoft.Win32`, consultando as visões de 32 e 64 bits |
| Instalação e desinstalação | Windows Installer por `msiexec.exe` |
| Downloads das automações | `System.Net.WebClient`, com cancelamento e limpeza de arquivos parciais |
| Atalhos da Central Downloads | `Process.Start` com `UseShellExecute=true`, abrindo o navegador padrão |
| Scripts administrativos | Windows PowerShell 5.1 e Batch/`cmd.exe`, incorporados como recursos |
| Build | .NET SDK, MSBuild e NuGet |
| Plataforma de saída | AnyCPU, com `Prefer32Bit=false` |
| Privilégio | Manifesto com `requireAdministrator` |

## Estrutura principal

```text
SoftcomSupportAutomation/
├── Program.cs                         # Interface e fluxos de automação
├── SoftcomSupportAutomation.csproj    # Configuração, versão e dependências
├── app.manifest                       # Execução obrigatória como administrador
├── Scripts/
│   ├── Atualizar-SoftcomBackup-v6.ps1
│   ├── AJUSTE_UPDATE_RDP.bat
│   ├── Finalizar e Reiniciar o Softconect.bat
│   └── LimparFilaDeImpressao.bat
└── README.md
```

## Pastas geradas no cliente

| Caminho | Conteúdo |
| --- | --- |
| `C:\Softcom\ResetCaixa\Prints\<data-hora>` | Prints do PDV, credenciais copiadas e `processo.log`. |
| `C:\Softcom\ResetCaixa\ReinstalacaoLimpa\<data-hora>` | Somente `processo.log` da Reinstalação Limpa, sem prints ou credenciais. |
| `C:\Softcom\ResetCaixa\Download` | Pacote ZIP/RAR e MSI do Softshop Caixa. |
| `C:\Softcom\ResetCaixa\DLLs` | RAR e MSI do SetupSoftcomDLLs. |
| `C:\Softcom\ResetCaixa\SoftcomBackup\<data-hora>` | Log da atualização do SoftcomBackup. |
| `C:\Softcom\ResetCaixa\Utilitarios` | Logs dos utilitários rápidos. |
| `C:\Softcom\ResetCaixa\Scripts` | Scripts extraídos do executável durante o uso. |

## Requisitos do computador cliente

- Windows 7 SP1, Windows 10 ou Windows 11, em 32 ou 64 bits;
- .NET Framework 4.8 instalado;
- execução como administrador;
- sessão e área de trabalho desbloqueadas durante a automação visual;
- acesso aos endereços de download dos instaladores;
- quando o Softshop já estiver instalado, ele deve usar a estrutura padrão `Softcom Tecnologia\Softshop Caixa` dentro do `Program Files` correspondente à arquitetura; se não estiver instalado, a rotina segue em modo de instalação nova.

A automação visual do Reset Completo controla a janela ativa. Durante as capturas e a abertura da nova instalação, o técnico não deve utilizar teclado ou mouse; o botão **Parar execução** é a única interação prevista durante esse trecho. A Reinstalação Limpa não executa automação visual do PDV.

## Compilação

No computador de desenvolvimento, é necessário ter o .NET SDK e acesso ao NuGet:

```powershell
dotnet build .\SoftcomSupportAutomation\SoftcomSupportAutomation.csproj -c Release
```

Saída principal do build:

```text
SoftcomSupportAutomation\bin\Release\net48\ResetadorDePdvV2.exe
```

Na release **v2.11.0**, o executável da saída acima é disponibilizado com o nome `ResetadorDePdvV2.11.0.exe`.

Graças ao Costura.Fody, as dependências gerenciadas são incorporadas ao executável, assim como os scripts existentes. No cliente, copie somente o `ResetadorDePdvV2.11.0.exe` baixado da release, sem DLLs ou arquivos de configuração ao lado dele. A Central permanece portátil, sem instalação própria; o .NET Framework 4.8 continua sendo um requisito do Windows.

## Observações de segurança

- A senha de suporte está incorporada ao código conforme o requisito de automação. Ela pode ser recuperada por alguém com acesso ao executável, portanto a distribuição deve ficar restrita aos técnicos autorizados.
- Os arquivos com Cliente ID e Cliente Secret podem conter dados sensíveis e devem ser armazenados e descartados conforme a política da empresa.
- As rotinas modificam programas instalados, pastas, serviços e o Registro do Windows; por isso o executável exige privilégios administrativos.
- A elevação administrativa não remove bloqueios de arquivos nem substitui permissões ACL explícitas. Caso uma pasta continue negando acesso após 30 tentativas, o log informa que ela deve ser verificada quanto a processos, antivírus ou permissões específicas.

## Defender e assinatura digital

Não existe um mecanismo legítimo que garanta que um executável administrativo nunca gere alerta no Microsoft Defender ou no SmartScreen. Para distribuição corporativa:

1. assine todas as versões com um certificado Authenticode confiável e carimbo de tempo SHA-256;
2. mantenha a mesma identidade de assinatura para acumular reputação;
3. distribua por um canal corporativo confiável;
4. envie falsos positivos para o portal oficial da Microsoft;
5. não desative o Defender nem crie exclusões globais como parte da aplicação.
