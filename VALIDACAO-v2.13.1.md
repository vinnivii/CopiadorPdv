# Validação v2.13.1 — senha permanente do HopToDesk

Data: 05/11/2026. Ambiente: Windows 64 bits, .NET Framework e Windows PowerShell 5.1.

## Alteração

- Campo inicialmente vazio e oculto, checkbox marcado por padrão e botão Mostrar/Ocultar.
- Validação da senha antes de iniciar a automação; checkbox desmarcado mantém a instalação sem configuração de senha.
- Comando oficial `--password` executado diretamente no aplicativo instalado, sem shell intermediário, com cancelamento e timeout de 30 segundos.
- Senha mantida somente em memória pela Central. Campo limpo ao iniciar e finalizar; buffer zerado em sucesso, falha, cancelamento e recusa da reinstalação. Sem argumentos ou saídas do processo no log.
- Exige confirmação do comando e verifica a presença da senha nas configurações nativas do usuário e do serviço, sem descriptografar ou regravar o valor.
- Verifica opções nativas de acesso não supervisionado. Quando não confirmado, abre o aplicativo com aviso e orientação para conferir/ativar esse acesso; não anuncia que uma conexão remota já foi validada.
- Versão, metadados, User-Agent e ambos os READMEs atualizados para 2.13.1. Sem novos pacotes NuGet ou referências adicionais às da v2.13.0.

## Testes executados

| Verificação | Evidência / resultado |
| --- | --- |
| Executável isolado | Cópia somente do EXE em pasta temporária carregou a interface e os novos controles. AssemblyVersion 2.13.1.0. |
| Estado inicial | Senha vazia e oculta; configuração marcada por padrão. |
| Validação | Campo vazio, somente espaços, quebra de linha e limite de bytes são tratados antes de iniciar. Campo vazio/espaços e limite UTF-8 exercitados. |
| Mostrar/Ocultar | Alterna a máscara e o texto do botão; limpeza restaura a máscara. |
| Checkbox | Desmarcar dispensa senha e desabilita/limpa os controles. |
| Execução ativa | Controles desabilitados durante uma automação e restaurados no fim. |
| Layout | Janela de 1040 × 700 preservada; limites dos controles conferidos; imagem renderizada inspecionada. |
| Argumento literal | Processo inofensivo de teste confirmou, por comparação de hash em memória, o recebimento correto de uma senha aleatória com espaços, aspas, barras finais, Unicode e metacaracteres como um único argumento. |
| Falha e confirmação do comando | Processo de teste sem marcador de sucesso, mesmo retornando zero, foi recusado. Retorno de erro também foi recusado. |
| Sigilo das mensagens | Processo de teste emitiu deliberadamente o segredo aleatório em stdout/stderr. O valor não apareceu no log ou nas exceções apresentadas pela configuração. |
| Cancelamento do processo | Processo inofensivo em espera foi encerrado; não permaneceu processo de teste. |
| Leitura nativa | Leitor exercitado nas opções da instalação existente, somente por leitura. Apenas as seis opções necessárias foram retidas; nenhum valor de senha foi exibido. |
| Parser | Aspas simples/duplas, seção options e recusa de chaves duplicadas verificados com arquivos de teste sem senhas reais. |
| Acesso não supervisionado | Testes confirmaram aviso quando desativado, limitado a senha temporária, dependente de clique, conexões de entrada desativadas, SSO ou acesso por conta. |
| Regressão de UI | Checkbox de DLLs continua habilitado em Reset Completo e Reinstalação Limpa e desabilitado em Prints de Configuração. |

O fluxo de produção do HopToDesk também foi compilado em uma sonda com operações externas simuladas. Foram exercitados: opção sem senha; instalação nova; reinstalação aceita/recusada; configuração confirmada/não confirmada; falha/cancelamento da configuração; arquivo inválido; falha/cancelamento de download; cancelamento de instalação; serviço ausente. A ordem instalação → serviço → senha → abertura foi confirmada. O buffer recebido foi conferido como zerado em todos os caminhos. Acesso não confirmado abriu o aplicativo com aviso, sem mensagem de acesso pronto.

As sondas dos três modos do PDV da v2.13.0 também passaram, incluindo DLLs opcionais, preservação, preparação antecipada e interrupção antes de instalação após cancelamento. São testes com operações externas simuladas, não reinstalações reais.

## Investigação da interface do fabricante

O comando documentado é `--password`; o código oficial consultado pode retornar zero sem confirmar a alteração. Por isso a Central exige o marcador de sucesso e a presença da configuração nativa.

Não foi encontrada opção pública de stdin para configurar a senha. O MCP público não oferece essa operação e o IPC interno recusa processos de outros executáveis. A leitura de opções por esse IPC foi investigada, recusada pelo aplicativo existente e descartada da implementação. Nenhuma tentativa de contornar a proteção faz parte da Central.

O README documenta que o argumento pode ser inspecionado durante a execução e pode ser registrado por ferramentas de auditoria externas. Não é possível prometer que `--password` ficará oculto nos detalhes do processo. A configuração manual na janela nativa evita esse argumento. O próprio HopToDesk persiste a senha nativa criptografada para torná-la permanente; a Central não cria cópias.

Referências: [ajuda oficial](https://help.hoptodesk.com/help), [comando de senha](https://gitlab.com/hoptodesk/hoptodesk/-/blob/main/src/core_main.rs), [proteção do IPC](https://gitlab.com/hoptodesk/hoptodesk/-/blob/main/src/ipc/auth.rs), [configuração nativa](https://gitlab.com/hoptodesk/hoptodesk/-/blob/main/libs/hbb_common/src/config.rs).

## Build e revisão

Comando: `dotnet build .\SoftcomSupportAutomation\SoftcomSupportAutomation.csproj -c Release`.

Resultado: **zero erros**, três avisos MSB3277 das dependências existentes (`System.Memory`, `System.Buffers`, `System.Runtime.CompilerServices.Unsafe`). EXE: `SoftcomSupportAutomation\bin\Release\net48\ResetadorDePdvV2.exe`.

Diff revisado. Os fluxos das demais automações, scripts incorporados, validações de pacotes e cancelamento existentes foram preservados. A reorganização ficou restrita aos cards dos Utilitários; alterações nas versões de User-Agent seguem o versionamento da Central. Alterações anteriores em fontes, bin/obj e arquivos não rastreados foram mantidas.

## Pendências de homologação real

Não foi executado `--password` no HopToDesk desta máquina, não foi alterada sua senha e não foi testada conexão remota. Permanecem pendentes a instalação real com nova senha, a validação após reinício, uma conexão real de outro dispositivo, o comportamento em Windows 32 bits/versões antigas e variantes futuras do HopToDesk. A verificação local é confirmação do comando/presença/opções nativas, sem comparação da senha descriptografada ou prova de autenticação remota. Não há declaração de aprovação desses testes reais.
