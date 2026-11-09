# Avaliação do relatório e plano de implementação

Atualização de 06/11/2026: v2.13.4 implementa Softhecom Remoto via GitHub Releases, reconhecimento do perfil personalizado e fechamento do aviso Avalonia no processo iniciado pela Central. Entrega em novo PR, sem merge ou publicação da Central. Evidências em [VALIDACAO-v2.13.4.md](VALIDACAO-v2.13.4.md); a avaliação histórica abaixo permanece referente à sua base original.

Revisão complementar de 09/11/2026, na mesma PR #2: o nome de distribuição passa a `CentralAutomação(MAJOR.MINOR.PATCH).exe`, gerado de `Version` por `scripts/Build-Distribution.ps1`. `ResetadorDePdvV2.exe` permanece como saída interna. Somente a cópia verificada em `dist/` segue para artefato/release da Central; o instalador remoto e as releases históricas permanecem separados. O teste real de PID do Softshop e os testes simulados estão registrados separadamente na validação da v2.13.4.

Data: 05/11/2026. Base avaliada: `a4564e8` (v2.13.2). Implementação dos itens 1 a 9: v2.13.3, em PR.

## Avaliação

O relatório está bem fundamentado e recomenda a direção correta: pequenas melhorias operacionais, preservando WinForms, .NET Framework 4.8, scripts incorporados e um único EXE. Não há evidência que justifique trocar a stack ou reorganizar toda a aplicação agora.

Conferências realizadas no repositório:

- Os 53 arquivos rastreados em `bin/` e `obj/` somavam **19.519.098 bytes** na árvore do commit. `processo.log` também estava rastreado. Os executáveis rastreados estavam dentro dessas pastas; não havia outro EXE de produção fora delas.
- Os dois READMEs continham a mesma documentação. A versão estava repetida no projeto, na interface e em três chamadas de download.
- Reset Completo e Reinstalação Limpa reutilizam `UpdateSoftcomDlls(msiPath)`. PDV e DLLs selecionadas são preparados antecipadamente, em pastas separadas, antes da captura/desinstalação; as instalações permanecem sequenciais.
- Na base avaliada, o downloader já tratava cancelamento/parciais, mas não tinha tentativas seletivas ou timeout explícito de inatividade. O PR implementa ambos no helper compartilhado.
- A verificação de senha do HopToDesk usa o perfil do processo elevado e a configuração do serviço. O cenário com outro administrador merece teste antes de uma alteração.

Ressalvas ao relatório:

1. **Dificuldade não é prioridade.** Uma tarefa difícil de homologação pode bloquear uma distribuição, enquanto uma limpeza fácil pode ser apenas manutenção.
2. **Não são todos os processos externos que têm timeout.** `RunCommand()` e o comando de senha possuem limite; `WaitForProcessExitWithCancellation()` aguarda até terminar ou cancelar. Scripts com pausas podem depender disso. Não aplicar um limite arbitrário a todos eles.
3. **Abertura isolada não comprova descompactação.** Nesta manutenção foram executadas extrações ZIP e RAR pelo método de produção, somente com o EXE na pasta da aplicação. Passaram nesta máquina; isso não elimina os avisos de binding nem substitui uma máquina limpa.
4. **Homologação foi informada pelo responsável.** Em 05/11/2026 ele confirmou instalação nova, reinstalação, senha, conexão de outro dispositivo e funcionamento após reiniciar. Esses testes não foram repetidos pelo agente. Versões e arquiteturas do Windows não foram especificadas; não declarar aprovação de Windows x86 ou de elevação por outra conta com base nessa confirmação genérica.
5. **Limpar a árvore não limpa o histórico nem reduz o EXE.** Os objetos antigos continuam nos commits anteriores. Não há motivo para reescrever o histórico nesta tarefa.
6. **Dividir `Program.cs` não é uma otimização de desempenho.** Deixar essa refatoração para quando uma mudança concreta precisar dela, começando por uma responsabilidade por vez.
7. **Senha opcional sem valor fixo refere-se ao HopToDesk.** A senha operacional do PDV existente continua deliberadamente incorporada conforme as regras do projeto; não foi removida nem modificada nesta manutenção.

## Ranking — do mais fácil ao mais difícil

Esforço relativo, considerando implementação e validação. P1 é uma verificação importante para distribuição; P2 é evolução incremental; P3 é condicional ou de menor retorno imediato.

| Ordem | Item | Dificuldade | Prioridade | Situação / resultado esperado |
| --- | --- | --- | --- | --- |
| 1 | README canônico e referências | Muito fácil | P2 | Aplicado: documentação principal na raiz; README do projeto aponta para ela. |
| 2 | Ignorar e retirar builds/log do índice | Fácil | P1 | Aplicado localmente: 53 artefatos e um log deixam de ser rastreados; arquivos locais preservados. |
| 3 | Versão única, metadados, UI e User-Agent | Fácil | P2 | Aplicado: `<Version>` é a origem; SDK gera metadados; C# lê a assembly. |
| 4 | Registrar homologação e dados da build personalizada | Fácil para registrar | P1 | Cenários confirmados pelo responsável registrados; faltam versões/arquiteturas do Windows e identificação da máquina. Repetir a conferência após trocar a build do fornecedor. |
| 5 | Padronizar tag e publicação do EXE em Releases | Fácil, com dependência de revisão | P1 | Workflow incluído: verifica PR, valida versão da tag e publica somente o EXE aprovado. O PR não cria tag ou release automaticamente. |
| 6 | Remover instalador temporário após uso | Fácil | P3 | Implementado a pedido do responsável: remove somente instalador/parcial conhecidos após liberar o processo/lock; bloqueio gera aviso. |
| 7 | Verificar ZIP/RAR com EXE isolado e tratar binding se falhar | Média | P1 | Testes reproduzíveis incluídos e executados. Não reproduzida falha de binding; diferenças de referências documentadas, sem atualizar pacotes cegamente. |
| 8 | Medir duração das etapas | Média | P2 | Implementado: fases e total, com sucesso/falha/cancelamento e distinção de tarefas paralelas. |
| 9 | Retry seletivo e cancelável no download | Média | P2 | Implementado no helper compartilhado: três tentativas, erros transitórios, Retry-After limitado e cancelamento durante leitura/intervalo. |
| 10 | Homologar elevação com outro administrador | Média/alta | P2 | Precisa de conta padrão, outra conta administrativa e ambiente controlado. Corrigir somente se o teste demonstrar divergência. |
| 11 | Homologar pacote personalizado no Windows 32 bits | Alta operacional | P1 se x86 é suportado | Exige máquina/VM x86 e teste real. Cabeçalho PE32 sozinho não comprova compatibilidade do Windows ou de dependências. |
| 12 | Separar `Program.cs` gradualmente | Alta pelo custo de regressão | P3 / quando necessário | Adiar a divisão geral. Cada movimentação precisa manter corpo dos métodos, ordem, estado, cancelamento e UI Automation. |

## Etapa A — higiene e versão, aplicada localmente

Alterações:

- `.gitignore` para `bin/`, `obj/`, PDBs, logs e estado local do Visual Studio.
- Retirada apenas do índice dos 54 arquivos de build/log. Uma conferência por SHA-256 confirmou que a limpeza não apagou nem alterou os arquivos locais; o build posterior atualiza suas saídas normalmente.
- README canônico na raiz e links no README do subdiretório.
- `<Version>2.13.3</Version>` no projeto. Overrides redundantes de `FileVersion`/`AssemblyVersion` removidos; metadados gerados pelo SDK.
- `ApplicationVersion` e `AutomationUserAgent` compartilhados, derivados da assembly. Nenhuma chamada de rede nova na abertura.
- Política de versionamento/documentação alinhada em `AGENTS.md` e README: `MAJOR.MINOR.PATCH`, tag `vMAJOR.MINOR.PATCH`, EXE com nome estável em GitHub Releases.

Verificação executada:

| Teste | Resultado e limite |
| --- | --- |
| Build Release obrigatório | Zero erros; três avisos MSB3277 existentes de System.Memory, System.Buffers e System.Runtime.CompilerServices.Unsafe. |
| AssemblyVersion / FileVersion | `2.13.3.0`, gerados a partir de Version. |
| Versão visual e User-Agent | `2.13.3` e `SoftcomSupportAutomation/2.13.3`, conferidos com a assembly compilada e o texto do formulário. |
| EXE isolado | Formulário instanciado com somente o EXE na pasta da aplicação. |
| Scripts incorporados | Quatro recursos presentes e não vazios. Os scripts não foram executados. |
| ZIP | Deflate, entrada em subpasta e bytes conferidos; conteúdo de teste com nome MSI, sem executar instalador. |
| RAR | Arquivo RAR real já existente no repositório; conteúdo extraído pelo método de produção e SHA-256 comparado com extração independente por `tar`. O EXE antigo contido no RAR não foi executado. |
| Cancelamento de extração | Solicitação anterior ao início impediu a extração. |
| Diff | Revisado; fluxos de automação, hash, captura, senha, scripts e pacotes NuGet preservados. |

Não foram alteradas instalações, senhas, serviços ou configurações do Windows. Os dois arquivos `VALIDACAO-v2.13.0.md` e `VALIDACAO-v2.13.1.md` já estavam removidos no diretório antes desta manutenção; essas exclusões preexistentes não foram restauradas ou incluídas na retirada de artefatos do índice.

Saída local interna: `SoftcomSupportAutomation\bin\Release\net48\ResetadorDePdvV2.exe`. A partir da v2.13.4, a cópia para o técnico é `dist/CentralAutomação(Version).exe`; os resultados históricos desta etapa continuam referentes à v2.13.3.

Publicação: revisar e integrar o PR; depois enviar a tag da versão. O workflow compila e testa a revisão marcada antes de criar o Release. Não há publicação de release apenas por abrir/integrar o PR. As duas exclusões locais de relatórios preexistentes continuam fora desta entrega.

## Etapa B — fechar a evidência da distribuição

1. Completar o registro informado pelo responsável com Windows, arquitetura, versão/build do HopToDesk e resultados por cenário, sem senhas, IDs de acesso ou credenciais nos registros.
2. Repetir a extração ZIP e RAR em Windows limpo com somente o EXE. Usar pelo menos um pacote real do atendimento, sem iniciar sua instalação durante o teste de extração.
3. Se houver erro de binding, registrar a exceção e a dependência solicitada; testar a menor correção no conjunto atual. Não atualizar todos os pacotes para silenciar avisos.
4. Publicar a versão validada com tag e notas coerentes, mantendo as releases históricas. Seguir o procedimento do README para que o binário corresponda ao commit marcado. Se a publicação afirmar homologação em x86 ou com elevação por outra conta, concluir os testes correspondentes da etapa D antes dessa afirmação.

Aceite: EXE sozinho abre e extrai os formatos usados; metadados/tag/notas identificam a mesma versão; testes reais e limitações estão separados de testes locais e simulados.

## Etapa C — confiabilidade de rede e observação

Itens C1 e C2 implementados neste PR, mantendo a ordem e as condições dos fluxos. Os testes estão em `tests/` e os resultados em [VALIDACAO-v2.13.3](VALIDACAO-v2.13.3.md).

### C1. Tentativas seletivas de download

- Reutilizar `DownloadFileWithCancellation()`; manter no máximo três tentativas totais, com intervalos curtos e canceláveis.
- Usar uma lista de falhas transitórias: por exemplo timeout/conexão interrompida e respostas 408, 429, 500, 502, 503 e 504. Não tratar todo HTTP 5xx como necessariamente temporário.
- Planejar um limite de inatividade/progresso para que uma transferência travada possa falhar e ser repetida; um timeout total curto não deve interromper um download lento que continua avançando.
- Limpar o parcial antes de outra tentativa e aguardar o download anterior encerrar. Nunca repetir depois de um download já concluído ou por SHA, assinatura, conteúdo ou extração inválidos. Validação e extração continuam fora do retry.
- Não repetir 401/403, erros de confiança TLS ou falhas definitivas. Logar tentativa e motivo de maneira útil ao técnico.
- Manter download antecipado, paths independentes, cancelamento imediato das próximas etapas e a proteção do Reset Completo antes da desinstalação.

Testes mínimos com servidor local controlado: falha temporária seguida de sucesso; limite de três tentativas; 403 sem repetição; cancelamento na transferência e no intervalo; parcial removido; pacote inválido sem retry. Depois, conferir os fluxos de PDV, DLLs e HopToDesk em homologação.

### C2. Medições no log

Usar `Stopwatch` nos limites das fases: preparação do pacote, captura, encerramento, desinstalação, preservação, DLLs e instalação. Registrar um resumo por fase, sem senhas ou varredura detalhada da árvore visual. A preparação paralela tem sua própria duração; não somar tempos sobrepostos como tempo total do atendimento.

Aceite: log mostra onde houve espera e qual fase falhou/cancelou; a ordem e as condições das operações permanecem iguais. Só depois dos dados avaliar redução de esperas na UI Automation.

## Etapa D — matriz de ambiente e mudanças condicionais

1. Testar instalação de HopToDesk partindo de usuário padrão e elevando a Central com outra conta. Identificar quais perfis o próprio comando configura e quais o serviço utiliza. Corrigir o alvo da verificação se houver evidência de falsa falha; não editar manualmente os TOMLs nem procurar senhas de outros perfis.
2. Homologar Windows x86 se a Central continuar sendo distribuída para ele. Registrar arquitetura do pacote, instalação, serviço, senha e conexão real. Não anunciar suporte apenas por o instalador ser um PE32.
3. A exclusão do instalador conhecido já foi autorizada e implementada no item 6. Qualquer limpeza adicional de arquivos temporários continua fora do escopo.

Aceite: resultados por ambiente e perfil documentados; nenhuma alteração de senha ou instalação fora da máquina de homologação; regras de preservação e cancelamento mantidas.

## Etapa E — refatoração somente quando houver necessidade concreta

Ao precisar alterar uma responsabilidade hoje concentrada em `Program.cs`, extrair somente essa parte para uma `partial class`, sem adicionar camadas, interfaces ou DI.

Ordem possível: montagem/navegação da UI → helpers de arquivos/processos → fluxo do PDV → captura visual. Não criar todos os arquivos antecipadamente. Não misturar movimentação de métodos com retry, troca de tempo de espera ou nova funcionalidade no mesmo commit.

Aceite por movimentação: corpo dos métodos e chamadas preservados, build Release, smoke test do EXE isolado e homologação do fluxo realmente afetado. F11, prints, RPC, cache, LocalAppData, sufixos de backup e cancelamento são requisitos, não oportunidades de simplificação.

## O que não fazer agora

- Migrar a stack, adicionar banco/serviço, trazer novas bibliotecas ou embutir o instalador remoto.
- Reescrever o histórico para eliminar os binários antigos, reduzir delays sem medição ou dividir arquivos só para diminuir contagem de linhas.
- Reabrir nomes/serviços/caminhos do instalador personalizado sem evidência de falha, dado que o responsável confirmou o fluxo atual.
- Confundir marcador de sucesso/presença da senha com autenticação remota comprovada ou prometer ocultar argumentos do comando `--password` das ferramentas do Windows.
