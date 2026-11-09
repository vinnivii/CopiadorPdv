# Central de Automações Softcom V2

Aplicação desktop para centralizar rotinas de suporte técnico da Softcom em um único executável administrativo. A interface utiliza um tema escuro corporativo monocromático, com variações de preto, branco e cinza, e separa as automações por aplicação e por utilitários rápidos.

Versão atual: **2.13.4**
Responsável: **Marcus Silva - Teresina**

## Automações disponíveis

A interface possui uma barra lateral para selecionar **Softshop Caixa**, **Softcom Backup**, **Utilitários** ou **Central Downloads**. Cada página exibe suas próprias opções. O log permanece disponível na área inferior da janela durante todo o processo.

O botão global **Parar execução** fica disponível enquanto uma automação está ativa. Ele cancela downloads, interrompe o processo externo iniciado pela central e impede a execução das etapas seguintes. Arquivos de download ou extração incompletos são removidos. Fechar a central pelo `X` durante uma automação solicita essa mesma parada e aguarda a rotina encerrar antes de fechar a janela. A parada não desfaz automaticamente alterações que já tenham sido concluídas por um instalador ou script.

| Automação | Finalidade |
| --- | --- |
| Reset Completo / Instalação do PDV | Inicia o download em paralelo à captura. Só desinstala e renomeia a instalação e o LocalAppData após o novo pacote estar validado e o MSI extraído. Sem PDV existente, segue em instalação nova. |
| Reinstalação Limpa | Inicia o download em paralelo, desinstala, preserva somente as pastas da instalação por renomeação, atualiza DLLs se selecionado e instala a versão escolhida. Sem capturas, alterações no LocalAppData ou abertura automática do PDV. |
| Prints de Configuração | Abre o caixa instalado, acessa as Configurações via F11, captura as abas suportadas e as credenciais já previstas, fecha o caixa e abre a pasta dos prints. Sem download, desinstalação ou instalação. |
| Atualização do SetupSoftcomDLLs | Opção do Reset Completo e da Reinstalação Limpa que remove versões anteriores e instala o pacote mais recente antes da instalação do caixa. Desabilitada em Prints de Configuração. |
| Atualização do SoftcomBackup | Extrai e executa o script PowerShell oficial incorporado ao EXE. |
| Ajuste Update RDP | Executa o ajuste de confirmação de dados do cliente RDP no Windows 11. |
| Reinicialização do Softconnect | Finaliza o processo atual e abre o Softconnect novamente. |
| Limpeza da fila de impressão | Reinicia o serviço Spooler e remove documentos travados na fila. |
| Softhecom Remoto | Baixa o instalador personalizado pelo GitHub Releases, valida a assinatura, instala silenciosamente e abre o aplicativo para consulta do ID. |

Os scripts PowerShell e Batch são incorporados ao executável sem alterações. Durante a execução, eles são extraídos para `C:\Softcom\ResetCaixa\Scripts` e abertos em uma janela administrativa visível, preservando as mensagens e pausas dos arquivos originais.

## Fluxo do PDV

Na página **Softshop Caixa**, selecione **Reset Completo / Instalação do PDV**, **Reinstalação Limpa** ou **Prints de Configuração**. Nos dois modos que instalam o caixa, o download da versão selecionada é a primeira etapa do atendimento e ocorre em background, com recursos nativos do .NET Framework. A preparação valida o arquivo, confere o SHA-256 quando cadastrado e extrai `SetupSoftshopFrenteLoja.msi`. Download, SHA-256 e extração são executados uma única vez por atendimento; a instalação reutiliza o MSI já preparado.

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

### Aviso de inicialização com Avalonia

A Central acompanha o PID do Softshop desde a abertura, antes do login, e registra a versão real do executável aberto, separada da versão selecionada para instalar. O monitor procura `Avalonia`, sem diferenciar maiúsculas, no título ou texto visível de uma janela desse processo e aciona somente seu botão **OK** visível e habilitado. Não usa o nome da classe gráfica, não fecha avisos de outros processos e respeita a parada. Se o login não aparecer, o Reset Completo interrompe antes de desinstalar e cancela a preparação paralela. Esse tratamento dispensa um aviso; não corrige uma falha fatal de inicialização do PDV.

### Reinstalação Limpa

1. Inicia a preparação do pacote em background.
2. Encerra os processos `Softshop.exe` e registra as pastas da instalação existentes nos caminhos de 32 e 64 bits.
3. Desinstala todas as entradas registradas de **Softshop Caixa** enquanto o pacote é preparado. O aviso conhecido de backup do cache recebe **OK** automaticamente.
4. Encerra processos restantes e preserva somente as pastas da instalação por renomeação, usando os sufixos `_`, `_1`, `_2` etc., sem sobrescrever backups. Mantém as novas tentativas por até 30 segundos em caso de bloqueio temporário.
5. Se **Atualizar SetupSoftcomDLLs antes de instalar** estiver marcado, executa a mesma rotina de DLLs do Reset Completo enquanto o pacote do PDV continua sendo preparado. Os temporários de DLLs e PDV ficam em pastas separadas.
6. Aguarda a preparação do pacote, incluindo validação do arquivo, SHA-256 quando cadastrado e extração do MSI.
7. Instala a versão selecionada e informa a conclusão.

A Central não abre o PDV antigo ou o novo, não preenche senha, não envia `F11`, não tira prints e não salva credenciais. As duas pastas do LocalAppData permanecem intactas. Não abre pasta de prints. Com o checkbox desmarcado, não atualiza `SetupSoftcomDLLs`.

O checkbox de DLLs fica habilitado tanto na Reinstalação Limpa quanto no Reset Completo; em Prints de Configuração continua desabilitado. O log da Reinstalação Limpa fica separado dos prints.

É intencional que a desinstalação e a preservação da pasta aconteçam antes de o pacote ficar pronto. Se a preparação falhar após essa etapa, o técnico é informado e nenhum arquivo inválido é instalado.

### Prints de Configuração

1. Encerra os processos `Softshop.exe` e localiza o caixa instalado nos caminhos de 32 e 64 bits.
2. Reutiliza a mesma captura do Reset Completo: abre o caixa, localiza o campo Senha, preenche a senha de suporte, envia `F11`, percorre as abas suportadas e salva os prints e as credenciais já previstas na rotina.
3. Fecha o Softshop após a captura e abre automaticamente a pasta do atendimento no Explorer.

O botão mostra **Gerar Prints de Configuração**. A seleção da versão do PDV e o checkbox de DLLs ficam desabilitados, preservando suas seleções para os outros modos. Não exige selecionar uma versão e não inicia download, validação de pacote, extração, `msiexec`, desinstalação, instalação ou renomeação de pastas. A Central não altera as pastas da instalação nem as duas pastas do LocalAppData.

Os PNGs, `processo.log` e `Credenciais_APIs_Softcom.txt`, quando gerado pela captura atual, ficam em `C:\Softcom\ResetCaixa\Prints\<data-hora>`. Se não houver Softshop instalado, informa: “Softshop Caixa não foi encontrado. Não é possível gerar os prints de configuração.”

**Parar execução** interrompe as próximas abas e tenta fechar o Softshop. Cancelamento ou erro preservam os prints e as credenciais já gerados e informam o caminho do log. A pasta só abre automaticamente após uma captura concluída e o fechamento do caixa.

Os três modos compartilham o watcher de avisos conhecidos. Ele reconhece a mensagem de backup do cache e o aviso de RPC da rotina existente, mantendo os filtros atuais; outros avisos não são fechados genericamente.

### Cancelamento da preparação em paralelo

**Parar execução** e o fechamento da janela cancelam o workflow e a preparação do pacote. Arquivos parciais de download ou extração são removidos, e a instalação seguinte não começa. Se outra etapa do workflow falhar, a preparação em andamento também é cancelada. A Central aguarda a tarefa de preparação encerrar antes de liberar outra execução ou fechar, evitando tarefas órfãs e dois downloads no mesmo arquivo.

Antes de cada preparação, apenas as saídas antigas `PDV_SoftshopCaixa_Selecionado.zip`, `PDV_SoftshopCaixa_Selecionado.rar` e `SetupSoftshopFrenteLoja.msi` são removidas da pasta `C:\Softcom\ResetCaixa\Download`.

### Downloads em conexão instável e duração das etapas

Os downloads de PDV, DLLs e Softhecom Remoto usam o mesmo helper, com até três tentativas totais. Há nova tentativa somente para falhas de conexão/timeout e HTTP 408, 429, 500, 502, 503 e 504. Os intervalos padrão são de um e dois segundos, sempre canceláveis. Um `Retry-After` válido de até 30 segundos é respeitado; se o servidor pedir mais tempo, a rotina informa o erro sem antecipar outra tentativa. Cada espera por resposta/leitura tem limite de 60 segundos sem progresso, sem impor um tempo total curto ao arquivo que continua chegando.

Arquivos parciais são descartados antes da próxima tentativa. Erros de disco, TLS/confiança, 401/403 e outros erros definitivos não são repetidos. SHA-256, assinatura e conteúdo são validados depois do download; reprovar uma dessas verificações não inicia um novo download. **Parar execução** cancela a transferência ou o intervalo e aguarda a operação de rede terminar antes de liberar outra execução.

O log registra a duração da preparação do pacote, captura, encerramento do Softshop, desinstalação, preservação de pastas, DLLs, instalação e atendimento total, inclusive em falha/cancelamento. A preparação continua paralela: sua duração não deve ser somada à captura para calcular o tempo total. Uma falha ao gravar somente a medição não substitui o resultado original da operação.

### Caminho do PDV por arquitetura

O caminho não é fixado no código. A rotina sempre testa `C:\Program Files\Softcom Tecnologia\Softshop Caixa\Softshop.exe` e `C:\Program Files (x86)\Softcom Tecnologia\Softshop Caixa\Softshop.exe`. Em Windows de 32 bits, prioriza `Program Files`; em Windows de 64 bits, prioriza `Program Files (x86)`. Diretórios inexistentes são simplesmente ignorados. No Reset Completo, a localização prioritária encontrada é usada para a captura; se houver cópias nas duas pastas, ambas são registradas no log e preservadas por renomeação após a desinstalação. Depois da instalação, os dois caminhos são verificados novamente para abrir o caixa. A Reinstalação Limpa registra previamente e preserva as pastas de instalação existentes nos dois caminhos, sem abrir o caixa ou alterar o LocalAppData. Prints de Configuração usa o executável prioritário encontrado para a mesma captura, sem renomear pastas.

### Roteiro compartilhado de capturas

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

Na versão **2.12.1**, a Central confirma o foco de teclado no campo Senha antes e depois do preenchimento. Alterar o texto por UI Automation não garante esse foco e pode deixar o cursor no início; reativar a janela inteira antes de F11 também foi removido dessa sequência. Quando o campo não aceita o preenchimento direto, a digitação substitui o conteúdo anterior. A mesma correção vale para a captura e para a abertura da nova instalação.

Após F11, a Central aguarda as Configurações e permite uma segunda tentativa se o login continuar visível e habilitado. Nessa tentativa, substitui a senha por digitação para contemplar versões que dependem de eventos de teclado. Antes de repetir, verifica se as Configurações já abriram, evitando enviar F11 novamente a um painel aberto. As esperas respeitam **Parar execução**. Se a captura falhar, a instalação atual permanece sem desinstalação e a mensagem orienta verificar a tela de login.

## Seleção da versão do PDV

Reset Completo e Reinstalação Limpa possuem um `ComboBox` para escolher a versão que será instalada. Em Prints de Configuração, esse controle fica desabilitado e a versão não é utilizada. A opção **Mais recente disponível (servidor atual)** mantém o endereço tradicional, e as seguintes versões estão disponíveis pelo GitHub Releases, nesta ordem:

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

## Softhecom Remoto nos Utilitários

Os utilitários existentes ficam à esquerda e o card do **Softhecom Remoto** à direita, mantendo a janela em 1040 × 700. O card oferece **Configurar senha permanente**, marcado por padrão, o campo **Senha permanente de acesso**, inicialmente vazio e oculto (`UseSystemPasswordChar = true`), e o botão **Mostrar/Ocultar**.

Antes de iniciar, preencha a senha ou desmarque o checkbox. O campo é limpo e volta a ficar oculto assim que a execução começa. A senha dessa execução não é reutilizada nas próximas; uma nova senha deve ser digitada a cada atendimento. Desmarcar a opção também limpa o campo.

1. Detecta o serviço, os registros de instalação nas visões de 32/64 bits e os executáveis em `Program Files` e `Program Files (x86)`. Se já houver instalação, pergunta se deseja reinstalar/atualizar; a opção padrão é **Não**. A mensagem avisa quando a senha será substituída pela informada.
2. Baixa sob demanda o instalador personalizado com assinatura do fabricante, hospedado no GitHub Releases, `https://github.com/vinnivii/CopiadorPdv/releases/download/softhecom-remoto-v1.46.80/softhecomremoto-install.exe`, para `C:\Softcom\ResetCaixa\SofthecomRemoto\softhecomremoto-install.exe.partial`. Nenhuma consulta é feita na inicialização da Central.
3. Verifica tamanho, cabeçalhos de executável Windows e assinatura Authenticode confiável de **Begonia Holdings LLC** com `WinVerifyTrust`, usando os certificados locais e o cache do Windows, sem consulta online de revogação. Downloads parciais ou inválidos são descartados.
4. Executa `softhecomremoto-install.exe --silent-install` diretamente em C#, com timeout de cinco minutos e cancelamento, e confirma o executável instalado e o serviço em execução.
5. Se selecionado, executa o aplicativo instalado com o argumento oficial `--password`, passando a senha apenas em memória. Não usa shell intermediário. Aspas, espaços, barras, Unicode e metacaracteres são tratados como parte de um único argumento literal. O limite de 128 bytes UTF-8 segue o armazenamento nativo observado no código do HopToDesk.
6. Exige o marcador de sucesso do comando e verifica, somente por leitura, a presença da senha permanente nas configurações nativas do usuário e do serviço. Não descriptografa, exporta ou regrava o valor. O código de saída zero sozinho não comprova sucesso.
7. Confere as opções nativas de acesso não supervisionado, método de senha, aprovação de conexão, conexões de entrada, SSO e acesso por conta. Se o acesso não for confirmado, informa a pendência e orienta ativá-lo/conferi-lo no aplicativo. Não altera políticas de segurança existentes nem declara o acesso pronto nessa situação.
8. Abre o aplicativo instalado para o técnico conferir o ID e testar uma conexão de outro dispositivo. A verificação local não substitui esse teste real de conexão.

Com **Configurar senha permanente** desmarcado, instala e abre normalmente, sem executar `--password` nem alterar senhas/opções de acesso. Os comandos de instalação e senha seguem a [ajuda oficial do projeto original](https://help.hoptodesk.com/help).

A Central não tem senha fixa do Softhecom Remoto e não salva a senha digitada em arquivos, configurações, logs ou mensagens de erro. Descarta stdout/stderr do comando e zera o buffer temporário ao concluir, falhar, cancelar ou recusar a reinstalação. O próprio Softhecom Remoto mantém sua configuração nativa de senha criptografada, necessária para que ela seja permanente; a Central apenas verifica sua presença. **Parar execução** interrompe o processo iniciado, impede próximas etapas e descarta a senha dessa execução, mas não desfaz uma senha que o aplicativo já tenha aplicado.

**Exposição pela linha de comando:** `--password` exige a senha como argumento. Durante a execução, usuários/processos com permissão de inspeção podem vê-la nos detalhes do processo; ferramentas de auditoria do Windows ou de terceiros também podem registrar a linha de comando. Ocultar a janela e o campo não elimina essa exposição. A Central não exibe nem registra os argumentos.

Foi investigada uma alternativa sem argumento sensível. Na documentação e no código oficial consultados em 05/11/2026 não foi encontrada opção pública `--password-stdin` ou equivalente; o MCP público não oferece configuração da senha local e o IPC interno rejeita outros executáveis. A configuração manual na janela nativa evita o argumento de linha de comando. A automação mantém o comando oficial solicitado, sem tentar contornar o bloqueio do IPC. Referências: [tratamento de `--password`](https://gitlab.com/hoptodesk/hoptodesk/-/blob/main/src/core_main.rs), [proteção do IPC](https://gitlab.com/hoptodesk/hoptodesk/-/blob/main/src/ipc/auth.rs), [armazenamento nativo](https://gitlab.com/hoptodesk/hoptodesk/-/blob/main/libs/hbb_common/src/config.rs).

Os logs ficam em `C:\Softcom\ResetCaixa\SofthecomRemoto\<data-hora>\processo.log`. O instalador é baixado somente no uso e **não é incorporado ao EXE da Central**. A URL fixa do asset está centralizada em `HopToDeskDownloadUrl`, em `HopToDesk.cs`. A variante personalizada usa o serviço, executável e configuração nativa `SofthecomRemoto`; a detecção também reconhece instalações anteriores de `HopToDesk`, mas apenas o serviço personalizado confirma a instalação nova. As pastas e logs antigos não são removidos. O download não exige Dashboard ou login. O arquivo original 1.46.80, com 9.672.072 bytes e assinatura válida de Begonia Holdings LLC, foi publicado e baixado novamente em 06/11/2026; SHA-256 conferido: `86c461206166759a1857817eda648a9b213e806de91ff338fdb0006cbbbffe65`. A release própria do remoto não é marcada como a mais recente da Central. Download, instalação e confirmação do serviço têm durações separadas no log.

Ao finalizar, falhar ou cancelar, a Central tenta remover apenas `softhecomremoto-install.exe` e seu `.partial` da pasta de download, depois de liberar o lock e encerrar o processo iniciado. Arquivo bloqueado ou sem permissão de exclusão gera aviso no log; a limpeza não remove o aplicativo instalado nem seus arquivos de configuração.

Em 05/11/2026, o responsável confirmou aprovação dos testes reais de instalação nova, reinstalação, senha, conexão de outro dispositivo e funcionamento após reiniciar. Esse resultado foi informado pelo responsável, não reexecutado pelo agente; as versões e arquiteturas do Windows não foram especificadas.

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
| Downloads das automações | `System.Net.HttpWebRequest`, com cancelamento, timeout de inatividade e retry seletivo |
| Atalhos da Central Downloads | `Process.Start` com `UseShellExecute=true`, abrindo o navegador padrão |
| Scripts administrativos | Windows PowerShell 5.1 e Batch/`cmd.exe`, incorporados como recursos |
| Build | .NET SDK, MSBuild e NuGet |
| Plataforma de saída | AnyCPU, com `Prefer32Bit=false` |
| Privilégio | Manifesto com `requireAdministrator` |

## Estrutura principal

```text
SoftcomSupportAutomation/
├── Program.cs                         # Interface e fluxos de automação
├── HopToDesk.cs                       # Instalação, detecção e assinatura do Softhecom Remoto
├── HopToDeskPassword.cs               # Campo de senha, execução protegida e verificação local
├── DownloadHelpers.cs                # Download compartilhado, retry e timeout canceláveis
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
| `C:\Softcom\ResetCaixa\Prints\<data-hora>` | Prints do Reset Completo ou de Prints de Configuração, credenciais copiadas e `processo.log`. |
| `C:\Softcom\ResetCaixa\ReinstalacaoLimpa\<data-hora>` | Somente `processo.log` da Reinstalação Limpa, sem prints ou credenciais. |
| `C:\Softcom\ResetCaixa\Download` | Pacote ZIP/RAR e MSI do Softshop Caixa. |
| `C:\Softcom\ResetCaixa\DLLs` | RAR e MSI do SetupSoftcomDLLs. |
| `C:\Softcom\ResetCaixa\SoftcomBackup\<data-hora>` | Log da atualização do SoftcomBackup. |
| `C:\Softcom\ResetCaixa\Utilitarios` | Logs dos utilitários rápidos. |
| `C:\Softcom\ResetCaixa\SofthecomRemoto` | Instalador baixado sob demanda e subpastas com logs do Softhecom Remoto. |
| `C:\Softcom\ResetCaixa\Scripts` | Scripts extraídos do executável durante o uso. |

## Requisitos do computador cliente

- Windows 7 SP1, Windows 10 ou Windows 11, em 32 ou 64 bits;
- .NET Framework 4.8 instalado;
- execução como administrador;
- sessão e área de trabalho desbloqueadas durante a automação visual;
- acesso aos endereços de download dos instaladores;
- quando o Softshop já estiver instalado, ele deve usar a estrutura padrão `Softcom Tecnologia\Softshop Caixa` dentro do `Program Files` correspondente à arquitetura; se não estiver instalado, o Reset Completo segue em modo de instalação nova e Prints de Configuração informa a ausência do caixa.

A automação visual do Reset Completo e de Prints de Configuração controla a janela ativa. Durante as capturas e a abertura da nova instalação, o técnico não deve utilizar teclado ou mouse; o botão **Parar execução** é a única interação prevista durante esse trecho. A Reinstalação Limpa não executa automação visual do PDV.

## Compilação

No computador de desenvolvimento, é necessário ter o .NET SDK e acesso ao NuGet:

```powershell
dotnet build .\SoftcomSupportAutomation\SoftcomSupportAutomation.csproj -c Release
```

Saída interna do build (assembly preservada):

```text
SoftcomSupportAutomation\bin\Release\net48\ResetadorDePdvV2.exe
```

Para compilar e gerar automaticamente a cópia de distribuição:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Distribution.ps1
```

Se o build já foi executado e validado, use `-SkipBuild`. O script lê `<Version>` do projeto, confere os metadados e gera somente **`dist\CentralAutomação(2.13.4).exe`** nesta versão. O formato obrigatório é `CentralAutomação(MAJOR.MINOR.PATCH).exe`, com acentos e parênteses e sem `v`. O SHA-256 deve ser idêntico ao da saída interna. A cópia não altera nome da assembly, versão ou conteúdo do executável.

O script remove apenas cópias anteriores com esse mesmo padrão em `dist/`. Arquivos ou diretórios desconhecidos bloqueiam a preparação sem serem apagados. A pasta final deve conter somente o EXE, sem DLLs, PDBs ou `.config`. Os scripts PowerShell com nomes Unicode usam UTF-8 com BOM para compatibilidade com Windows PowerShell 5.1.

Graças ao Costura.Fody, as dependências gerenciadas são incorporadas ao executável, assim como os scripts existentes. No cliente, copie somente o executável, sem DLLs ou arquivos de configuração ao lado dele. A Central permanece portátil, sem instalação própria; o .NET Framework 4.8 continua sendo um requisito do Windows. O controle de serviços do Softhecom Remoto utiliza `System.ServiceProcess`, já disponível no framework, sem novo pacote NuGet.

## Versionamento e distribuição

A versão da aplicação tem uma única origem: `<Version>` em [SoftcomSupportAutomation.csproj](SoftcomSupportAutomation/SoftcomSupportAutomation.csproj). O SDK gera `AssemblyVersion` e `FileVersion` automaticamente. A interface e todos os User-Agents leem a versão da assembly, sem números de versão repetidos no C#.

| Item | Padrão |
| --- | --- |
| Versão estável | `MAJOR.MINOR.PATCH`, por exemplo `2.13.4`. |
| MAJOR | Mudança incompatível que exige orientação de migração. |
| MINOR | Nova automação ou funcionalidade compatível. |
| PATCH | Correção ou manutenção compatível, sem nova automação. |
| AssemblyVersion / FileVersion | Gerados pelo SDK: `2.13.4.0` neste release. |
| User-Agent | `SoftcomSupportAutomation/2.13.4`, calculado pela aplicação. |
| Tag Git | `v2.13.4`, apontando para o commit que foi compilado e validado. |
| Arquivo para o cliente | `CentralAutomação(2.13.4).exe`, gerado em `dist/` e anexado ao GitHub Release correspondente. |

Para uma nova versão:

1. Altere apenas `<Version>` no projeto e atualize a versão/documentação deste README.
2. Execute o build Release e confira metadados, abertura e extração ZIP/RAR com uma cópia isolada do EXE usando o nome oficial. Gere a distribuição com `scripts/Build-Distribution.ps1 -SkipBuild` e execute `tests/Verify-Distribution.ps1`. Registre os testes reais aplicáveis e suas pendências.
3. Revise o diff e faça o commit dos fontes e da documentação.
4. Integre o PR e crie a tag `vMAJOR.MINOR.PATCH` no commit escolhido. Não publique uma cópia compilada a partir de outra revisão ou com alterações de código pendentes.
5. Envie a tag ao GitHub. O workflow [verify-release.yml](.github/workflows/verify-release.yml) confere a tag contra `<Version>`, compila e executa os testes em Windows. Só depois cria a cópia padronizada, confere os hashes e envia somente essa cópia como artefato `CentralAutomacao-MAJOR.MINOR.PATCH`. O job por tag baixa esse mesmo artefato, confere novamente nome, arquivo único e SHA-256 antes da publicação. Não recompila nem renomeia outro binário. O job de PR tem acesso de leitura; apenas o job de publicação por tag pode criar uma release. Tags/releases históricas não são alteradas. O instalador remoto `softhecomremoto-install.exe` permanece separado.

Depois de preparar `dist/`, os mesmos testes podem ser executados localmente, sem instalar ou alterar serviços:

```powershell
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File .\tests\Verify-Portable.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Verify-Workflow.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Verify-Distribution.ps1
```

O primeiro testa o EXE sozinho com o nome oficial em uma pasta Unicode com espaços/parênteses, recursos, ZIP/RAR, download/retry, cancelamento, limpeza e medições. No CI, recebe explicitamente a saída interna antes de gerar `dist/`, mas a cópia isolada usa o nome oficial e tem SHA-256 conferido. O segundo executa as decisões do fluxo real com operações externas simuladas. O terceiro confere a distribuição e testa preparação em caminho Unicode, limpeza de cópia antiga conhecida, preservação de arquivos inesperados e recusa de EXE com versão divergente do projeto. Esses testes não comprovam instalação, F11/prints ou conexão remota reais. A fixture RAR de 127 bytes tem origem e licença registradas em [tests/fixtures](tests/fixtures/README.md).

`bin/`, `obj/`, `dist/`, PDBs, logs e estado local do Visual Studio ficam fora do Git. A limpeza retirou esses artefatos apenas do índice e preservou os arquivos desta máquina; não reescreveu o histórico antigo. Os executáveis de distribuição devem ser publicados em [GitHub Releases](https://github.com/vinnivii/CopiadorPdv/releases), sem exigir DLLs, configuração ou instalação da própria Central.

O teste de EXE isolado também verifica os perfis personalizado/legado do remoto e usa janelas WinForms artificiais para validar o aviso Avalonia e o isolamento por PID. Os hosts de teste são compilados em uma pasta temporária e encerrados ao terminar; não executam o PDV nem instalam serviços reais.

Este README é a documentação canônica. A [avaliação do relatório e o plano de implementação](docs/PLANO_IMPLEMENTACAO.md) registram as melhorias e sua ordem sugerida. Os resultados e limites desta entrega estão em [VALIDACAO-v2.13.4](docs/VALIDACAO-v2.13.4.md). Abrir ou integrar o PR não publica uma release; a publicação exige o envio da tag correspondente.

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
