# Central de Automações Softcom V2

Aplicação desktop para centralizar rotinas de suporte técnico da Softcom em um único executável administrativo. A interface utiliza um tema escuro corporativo monocromático, com variações de preto, branco e cinza, e separa as automações por aplicação e por utilitários rápidos.

Versão atual: **2.10.0**
Responsável: **Marcus Silva - Teresina**

## Automações disponíveis

A interface possui uma barra lateral para selecionar **Softshop Caixa**, **Softcom Backup**, **Utilitários** ou **Central Downloads**. Cada página exibe suas próprias opções. O log permanece disponível na área inferior da janela durante todo o processo.

O botão global **Parar execução** fica disponível enquanto uma automação está ativa. Ele cancela downloads, interrompe o processo externo iniciado pela central e impede a execução das etapas seguintes. Arquivos de download ou extração incompletos são removidos. Fechar a central pelo `X` durante uma automação solicita essa mesma parada e aguarda a rotina encerrar antes de fechar a janela. A parada não desfaz automaticamente alterações que já tenham sido concluídas por um instalador ou script.

| Automação | Finalidade |
| --- | --- |
| Reset Completo / Instalação do PDV | Quando o PDV existe, captura as configurações, desinstala e preserva por renomeação a pasta da instalação e as duas pastas do LocalAppData. Quando não existe, instala diretamente a versão selecionada. |
| Reinstalação Limpa | Reutiliza o fluxo de captura, desinstalação e instalação do PDV, preservando por renomeação somente a pasta da instalação. As duas pastas do LocalAppData permanecem intactas. |
| Atualização do SetupSoftcomDLLs | Opção adicional dos dois modos do PDV que remove versões anteriores e instala o pacote mais recente antes da instalação do caixa. |
| Atualização do SoftcomBackup | Extrai e executa o script PowerShell oficial incorporado ao EXE. |
| Ajuste Update RDP | Executa o ajuste de confirmação de dados do cliente RDP no Windows 11. |
| Reinicialização do Softconnect | Finaliza o processo atual e abre o Softconnect novamente. |
| Limpeza da fila de impressão | Reinicia o serviço Spooler e remove documentos travados na fila. |

Os scripts PowerShell e Batch são incorporados ao executável sem alterações. Durante a execução, eles são extraídos para `C:\Softcom\ResetCaixa\Scripts` e abertos em uma janela administrativa visível, preservando as mensagens e pausas dos arquivos originais.

## Fluxo do PDV

Na página **Softshop Caixa**, selecione **Reset Completo / Instalação do PDV** ou **Reinstalação Limpa**. Os dois modos compartilham o mesmo fluxo; a diferença está nas pastas preservadas após a desinstalação:

- **Reset Completo:** renomeia/preserva a pasta da instalação e `%LOCALAPPDATA%\Softcom Tecnologia` e `%LOCALAPPDATA%\Softcom_Tecnologia`.
- **Reinstalação Limpa:** renomeia/preserva somente a pasta da instalação. A Central não renomeia, move, apaga, recria ou limpa as duas pastas do LocalAppData; elas permanecem com os mesmos nomes e dados.

O log registra a automação selecionada e, quando há uma instalação existente, informa o tratamento das pastas do LocalAppData.

1. Encerra qualquer processo `softshop.exe` existente.
2. Se o executável instalado existir, abre o Softshop Caixa.
3. Quando há instalação existente, localiza o campo **Senha**, preenche a senha de suporte e envia `F11`, sem clicar em **Logar**.
4. Se o Softshop exibir o aviso **O servidor RPC não está disponível**, valida o título, o texto e o processo de origem e clica em **OK** automaticamente.
5. Quando há instalação existente, percorre as abas e subabas disponíveis e salva uma imagem PNG de cada tela.
6. Exibe as senhas das telas SQL Server e E-Mail quando o respectivo controle existir.
7. Copia Cliente ID e Cliente Secret das integrações Pix e Quero Bônus para `Credenciais_APIs_Softcom.txt`.
8. Quando há instalação existente, encerra o Softshop e desinstala silenciosamente todas as versões registradas como **Softshop Caixa**.
9. Renomeia a pasta remanescente da instalação usando os sufixos `_`, `_1`, `_2` e assim por diante, sem sobrescrever backups anteriores. No Reset Completo, também renomeia as duas pastas do LocalAppData; na Reinstalação Limpa, mantém essas pastas intactas. Se uma pasta ainda estiver bloqueada logo após a desinstalação, repete a operação por até 30 segundos.
10. Quando selecionado, atualiza o `SetupSoftcomDLLs` antes de reinstalar o PDV.
11. Baixa o pacote ZIP ou RAR da versão selecionada, valida o SHA-256 quando cadastrado, extrai somente `SetupSoftshopFrenteLoja.msi` e instala silenciosamente.
12. Abre a nova instalação, preenche a senha, envia `F11` e deixa o painel de Configurações aberto.
13. Abre a pasta que contém os prints e o log do atendimento.

Se `Softshop.exe` não existir nos caminhos verificados no início da rotina, a automação entra no **modo de instalação nova**, independentemente da opção selecionada. Nesse modo, ela não tenta capturar configurações, desinstalar o PDV, renomear a pasta da instalação ou alterar o AppData: respeita a opção de atualização das DLLs, baixa a versão escolhida, instala o caixa e abre a nova instalação diretamente nas Configurações.

### Caminho do PDV por arquitetura

O caminho não é fixado no código. A rotina sempre testa `C:\Program Files\Softcom Tecnologia\Softshop Caixa\Softshop.exe` e `C:\Program Files (x86)\Softcom Tecnologia\Softshop Caixa\Softshop.exe`. Em Windows de 32 bits, prioriza `Program Files`; em Windows de 64 bits, prioriza `Program Files (x86)`. Diretórios inexistentes são simplesmente ignorados. A localização prioritária encontrada é usada para a captura; se houver cópias nas duas pastas, ambas são registradas no log e preservadas por renomeação após a desinstalação. Depois da instalação, os dois caminhos são verificados novamente.

### Roteiro de capturas

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

A automação visual controla a janela ativa. Durante qualquer modo do PDV, o técnico não deve utilizar teclado ou mouse até a conclusão das capturas e da abertura da nova instalação; o botão **Parar execução** é a única interação prevista durante esse trecho.

## Compilação

No computador de desenvolvimento, é necessário ter o .NET SDK e acesso ao NuGet:

```powershell
dotnet build .\SoftcomSupportAutomation\SoftcomSupportAutomation.csproj -c Release
```

Saída principal do build:

```text
SoftcomSupportAutomation\bin\Release\net48\ResetadorDePdvV2.exe
```

Graças ao Costura.Fody, as dependências gerenciadas são incorporadas ao executável, assim como os scripts existentes. No cliente, distribua somente o `ResetadorDePdvV2.exe` da saída acima, sem DLLs ou arquivos de configuração ao lado dele. A Central permanece portátil, sem instalação própria; o .NET Framework 4.8 continua sendo um requisito do Windows.

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
