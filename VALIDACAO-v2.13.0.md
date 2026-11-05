# Validação da Central v2.13.0

Data: 05/11/2026. Plataforma da verificação: Windows 64 bits, .NET Framework 4.8 e Windows PowerShell 5.1.

## Build e distribuição

Comando executado:

```powershell
dotnet build .\SoftcomSupportAutomation\SoftcomSupportAutomation.csproj -c Release
```

Resultado: **sucesso, zero erros**. Há três avisos MSB3277 de conflito entre versões de `System.Memory`, `System.Buffers` e `System.Runtime.CompilerServices.Unsafe`, referentes às dependências já utilizadas pelo projeto. Os pacotes NuGet não foram alterados.

EXE gerado: `SoftcomSupportAutomation\bin\Release\net48\ResetadorDePdvV2.exe`. `AssemblyVersion` e `FileVersion`: **2.13.0.0**. O EXE foi copiado para uma pasta temporária isolada e a interface foi instanciada sem DLLs ou arquivo `.config` ao lado dele. Os recursos incorporados foram inspecionados: o instalador HopToDesk não está incorporado.

## Verificações executadas com o código compilado

| Verificação | Resultado |
| --- | --- |
| Checkbox de DLLs no Reset Completo e na Reinstalação Limpa | Habilitado; seleção preservada ao trocar de modo. |
| Checkbox de DLLs e seleção de versão em Prints de Configuração | Desabilitados. |
| Bloqueio dos botões durante uma automação | HopToDesk e utilitários existentes bloqueados junto às demais ações. |
| Interface dos Utilitários | Quatro cards em 2 × 2; limites dos controles conferidos e imagem renderizada inspecionada. Janela mantida em 1040 × 700. |
| Detecção do HopToDesk | Caminhos de Program Files e Program Files (x86) considerados; parsing de caminhos entre aspas e com argumentos verificado. |
| Instalação existente e serviço | Nesta máquina, detectou o executável em Program Files (x86) e confirmou o serviço HopToDesk em execução, somente por leitura. Não reinstalou nem reiniciou o serviço. |
| Instalador oficial baixado | Arquivo de 7.505.800 bytes, versão 1.46.80; aceito pelo validador C# de PE, WinVerifyTrust e editor Begonia Holdings LLC. O instalador não foi executado. |
| Arquivos inválidos | Arquivo vazio, HTML, EXE sem assinatura, EXE de outro editor e cópia adulterada do instalador oficial recusados. |
| Download concluído | Servidor HTTP local de teste; conteúdo recebido corretamente. |
| Falha de download | Resposta HTTP 503 de teste; arquivo parcial removido. |
| Cancelamento durante download | Testado tanto por CancellationToken quanto por stopRequested; download interrompido e arquivo parcial removido. |
| Cancelamento de processo externo | Processo inofensivo de teste iniciado para a verificação foi encerrado pela infraestrutura existente de cancelamento. |

## Fluxos executados com operações externas simuladas

Os métodos de produção `RunWorkflow()` e `RunHopToDeskInstallation()` foram extraídos integralmente dos fontes e compilados em sondas temporárias. Downloads, processos, registro, arquivos e operações que alterariam instalações foram substituídos por simulações. Estes testes verificam decisões e ordem das etapas; **não equivalem a instalações reais**.

| Cenário | Resultado |
| --- | --- |
| Reinstalação Limpa, DLLs desmarcadas | Sem chamada de atualização de DLLs. |
| Reinstalação Limpa, DLLs marcadas | Atualiza depois de preservar as duas pastas de instalação e enquanto a preparação do PDV está em andamento; instala o PDV depois do pacote pronto. |
| Regras da Reinstalação Limpa | Sem captura, LocalAppData, abertura da configuração ou pasta de prints; preserva os dois caminhos de instalação. |
| Reset Completo com DLLs | Mantém captura, preservação incluindo LocalAppData, atualização de DLLs e abertura final; só desinstala após o pacote pronto. |
| Prints de Configuração | Sem preparação, desinstalação, instalação ou atualização de DLLs, mesmo com flag antiga de DLLs verdadeira. |
| Pacote inválido no Reset Completo | Interrompe antes de desinstalar. |
| Cancelamento na captura ou na atualização de DLLs | Impede instalação posterior do PDV e encerra o fluxo. |
| HopToDesk novo ou reinstalação autorizada | Valida antes de chamar exatamente `--silent-install`; verifica serviço antes de abrir o aplicativo. |
| HopToDesk existente, reinstalação recusada | Não baixa nem executa instalador. |
| HopToDesk com download inválido, falha HTTP ou cancelamento do download | Descarta arquivos, impede instalação e libera a interface. |
| Cancelamento do instalador HopToDesk | Interrompe próximas etapas. |
| Serviço HopToDesk não confirmado | Informa erro e não abre aplicativo nem declara sucesso. |

## Revisão e testes pendentes em máquina de homologação

O diff foi revisado. Scripts de Softcom Backup, Softconnect, RDP e fila de impressão, manifesto, catálogo de versões/hashes do PDV, watcher de avisos, captura e rotina existente `UpdateSoftcomDlls()` foram preservados; nessa última rotina mudou somente a versão do User-Agent. A Central Downloads mantém a abertura de URLs no navegador. As mudanças de bin/obj geradas pelo build seguem a política já existente no repositório; havia alterações nesses arquivos antes desta tarefa.

**Não executados nesta máquina:** instalação real do HopToDesk em ambiente sem o aplicativo; atualização real de uma instalação existente; execução real de `--silent-install`; criação de serviço e abertura do aplicativo após essa instalação; desinstalação/reinstalação real de PDV ou SetupSoftcomDLLs; automação real de F11/prints; execução dos scripts de Backup, Softconnect, RDP e fila de impressão; abertura real dos links da Central Downloads; execução em Windows 32 bits e versões antigas do Windows. Esses itens permanecem pendentes de homologação e não foram declarados aprovados.
