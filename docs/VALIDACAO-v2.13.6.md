# Validação v2.13.6 — Windows Installer ocupado e preparação antecipada de DLLs

## Diagnóstico do atendimento de 09/11/2026

O log fornecido registra Reset Completo na Central **2.13.5.0**, Windows/processo de 64 bits, PDV instalado **8.37.3.0** e seleção **Mais recente disponível (servidor atual)**.

- **10:36:49:** o download antecipado do PDV já iniciou, antes da abertura para prints.
- **10:37:36:** pacote de 135.805.656 bytes preparado, após validação de conteúdo/extração RAR. Essa opção do servidor não tem SHA-256 cadastrado; a validação existente foi preservada.
- **10:38:41:** captura terminou; o MSI já estava pronto e foi reaproveitado imediatamente. Foram salvos 11 prints e as credenciais previstas na rotina, sem necessidade de refazer capturas.
- **10:38:53:** PDV desinstalado com código 0; pasta da instalação e as duas pastas do LocalAppData preservadas por renomeação.
- **10:39:05–10:39:32:** download de DLLs começou somente depois da captura/desinstalação, consumindo mais 27 segundos fora desse paralelismo.
- **10:40:11:** DLLs confirmadas no registro; iniciou a instalação do PDV.
- **10:40:15:** o Windows Installer retornou **1618** e a Central encerrou com erro. O PDV antigo já havia sido removido; o MSI preparado e os backups permaneceram disponíveis. O total do atendimento foi **213,9 s**.

A [Microsoft define 1618](https://learn.microsoft.com/en-us/windows/win32/msi/error-codes) como outra instalação em andamento. A falha ocorreu logo após SetupSoftcomDLLs, mas o log não identifica o processo que mantinha o Windows Installer ocupado. Não há evidência de novo erro de captura ou falha no download do PDV. Uma entrada em Programas e Recursos não garante que outra transação MSI possa começar naquele instante.

## Alterações

- Preparação do PDV preservada; preparação das DLLs selecionadas também inicia no começo do atendimento, antes dos prints. Cada pacote tem seus próprios arquivos e é baixado/extraído uma vez por execução.
- `UpdateSoftcomDlls(msiPath)` reutiliza o MSI preparado e valida sua disponibilidade antes de remover DLLs existentes.
- No Reset Completo, ambos os pacotes selecionados precisam estar preparados antes de desinstalar o PDV. Na Reinstalação Limpa, a ordem atual de desinstalar/preservar a instalação enquanto os downloads continuam foi mantida.
- Prints de Configuração e a opção de DLLs desmarcada não iniciam download de DLLs. F11, captura, credenciais previstas, LocalAppData e tratamento de Avalonia não foram modificados.
- Instalações/desinstalações MSI repetem o comando somente após **1618**, com espera cancelável de até um segundo entre tentativas e prazo total de dez minutos por operação. O tempo de execução das tentativas integra esse prazo. Não há espera adicional no caminho de sucesso.
- Códigos 0/1641/3010 continuam aceitos. Outros erros não ganham retry. Um 1618 persistente das DLLs lança erro antes da verificação de registro e não pode ser confundido com instalação confirmada.
- Não são encerradas instalações de outros produtos para liberar o MSI. Cancelamento/timeout continuam encerrando somente o processo iniciado pela Central e seus descendentes, usando o mecanismo existente.
- Em falha de instalação do PDV, a mensagem informa o caminho do MSI mantido; os backups não são apagados. Nenhum novo log detalhado MSI ou credencial foi acrescentado.
- Falha/cancelamento do workflow cancela e aguarda as duas preparações, sem tarefas órfãs ou instalação posterior ao cancelamento. Arquivos parciais são removidos pelos mesmos mecanismos de download/extração.
- Versão alterada somente em `<Version>`: **2.13.6**, metadados derivados **2.13.6.0**, distribuição **`CentralAutomação(2.13.6).exe`**. Sem dependência nova, mudança de stack ou chamada de rede na inicialização.

## Verificação

```powershell
dotnet build .\SoftcomSupportAutomation\SoftcomSupportAutomation.csproj -c Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Distribution.ps1 -SkipBuild
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File .\tests\Verify-Portable.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Verify-Workflow.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Verify-Distribution.ps1
```

Os testes de instalador usam executáveis artificiais que apenas retornam códigos/gravam PID/aguardam; não executam `msiexec` nem alteram produtos, serviços ou registro. Conferem 1618 transitório seguido de sucesso, 1618 persistente com prazo total, cancelamento na espera, parada antes de iniciar, 1603 sem retry, preservação de 0/1641/3010 e encerramento do processo próprio por cancelamento/timeout, mantendo um processo independente vivo. A chamada genérica usada pelo Softhecom Remoto não recebe retry MSI implícito.

Os fluxos simulados usam o método de produção inserido no probe e conferem os três modos: ambos os pacotes iniciados antes da captura, preparação única/reuso dos MSIs, DLLs opcionais, instalação sequencial, LocalAppData intacto na Reinstalação Limpa e proteção antes da desinstalação no Reset Completo. Falhas na captura e no pacote de DLLs, além de cancelamento, verificam que ambas as tarefas são aguardadas e que não ocorre instalação indevida.

Também são executados EXE isolado, metadados/UI/User-Agent derivados, quatro scripts incorporados, ZIP/RAR, download de 10 MiB em blocos, retry de rede seletivo/limite de três tentativas, transferência truncada, timeout/cancelamento, perfis do remoto, janelas Avalonia/modais e distribuição Unicode com apenas o EXE e SHA-256 idêntico ao build.

Resultados locais: **build Release com zero erros e três avisos MSB3277 preexistentes**, portabilidade/instalador, fluxos simulados e distribuição aprovados. EXE de **2.003.456 bytes**, FileVersion/AssemblyVersion **2.13.6.0**; SHA-256 origem/cópia `eebf05e488b931e10569b579f753c4c78d8997a502dfe652754694d9bc0991d3`. O download de 10 MiB em 160 blocos levou **1.750 ms**, os cancelamentos de rede responderam em **2–95 ms** após o sinal e o timeout de leitura de teste de 200 ms encerrou em **212 ms**. O CI confere os hashes de seu próprio build, sem exigir identidade entre revisões/ambientes diferentes.

## Limites

Não foi repetida a instalação real na máquina do cliente. Os retornos MSI são simulados; não foi identificado qual transação gerou o 1618 original. Um Windows Installer permanentemente ocupado ainda precisa ser resolvido no Windows pelo técnico, após o timeout informado. O paralelismo reduz espera quando os pacotes ficam prontos durante a captura, sem garantir uma velocidade específica em cada rede.

Esta correção não publica tag/release nem integra a PR automaticamente. Releases históricas, Softhecom Remoto e exclusões locais anteriores permanecem fora destas alterações.
