# AGENTS.md — Boas Práticas do Projeto CopiadorPdv

Este arquivo define as prioridades e regras que qualquer agente deve seguir ao alterar o projeto **CopiadorPdv / Central de Automações Softcom**.

O objetivo principal do projeto não é ter a arquitetura mais sofisticada possível. O objetivo é entregar uma ferramenta de suporte **rápida, prática, portátil, confiável e simples de distribuir entre os clientes**.

## 1. Prioridades do projeto

Sempre priorize, nesta ordem:

1. Velocidade no atendimento do técnico
2. Redução de cliques e etapas manuais
3. Confiabilidade das automações
4. Executável único e portátil
5. Baixo consumo de recursos
6. Inicialização rápida
7. Compatibilidade com diferentes versões do Windows
8. Código simples de manter
9. Baixo risco de regressão
10. Open source e facilidade de auditoria

Não introduza complexidade arquitetural apenas por considerar uma abordagem “mais moderna”.

## 2. Filosofia da aplicação

A Central é uma ferramenta usada por técnicos de suporte em máquinas de clientes.

Fluxo esperado:

```text
copiar o EXE
→ executar como administrador
→ escolher a rotina
→ resolver o problema
```

Evite qualquer alteração que transforme a Central em algo que precise ser instalado ou configurado antes de usar.

## 3. Executável único

A distribuição deve continuar sendo, preferencialmente:

```text
ResetadorDePdvV2.exe
```

Não tornar obrigatório distribuir ao lado do EXE:

- `.dll`
- `.json`
- `.xml`
- `.ini`
- `.bat`
- `.ps1`
- bancos locais
- arquivos auxiliares

Scripts necessários devem continuar incorporados ao executável quando possível.

## 4. Stack atual deve ser preservada

Manter, salvo necessidade técnica real:

```text
C#
.NET Framework 4.8
Windows Forms
UI Automation
Win32 / PInvoke
SharpCompress
Costura.Fody
```

Não migrar por iniciativa própria para Electron, WebView, MAUI, WPF, Avalonia, React, aplicação web, serviço Windows, SQLite ou outra stack apenas por modernização.

## 5. Dependências

Evite novas dependências NuGet.

Antes de adicionar uma biblioteca, verifique se o problema pode ser resolvido com recursos já disponíveis no .NET Framework e no Windows.

Toda dependência nova deve justificar impacto em:

- tamanho do executável
- inicialização
- manutenção
- portabilidade

## 6. Inicialização rápida

A aplicação deve abrir rapidamente.

Não fazer na inicialização:

- downloads
- consultas ao GitHub
- consultas aos servidores da empresa
- teste de links
- verificação remota de versões
- chamadas HTTP desnecessárias
- processamento pesado

Informações locais rápidas podem ser detectadas, desde que não prejudiquem perceptivelmente a abertura.

## 7. O técnico não deve esperar sem necessidade

Sempre procure oportunidades de executar tarefas independentes em paralelo.

Exemplo:

```text
download do novo PDV
+
captura das configurações atuais
```

podem acontecer ao mesmo tempo.

Não paralelize operações dependentes apenas para economizar alguns segundos.

## 8. Downloads do PDV

Ao iniciar um fluxo que instalará uma versão do PDV, o download do pacote selecionado deve começar o mais cedo possível.

Fluxo preferencial:

```text
iniciar download
→ continuar tarefas independentes
→ aguardar pacote somente quando realmente necessário
→ validar
→ instalar
```

Não baixar o mesmo arquivo duas vezes na mesma execução.

Separar conceitualmente:

```text
Preparar pacote
```

de:

```text
Instalar pacote
```

Preparar pacote envolve:

- download
- verificação de arquivo vazio
- SHA-256 quando disponível
- extração do MSI necessário

A instalação deve reutilizar o MSI já preparado.

## 9. Validação antes de ações destrutivas

Quando o fluxo atual do cliente ainda está funcional, evite destruí-lo antes de saber que o substituto está disponível.

No **Reset Completo**, a instalação atual só deve ser desinstalada depois que o novo pacote estiver:

- baixado
- válido
- com SHA-256 conferido quando disponível
- com o MSI necessário extraído

Assim, uma falha de internet não deixa o cliente sem PDV.

## 10. Reset Completo

Comportamento esperado:

```text
iniciar download em background
→ detectar instalação
→ capturar configurações atuais
→ F11 quando necessário
→ gerar prints
→ salvar informações atualmente coletadas
→ aguardar preparação do pacote se necessário
→ desinstalar
→ preservar pasta da instalação
→ preservar pastas do LocalAppData conforme regra atual
→ atualizar DLLs quando selecionado
→ instalar versão escolhida
→ abrir nova instalação nas configurações
```

O download deve ocorrer em paralelo com a captura sempre que possível.

Não remover etapas do Reset Completo sem solicitação explícita.

## 11. Reinstalação Limpa

A **Reinstalação Limpa** deve ser rápida e objetiva.

Fluxo esperado:

```text
iniciar download em background
→ encerrar Softshop
→ detectar instalação
→ desinstalar Softshop Caixa
→ renomear/preservar a pasta Softshop Caixa encontrada
→ aguardar pacote se ainda estiver baixando
→ validar pacote
→ extrair MSI
→ instalar versão selecionada
→ concluir
```

Na Reinstalação Limpa não executar:

- abertura do PDV antigo para configuração
- preenchimento de senha
- F11
- captura de telas
- prints
- leitura de abas
- coleta de credenciais
- criação de arquivo de credenciais
- alteração das pastas `Softcom Tecnologia` e `Softcom_Tecnologia` do LocalAppData
- atualização de SetupSoftcomDLLs, salvo solicitação explícita

A Reinstalação Limpa deve continuar focada em:

```text
desinstalar
+
preservar pasta da instalação
+
instalar novamente
```

## 12. Detectar instalação em diferentes versões do Windows

Nunca assumir apenas um caminho.

O Softshop Caixa pode estar em:

```text
C:\Program Files\Softcom Tecnologia\Softshop Caixa
```

ou:

```text
C:\Program Files (x86)\Softcom Tecnologia\Softshop Caixa
```

Reutilizar a lógica de detecção existente no projeto.

Antes da desinstalação, registrar quais diretórios realmente existem.

Se houver instalação válida nos dois locais, tratar ambas corretamente.

## 13. Preservação da pasta Softshop Caixa

Após a desinstalação, quando a rotina exigir preservação, manter o comportamento:

```text
Softshop Caixa_
Softshop Caixa_1
Softshop Caixa_2
Softshop Caixa_3
...
```

Nunca sobrescrever backup anterior.

Se a pasta permanecer bloqueada logo após a desinstalação, utilizar tentativas controladas com timeout.

## 14. LocalAppData

Pastas relevantes:

```text
%LOCALAPPDATA%\Softcom Tecnologia
%LOCALAPPDATA%\Softcom_Tecnologia
```

No **Reset Completo**, seguir a regra definida pelo fluxo atual.

Na **Reinstalação Limpa**, não renomear, mover, apagar, recriar, limpar ou modificar essas pastas.

## 15. Central Downloads

A Central Downloads deve permanecer extremamente leve.

Ao clicar em um item:

```text
abrir URL no navegador padrão
```

Preferir:

```csharp
Process.Start(new ProcessStartInfo(url)
{
    UseShellExecute = true
});
```

Não criar downloader interno para essa página sem solicitação explícita.

Não consultar disponibilidade dos links na inicialização.

## 16. UI Automation

Automação visual deve priorizar robustez.

Evite depender exclusivamente de:

- posição fixa
- resolução fixa
- número fixo de pixels
- ordem absoluta de controles

Quando possível, usar:

- `AutomationId`
- `Name`
- `ControlType`
- `IsPassword`
- hierarquia
- posição relativa
- estado selecionado
- validação da janela e do processo

Fallback visual pode existir, mas não deve ser a primeira estratégia.

## 17. Esperas e delays

Evite `Thread.Sleep` longo apenas para “garantir”.

Prefira:

```text
aguardar uma condição
com timeout
```

Exemplos:

- janela apareceu
- processo iniciou
- aba ficou selecionada
- arquivo apareceu
- processo terminou
- controle ficou disponível

Delays pequenos podem ser usados para estabilização visual, mas devem ser mínimos.

## 18. Cancelamento

O botão **Parar execução** é crítico.

Toda nova rotina demorada deve respeitar o cancelamento.

Ao cancelar:

- parar próximas etapas
- cancelar download quando possível
- apagar arquivo parcial
- não iniciar instalação depois do cancelamento
- encerrar processos externos iniciados pela Central quando aplicável
- não deixar thread órfã

Não remover nem contornar `stopRequested`.

## 19. Threads e interface

Nunca manipular diretamente controles WinForms a partir de threads secundárias.

Usar `Invoke`, `BeginInvoke` ou infraestrutura já existente.

Ao introduzir paralelismo:

- evitar duas threads escrevendo no mesmo arquivo
- evitar duas instalações simultâneas
- evitar dois downloads usando o mesmo caminho
- preservar o log thread-safe

## 20. Logs

Logs devem ajudar o técnico a saber:

- o que está acontecendo
- onde parou
- por que falhou
- qual versão foi selecionada
- qual modo foi executado
- qual caminho foi detectado

Exemplos:

```text
Iniciando download antecipado do PDV 8.40.3.0.
Pacote do PDV sendo preparado em paralelo.
Softshop Caixa encontrado em C:\Program Files (x86)\...
Desinstalação concluída.
Preservando pasta da instalação.
Aguardando conclusão do download.
SHA-256 validado.
Instalando versão 8.40.3.0.
Instalação concluída.
```

Evite logs excessivamente técnicos que não ajudem no atendimento.

## 21. Tratamento de erro

Mensagens devem explicar:

1. o que falhou
2. em qual etapa
3. qual é o estado atual da máquina
4. o que o técnico pode fazer em seguida

Exemplo:

```text
Não foi possível preparar o pacote do PDV.
A instalação atual ainda não foi removida.
Verifique a conexão e tente novamente.
```

O código técnico pode continuar no log.

## 22. Evitar regressões

Uma alteração em uma rotina não deve modificar outras sem necessidade.

Ao alterar Reinstalação Limpa, não alterar sem necessidade:

- Softcom Backup
- Softconnect
- RDP
- fila de impressão
- Central Downloads
- captura do Reset Completo

Mantenha o escopo da tarefa pequeno.

## 23. Não reescrever o projeto por estética

Refatore quando isso trouxer benefício concreto:

- reduzir duplicação
- diminuir risco
- facilitar nova função
- separar responsabilidades claramente

Evite:

- Dependency Injection sem necessidade
- camadas artificiais
- interfaces para classes com uma única implementação
- padrões complexos sem benefício

## 24. Performance

Ao implementar uma função, pergunte:

```text
Isso torna o atendimento mais rápido?
```

Procure eliminar:

- download duplicado
- cálculo SHA duplicado
- extração duplicada
- consultas repetidas ao registro
- varreduras repetidas
- esperas fixas desnecessárias
- processos desnecessários
- chamadas de rede evitáveis

Performance aqui significa principalmente:

```text
tempo do técnico
+
tempo de indisponibilidade do cliente
```

## 25. Interface

A UI deve ser objetiva.

Priorizar:

- textos curtos
- ações óbvias
- poucos cliques
- botões claros
- feedback de execução
- poucos diálogos

O técnico deve entender rapidamente:

```text
o que será feito
qual versão será instalada
qual automação está selecionada
se algo está rodando
se algo falhou
```

## 26. Compatibilidade

Ao lidar com caminhos, considerar:

- Windows 32 bits
- Windows 64 bits
- `Program Files`
- `Program Files (x86)`
- computadores lentos
- discos lentos
- internet lenta

Não codificar caminhos de forma rígida quando o .NET oferece uma forma de descobrir o diretório correto.

## 27. Versões do PDV

Ao cadastrar versão com SHA-256:

- preservar a validação
- nunca instalar pacote cujo hash não corresponda
- não calcular o hash mais de uma vez
- não remover versões anteriores sem solicitação

## 28. Versionamento da Central

Quando uma alteração funcional relevante for implementada:

- atualizar `Version`
- atualizar `FileVersion`
- atualizar `AssemblyVersion`
- atualizar versão exibida na interface
- atualizar User-Agent quando usado
- atualizar README

## 29. Build obrigatório

Toda alteração de código deve terminar com build Release:

```powershell
dotnet build .\SoftcomSupportAutomation\SoftcomSupportAutomation.csproj -c Release
```

A tarefa não está concluída se houver erro.

Confirmar geração de:

```text
SoftcomSupportAutomation\bin\Release\net48\ResetadorDePdvV2.exe
```

## 30. Testes mínimos após alterações no PDV

### Reset Completo

Confirmar:

- download antecipado
- captura funcionando
- F11 funcionando
- prints funcionando
- validação do pacote
- desinstalação somente no momento correto
- preservação de pastas
- instalação
- cancelamento

### Reinstalação Limpa

Confirmar:

- download antecipado
- ausência de F11
- ausência de prints
- ausência de captura
- desinstalação
- detecção em `Program Files` / `Program Files (x86)`
- renomeação da pasta `Softshop Caixa`
- LocalAppData intacto
- instalação da versão selecionada

### Regressão

Confirmar que continuam funcionando:

- Softcom Backup
- Softconnect
- Ajuste RDP
- fila de impressão
- Central Downloads
- demais versões do PDV

## 31. Git e commits

Antes de concluir:

```text
git diff
```

Revisar o diff e remover mudanças que não pertencem à tarefa.

Evite misturar no mesmo trabalho:

```text
nova funcionalidade
+
refatoração grande
+
mudança visual
+
limpeza geral
```

salvo quando realmente necessário.

## 32. Arquivos de build no repositório

Evite adicionar novos artefatos de compilação ao controle de versão sem necessidade.

Preferir manter fora do Git:

```text
bin/
obj/
*.pdb
```

Se o projeto deliberadamente mantiver um executável compilado no repositório, não alterar essa política sem solicitação.

## 33. Open source

O código deve permanecer legível e auditável.

Evite:

- ofuscação
- lógica escondida em binários externos
- dependência de serviços secretos apenas para a Central funcionar

Informações internas deliberadamente incorporadas ao projeto podem permanecer quando fazem parte da operação definida pelo responsável do projeto.

Não removê-las por iniciativa própria.

## 34. Antes de implementar qualquer tarefa

O agente deve se perguntar:

```text
Qual problema do técnico estou resolvendo?

Essa mudança reduz cliques, espera ou erro humano?

Estou mantendo o executável único?

Estou adicionando dependência desnecessária?

Posso reutilizar código já existente?

Existe risco de quebrar outra automação?

Posso executar alguma etapa em paralelo com segurança?

Estou preservando o cancelamento?

Funciona em Program Files e Program Files (x86)?

O comportamento ficará claro no log?

Tenho como testar sem alterar funcionalidades fora do escopo?
```

## 35. Checklist final obrigatório

- [ ] O objetivo solicitado foi implementado.
- [ ] O fluxo ficou tão simples quanto possível.
- [ ] O executável continua único e portátil.
- [ ] Nenhuma dependência desnecessária foi adicionada.
- [ ] Não foram adicionadas chamadas de rede na inicialização sem necessidade.
- [ ] Downloads não são repetidos.
- [ ] SHA não é calculado desnecessariamente mais de uma vez.
- [ ] O cancelamento continua funcionando.
- [ ] `Program Files` e `Program Files (x86)` foram considerados.
- [ ] Nenhuma rotina fora do escopo foi alterada sem necessidade.
- [ ] Logs explicam as etapas importantes.
- [ ] Erros deixam claro o estado final da máquina.
- [ ] O `git diff` foi revisado.
- [ ] README foi atualizado quando necessário.
- [ ] A versão foi atualizada quando necessário.
- [ ] O build Release terminou sem erros.
- [ ] O EXE final foi gerado.
- [ ] Foram executados testes dos fluxos afetados.

## Regra principal

Quando houver dúvida entre uma solução sofisticada e uma solução simples que atende bem ao suporte:

> **Escolha a solução simples, rápida, confiável e fácil de carregar em um único EXE.**

A Central existe para economizar tempo do técnico e diminuir o tempo de atendimento ao cliente. Toda decisão técnica deve ser avaliada principalmente por esse critério.
