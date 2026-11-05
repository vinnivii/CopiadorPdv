# Validação v2.13.3

Data: 05/11/2026. Escopo: itens 1 a 9 do plano, em PR contra `master`.

## Implementado

- README canônico, `.gitignore`, retirada de 53 artefatos de build e um log apenas do índice.
- Versão única no `.csproj`, metadados gerados pelo SDK, UI/User-Agent derivados da assembly.
- Registro de homologação informado pelo responsável; versões/arquiteturas do Windows continuam não especificadas.
- Workflow Windows de verificação do PR e publicação por tag, com verificação de versão e anexo de somente um EXE.
- Limpeza do instalador/parcial conhecidos do HopToDesk ao terminar a execução, fora do lock e após o processo. Exclusão bloqueada registra aviso.
- Medição das fases do PDV e do atendimento total, sem somar tempos paralelos ou alterar a ordem das operações. Falha de gravação da medição preserva o resultado original.
- Downloader compartilhado em `DownloadHelpers.cs`: três tentativas totais, erros transitórios explícitos, intervalos canceláveis, `Retry-After` respeitado até 30 segundos e limite de 60 segundos por espera sem progresso.

## Testes reproduzíveis

```powershell
dotnet build .\SoftcomSupportAutomation\SoftcomSupportAutomation.csproj -c Release
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File .\tests\Verify-Portable.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Verify-Workflow.ps1
```

O primeiro script copia somente o EXE para uma pasta isolada e usa os métodos da assembly compilada. Os testes de rede usam um servidor TCP/HTTP limitado a `127.0.0.1`. O segundo compila o fluxo de produção com operações externas simuladas. Scripts e fixtures são ferramentas de desenvolvimento; não acompanham o cliente.

Verificações locais aprovadas:

| Área | Cenários |
| --- | --- |
| Portabilidade | AssemblyVersion/FileVersion 2.13.3.0, versão visual e User-Agent; formulário e quatro scripts incorporados disponíveis sem DLL/config ao lado do EXE. |
| Extração | ZIP/Deflate com entrada em subpasta, RAR5 pequeno com texto conhecido e cancelamento anterior ao início; conteúdo extraído conferido. |
| Retry | 503 seguido de sucesso; máximo de três tentativas; conexão truncada seguida de sucesso. |
| Erros definitivos | 401, 403, 404, 501, falha de disco, TrustFailure, SecureChannelFailure e RequestCanceled sem retry. |
| Rate limit | 429 com Retry-After curto, data HTTP, cabeçalho inválido e janela longa; janela longa não foi antecipada. |
| Cancelamento | Token e stopRequested durante transferência e intervalo; parcial removido, sem nova tentativa. |
| Timeout | Operação sem resposta abortada e aguardada; tarefa encerrada antes do retorno. O teste usa um limite menor na mesma função privada para não aguardar 60 segundos. |
| Validação | HTML recebido com sucesso é recusado pelo validador de instalador sem provocar nova requisição. Hash/assinatura/extração continuam fora do loop de retry. |
| Limpeza | Arquivo bloqueado preservado com aviso; depois de liberar o lock, instalador conhecido removido. |
| Medições | Sucesso, falha e cancelamento registrados; falha de escrita do log não substitui a exceção original. |
| Fluxos simulados | Reinstalação Limpa com/sem DLLs; preservação dos dois caminhos e LocalAppData intocado; Reset Completo só desinstala depois do pacote; Prints sem instalação/DLLs; falha/cancelamento impedem instalação posterior. |

## Revisão do download antes do merge

`WaitForDownloadIo<T>` passou a usar `Task.WaitAny` em lugar de `Thread.Sleep(100)`. A espera desperta quando a Task termina ou o token é cancelado, preservando a exceção original em `GetAwaiter().GetResult()`. A checagem de `stopRequested` continua a cada no máximo 100 ms de espera, sujeita ao escalonamento do Windows. Timeout de inatividade de 60 segundos, abort/aguardo da operação e política de retry não mudaram; SHA-256, assinatura e extração não foram alterados.

O teste de regressão serve **10 MiB (10.485.760 bytes)** por HTTP local em **160 blocos de 64 KiB**, liberando o próximo somente depois da gravação do anterior. Isso impede que o buffering esconda a espera fixa por leitura. Todos os bytes são conferidos; o limite do teste é 8 segundos (média abaixo de 50 ms por bloco), com margem para o servidor e o disco. O teste novo falhou com o EXE anterior à correção e passou com a espera baseada na Task.

Medições locais em Windows 10 Pro 10.0.19045, 64 bits:

| Cenário | Resultado |
| --- | --- |
| Espera anterior, `Thread.Sleep(100)` | 17.432 ms; 108,95 ms/bloco; teste de desempenho reprovado. |
| Espera corrigida, `Task.WaitAny` | 1.424 ms; 8,90 ms/bloco; 1 requisição, todos os bytes corretos. |
| Cancelamento durante transferência | Token: 19 ms após o sinal; `stopRequested`: 61 ms; parcial removido, sem retry. |
| Cancelamento durante intervalo de retry | Token e `stopRequested`: 62 ms após o sinal; sem nova requisição. |
| Leitura interrompida por inatividade | Limite de teste de 200 ms observado em 219 ms; requisição abortada e Task encerrada. |
| Transferência truncada | Recuperação na segunda tentativa; falha persistente encerrada na terceira, sem parcial. |

Esses tempos medem um cenário local controlado, não a velocidade dos servidores de produção. Build Release, testes de EXE isolado/ZIP/RAR e fluxos simulados passaram novamente; continuam os três avisos de binding já descritos abaixo. A correção permanece na v2.13.3 ainda em revisão, sem nova dependência ou publicação de release.

## Investigação dos avisos de binding

O build Release mantém três avisos MSB3277. Conferência das identidades das assemblies locais:

| Assembly | Referência de SharpCompress 0.50.4 | Assembly selecionada pelo build |
| --- | --- | --- |
| System.Memory | 4.0.5.0 | 4.0.1.2, pacote 4.5.5 |
| System.Buffers | 4.0.5.0 | 4.0.3.0, pacote 4.5.1 |
| System.Runtime.CompilerServices.Unsafe | 6.0.3.0 | 6.0.0.0, pacote 6.0.0 |

System.Text.Encoding.CodePages 8.0.0 referencia Memory 4.0.1.2 e Unsafe 6.0.0.0. As diferenças explicam os conflitos de referências observados; não foi reproduzida falha de carregamento ou de extração ZIP/RAR no EXE isolado. Não foram adicionados pacotes, redirects externos, supressão de avisos ou atualizações amplas para ocultar o problema. Outros formatos, codificações ou ambientes podem precisar de investigação se surgir uma falha concreta.

## Homologação e limites

O responsável confirmou em 05/11/2026 que instalação nova/reinstalação do cliente remoto, senha, conexão de outra máquina e funcionamento após reiniciar passaram. Isso é uma informação do responsável sobre o fluxo existente, não uma nova execução desses cenários pelo agente nesta versão. Windows e arquitetura não foram especificados.

Nesta implementação não foram executados instaladores reais, F11/prints, alterações de senha/serviço ou conexões remotas. Windows x86, elevação por outra conta e uma nova homologação real dos fluxos alterados permanecem separados dos testes automatizados. Não houve migração de stack, nova dependência NuGet, consulta de rede na inicialização ou mudança de nomes/serviços/caminhos do cliente personalizado.

O PR executa build e testes no GitHub Actions. A criação de uma release continua condicionada ao envio de tag `vMAJOR.MINOR.PATCH` correspondente ao projeto; abrir/integrar o PR não publica a versão nem sobrescreve releases existentes.
