# Validação v2.13.5 — Configurações modais do Softshop

## Ocorrência de 09/11/2026

O `processo.log` fornecido registra a Central **2.13.4.0**, SHA-256 `24330d3ac22eaa7165f7d89a5a9266bd1808ac4d50e63b6fcca17c70be34a744`, executando Reset Completo em Windows/processo de 64 bits. O PDV instalado era **8.37.3.0**, em `Program Files (x86)`, PID **3592**; a versão selecionada para instalar era **8.38.0.0**.

O pacote de 109.315.280 bytes foi baixado, validado por SHA-256 e extraído em **4,6 s**. A Central encontrou o campo `txtSenha` protegido e enviou F11. Não identificou as Configurações e, antes de repetir o atalho, encontrou o login indisponível/bloqueado por outra janela. A captura falhou em **26,5 s** e o atendimento terminou em **26,9 s**, antes de desinstalar, renomear pastas ou instalar.

O log não identifica a janela que bloqueava o login e não registra fechamento de aviso Avalonia. Portanto, não comprova se havia Configurações abertas ou um aviso do PDV.

## Defeito reproduzido e correção

A busca anterior usava apenas `RootElement` / `TreeScope.Children`. Uma janela modal pode aparecer como descendente do login na árvore UI Automation; nesse caso, o login desabilitado era descartado e o painel ficava fora da busca. Uma fixture WinForms com `ShowDialog(login)` reproduziu na versão anterior a mesma exceção: “A tela de login está indisponível ou bloqueada por outra janela do Softshop.”

- A busca inclui as janelas descendentes das raízes do PID iniciado, sem varrer todos os descendentes da área de trabalho ou aceitar processos indiscriminadamente.
- Configurações precisam ser uma janela visível/habilitada desse PID. Avisos Avalonia com OK não são aceitos como painel, inclusive quando contêm abas.
- Se o login estiver bloqueado, aguarda o painel com timeout e cancelamento, sem enviar senha ou F11 à janela bloqueada.
- Os monitores existentes de Avalonia e RPC reutilizam a mesma busca para encontrar seus avisos modais. Os filtros de mensagem e processo são preservados; avisos desconhecidos não são fechados.
- Em falha, registra PID, HWND, tipo, visibilidade e habilitação das janelas. O diagnóstico não coleta títulos, textos ou valores de campos e não substitui a exceção original.
- A ordem do workflow, captura, download antecipado, validação do pacote e proteção contra desinstalação após falha na captura permanecem preservadas.

Referência: a [documentação de UI Automation da Microsoft](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.automationelement.findall) define o escopo relativo ao elemento e recomenda procurar raízes com `Children`, evitando uma varredura de todos os descendentes do desktop.

## Validação automatizada

```powershell
dotnet build .\SoftcomSupportAutomation\SoftcomSupportAutomation.csproj -c Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Distribution.ps1 -SkipBuild
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File .\tests\Verify-Portable.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Verify-Workflow.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Verify-Distribution.ps1
```

As fixtures incluem Configurações modais com título e sem título, carregamento em sete segundos, aviso desconhecido, Avalonia com abas que não pode ser confundido com Configurações, RPC e cancelamento durante a espera. Conferem isolamento por PID, ausência de F11 sobre login bloqueado, fechamento somente dos avisos conhecidos e encerramento de cada processo de teste.

Também são executados os casos existentes de Avalonia, perfis personalizado/legado do remoto, EXE isolado, metadados/UI/User-Agent, quatro scripts incorporados, ZIP/RAR, download de 10 MiB em blocos, retry seletivo com três tentativas, falha durante transferência, timeout, cancelamento e distribuição Unicode com hash idêntico. Os três fluxos simulados preservam a instalação e o LocalAppData quando a captura falha e cancelam a preparação paralela.

Build Release: **zero erros**, com os **três avisos MSB3277 preexistentes**. Versão gerada da assembly/arquivo: **2.13.5.0**. Saída de distribuição: **`dist/CentralAutomação(2.13.5).exe`**, somente o EXE, sem nova dependência.

No build local, origem e cópia têm **1.999.872 bytes** e SHA-256 `662f9081bf9671627fafc11022a553f55c4aa47b625e905b7ae4382b536ae693`. A transferência de 10 MiB em 160 blocos levou **1.770 ms** (11,06 ms/bloco), os cancelamentos de download responderam em **2–62 ms** após o sinal e o timeout de leitura de teste de 200 ms encerrou em **229 ms**. Hashes de builds em outras revisões/ambientes são conferidos entre origem e cópia da própria execução.

## Limites

Os testes de janela são fixtures. Não foi reproduzida a ocorrência real no PDV 8.37.3.0 do cliente nem executado Reset, F11/captura ou instalação real nesta revisão. A correção resolve o defeito reproduzível de localização de janelas; não confirma qual janela estava aberta naquela máquina. Um aviso desconhecido ou uma falha persistente do PDV continua interrompendo a captura com a instalação atual preservada.

Nenhuma release/tag foi publicada nesta correção. As releases históricas e o instalador do Softhecom Remoto permanecem inalterados. As exclusões locais anteriores de `VALIDACAO-v2.13.0.md` e `VALIDACAO-v2.13.1.md` ficam fora do commit.
