# Central de Automações Softcom V2

Aplicação desktop para centralizar rotinas de suporte técnico da Softcom em um único executável administrativo. A interface utiliza um tema escuro corporativo monocromático, com variações de preto, branco e cinza, e separa as automações por aplicação e por utilitários rápidos.

Versão atual: **2.9.0**  
Responsável: **Marcus Silva - Teresina**

## Automações disponíveis

A interface possui uma barra lateral para selecionar **Softshop Caixa**, **Softcom Backup** ou **Utilitários**. Cada produto exibe somente suas próprias automações e configurações. O log permanece disponível na área inferior da janela durante todo o processo.

O botão global **Parar execução** fica disponível enquanto uma automação está ativa. Ele cancela downloads, interrompe o processo externo iniciado pela central e impede a execução das etapas seguintes. Arquivos de download ou extração incompletos são removidos. Fechar a central pelo `X` durante uma automação solicita essa mesma parada e aguarda a rotina encerrar antes de fechar a janela. A parada não desfaz automaticamente alterações que já tenham sido concluídas por um instalador ou script.

| Automação | Finalidade |
| --- | --- |
| Reset ou instalação do Softshop Caixa / PDV | Quando o PDV existe, captura as configurações, desinstala e preserva as pastas. Quando não existe, pula essas etapas e instala diretamente a versão selecionada. |
| Atualização do SetupSoftcomDLLs | Opção adicional do Reset do PDV que remove versões anteriores e instala o pacote mais recente antes da reinstalação do caixa. |
| Atualização do SoftcomBackup | Extrai e executa o script PowerShell oficial incorporado ao EXE. |
| Ajuste Update RDP | Executa o ajuste de confirmação de dados do cliente RDP no Windows 11. |
| Reinicialização do Softconnect | Finaliza o processo atual e abre o Softconnect novamente. |
| Limpeza da fila de impressão | Reinicia o serviço Spooler e remove documentos travados na fila. |

Os scripts PowerShell e Batch são incorporados ao executável sem alterações. Durante a execução, eles são extraídos para `C:\Softcom\ResetCaixa\Scripts` e abertos em uma janela administrativa visível, preservando as mensagens e pausas dos arquivos originais.

## Fluxo do Reset do PDV

1. Encerra qualquer processo `softshop.exe` existente.
2. Se o executável instalado existir, abre o Softshop Caixa.
3. No modo de reset, localiza o campo **Senha**, preenche a senha de suporte e envia `F11`, sem clicar em **Logar**.
4. Se o Softshop exibir o aviso **O servidor RPC não está disponível**, valida o título, o texto e o processo de origem e clica em **OK** automaticamente.
5. No modo de reset, percorre as abas e subabas disponíveis e salva uma imagem PNG de cada tela.
6. Exibe as senhas das telas SQL Server e E-Mail quando o respectivo controle existir.
7. Copia Cliente ID e Cliente Secret das integrações Pix e Quero Bônus para `Credenciais_APIs_Softcom.txt`.
8. No modo de reset, encerra o Softshop e desinstala silenciosamente todas as versões registradas como **Softshop Caixa**.
9. No modo de reset, renomeia as pastas remanescentes usando os sufixos `_`, `_1`, `_2` e assim por diante. Se uma pasta ainda estiver bloqueada logo após a desinstalação, repete a operação por até 30 segundos.
10. Quando selecionado, atualiza o `SetupSoftcomDLLs` antes de reinstalar o PDV.
11. Baixa o pacote ZIP ou RAR da versão selecionada, extrai somente `SetupSoftshopFrenteLoja.msi` e instala silenciosamente.
12. Abre a nova instalação, preenche a senha, envia `F11` e deixa o painel de Configurações aberto.
13. Abre a pasta que contém os prints e o log do atendimento.

Se `Softshop.exe` não existir no caminho esperado no início da rotina, a automação entra no **modo de instalação nova**. Nesse modo, ela não tenta capturar configurações, desinstalar o PDV ou renomear pastas: respeita a opção de atualização das DLLs, baixa a versão escolhida, instala o caixa e abre a nova instalação diretamente nas Configurações.

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

O Reset Completo possui um `ComboBox` para escolher a versão que será instalada. A opção **Mais recente disponível (servidor atual)** mantém o endereço tradicional, e as seguintes versões estão disponíveis pelo GitHub Releases:

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
| Downloads | `System.Net.WebClient`, com cancelamento e limpeza de arquivos parciais |
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

A automação visual controla a janela ativa. Durante o Reset do PDV, o técnico não deve utilizar teclado ou mouse até a conclusão das capturas e da abertura da nova instalação; o botão **Parar execução** é a única interação prevista durante esse trecho.

## Compilação

No computador de desenvolvimento, é necessário ter o .NET SDK e acesso ao NuGet:

```powershell
dotnet build .\SoftcomSupportAutomation\SoftcomSupportAutomation.csproj -c Release
```

Saída principal do build:

```text
SoftcomSupportAutomation\bin\Release\net48\ResetadorDePdvV2.exe
```

Executável preparado para distribuição:

```text
SoftcomSupportAutomation\bin\Release\V2\ResetadorDePdvV2.9.0.exe
```

Graças ao Costura.Fody, as dependências gerenciadas são incorporadas ao executável. No cliente, distribua o `ResetadorDePdvV2.9.0.exe`; o .NET Framework 4.8 continua sendo um requisito do Windows.

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
