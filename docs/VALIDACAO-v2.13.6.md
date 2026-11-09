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

Resultados locais da primeira revisão (PDV): **build Release com zero erros e três avisos MSB3277 preexistentes**, portabilidade/instalador, fluxos simulados e distribuição aprovados. EXE de **2.003.456 bytes**, FileVersion/AssemblyVersion **2.13.6.0**; SHA-256 origem/cópia `eebf05e488b931e10569b579f753c4c78d8997a502dfe652754694d9bc0991d3`. O download de 10 MiB em 160 blocos levou **1.750 ms**, os cancelamentos de rede responderam em **2–95 ms** após o sinal e o timeout de leitura de teste de 200 ms encerrou em **212 ms**. O CI confere os hashes de seu próprio build, sem exigir identidade entre revisões/ambientes diferentes.

## Revisão complementar — Softhecom Remoto

O novo log registra download em 1,4 s, assinatura válida e instalação com código 0, seguidos de timeout de 30 s na confirmação do serviço. Essa exceção ocorria antes do comando de senha e da abertura do aplicativo.

Na instalação real 1.46.80 disponível nesta máquina, o serviço se chama **`Softhecom Remoto`**, a pasta/executável são **`Program Files (x86)\Softhecom Remoto\Softhecom Remoto.exe`**, e os arquivos nativos usam **`Softhecom Remoto\config\Softhecom Remoto.toml`** e **`Softhecom Remoto2.toml`**, tanto no AppData do usuário quanto no perfil LocalService. O ProductName do executável é `SofthecomRemoto`, mas esse metadado não é o nome do serviço ou dos arquivos. A Central usava esse nome sem espaços em todos esses locais, causando a confirmação falsa de falha.

- A identidade nativa passa a usar `Softhecom Remoto`, com espaço. Os aliases `SofthecomRemoto` e `HopToDesk` continuam reconhecidos para detecção anterior, sem aceitá-los como prova de sucesso da variante recém-instalada.
- A senha continua opcional, via comando oficial `--password`, usando os caminhos nativos corretos para verificar a presença da configuração criptografada. A Central não salva a senha nem a reutiliza em outra execução.
- A abertura agora aguarda a janela principal do executável esperado na sessão interativa atual. Aceita encaminhamento à instância já existente na bandeja e tenta restaurar/trazer a janela para frente, sem confundir uma conexão remota de outro título com a tela do ID.
- Se a senha não puder ser confirmada, o valor temporário é descartado e o aplicativo ainda é aberto para conferência; a falha continua sendo informada, sem anunciar senha configurada.
- Conforme a [ajuda oficial](https://help.hoptodesk.com/help), a senha permanente é distinta da senha temporária exibida na tela. `--password` continua exigindo argumento inspecionável temporariamente no Windows; a Central descarta stdout/stderr e não registra argumentos. Não foi implementado arquivo de senha/configuração ou protocolo IPC alternativo.

As fixtures acrescentam três perfis/seis caminhos de instalação em 32/64 bits, ImagePath com espaços/aspas, paths TOML com espaço, argumento Unicode com aspas/barras/metacaracteres, confirmação `Done!`, rejeição de código zero sem confirmação, descarte de stdout/stderr que ecoam um segredo sintético e cancelamento do comando. Testam janela correta, título incorreto, encaminhamento à instância existente e cancelamento da espera.

**Verificação real separada dos testes simulados:** o método de produção confirmou o serviço instalado e seu executável em **37 ms**. A abertura/reutilização da janela principal já em execução levou **34 ms**, com janela visível confirmada por UI Automation. Não foram lidos controles de senha, alterada autenticação, reinstalado o produto ou encerradas sessões remotas. O teste não mede abertura fria nem comprova conexão de outro dispositivo.

Build complementar local: **zero erros e os mesmos três avisos**, EXE único de **2.004.992 bytes**, versão **2.13.6.0**, SHA-256 origem/cópia `c3188a40d129c1d8c7ffb0da66b8de75ce88c580a668e5fce19b95a05b5a4ce3`. As correções do PDV desta PR permanecem preservadas.

### Reinstalação real autorizada pelo responsável

Em seguida, o responsável autorizou desinstalar/reinstalar nesta máquina e usar uma senha aleatória temporária, removendo-a ao final. O instalador original foi baixado e validado por SHA-256/assinatura antes da remoção, para garantir disponibilidade da reinstalação.

- O comando oficial `--uninstall` retornou 1; sua conclusão foi verificada pelo estado efetivo: serviço, executável e registro de desinstalação ausentes. A configuração do usuário permaneceu disponível. Esse retorno não foi acrescentado aos códigos de sucesso da Central.
- Reinstalação pelos métodos de produção: **código 0 em 7.471 ms**, serviço confirmado em **3 ms** e janela principal aberta em **773 ms**. Os arquivos nativos do usuário e de LocalService ficaram disponíveis.
- O comando oficial com argumento vazio foi primeiro confirmado como operação válida para limpeza, mantendo o estado inicial sem senha permanente.
- Uma senha aleatória criptograficamente gerada em memória foi aplicada pelo método `ConfigureHopToDeskPassword`: **confirmação em 203 ms**, incluindo marcador de sucesso e presença da senha nativa nos dois perfis.
- No `finally` do teste, o comando oficial removeu a senha temporária. Sua ausência foi verificada no usuário e no serviço; os buffers foram zerados. Nenhuma senha de teste foi exibida ou salva pela Central, pelo script de teste ou pelos relatórios. A exposição temporária dos argumentos a ferramentas do Windows continua documentada.
- O aplicativo permaneceu reinstalado, serviço em execução e janela aberta ao terminar. A opção de acesso não supervisionado **não foi confirmada** pela verificação existente; políticas de aprovação/SSO/acesso não foram modificadas. Não foi testada conexão de outro dispositivo ou reinício.

## Limites

Não foi repetida a instalação real na máquina do cliente. Os retornos MSI são simulados; não foi identificado qual transação gerou o 1618 original. Um Windows Installer permanentemente ocupado ainda precisa ser resolvido no Windows pelo técnico, após o timeout informado. O paralelismo reduz espera quando os pacotes ficam prontos durante a captura, sem garantir uma velocidade específica em cada rede.

O teste da senha temporária comprovou aplicação e remoção pelo comando oficial e pelas configurações nativas. Não comprova conexão remota/reinício ou todas as políticas de acesso. Para uso, o técnico deve informar sua própria senha no campo da Central, sem enviá-la pelo chat; a senha temporária do teste não é reutilizada.

A [PR #4](https://github.com/vinnivii/CopiadorPdv/pull/4) foi integrada a `master` no commit `6daf2e9840c543921aab8854f6ed3b665e3cc93b`. O responsável autorizou também a publicação e a atualização do GitHub. A tag `v2.13.6` identifica o commit distribuído, com build/testes do workflow antes da publicação; a revisão de documentação para os links de download não altera o código funcional integrado.

A [release v2.13.6](https://github.com/vinnivii/CopiadorPdv/releases/tag/v2.13.6) reúne o EXE e o ZIP que preserva `CentralAutomação(2.13.6).exe`. Os hashes do build publicado e dos downloads conferidos ficam nas notas da release, separados dos builds locais registrados acima. Releases históricas, o asset original do instalador remoto e exclusões locais anteriores permanecem inalterados.
