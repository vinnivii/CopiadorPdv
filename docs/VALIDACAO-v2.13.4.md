# Validação v2.13.4

Data: 06/11/2026. Entrega: novo PR contra `master`; merge e publicação da Central aguardam revisão.

## Instalador publicado

- [Softhecom Remoto 1.46.80](https://github.com/vinnivii/CopiadorPdv/releases/tag/softhecom-remoto-v1.46.80), sem marcar como a release mais recente da Central.
- Asset original `softhecomremoto-install.exe`, 9.672.072 bytes; assinatura Authenticode válida de Begonia Holdings LLC antes e depois da publicação.
- SHA-256 local e do download pela URL pública: `86c461206166759a1857817eda648a9b213e806de91ff338fdb0006cbbbffe65`.
- A release mais recente da Central continua `v2.13.3`; nenhuma tag publicada foi movida nem asset anterior substituído.

## Mudanças

- Nome visível Softhecom Remoto, pasta de trabalho própria, URL fixa no GitHub e tempos separados de download, instalação e confirmação do serviço.
- Detecção dos perfis `SofthecomRemoto` e `HopToDesk` em Program Files/Program Files (x86), registros de desinstalação e serviços. Após instalar o asset personalizado, a confirmação exige o serviço `SofthecomRemoto`.
- Arquivos de senha/opções correspondem ao nome nativo do perfil personalizado; permanece a leitura do perfil elevado e do LocalService já utilizada. Não há migração de configuração, remoção do legado ou busca em perfis de outros usuários.
- Monitor Avalonia limitado ao PID iniciado pela Central, antes do login e na abertura da instalação nova. Reconhece título/texto visível e exige OK habilitado/visível; classes gráficas não entram na correspondência. Usa a invocação de botão existente e registra o fechamento.
- Versão real do PDV aberto e PID no log. A versão selecionada para futura instalação não é tratada como a versão do PDV antigo.
- v2.13.4 no projeto; metadados, interface e User-Agent continuam derivados da assembly. Sem dependência nova, mudança de stack, rede na inicialização ou arquivos extras obrigatórios na distribuição.

## Verificação reproduzível

```powershell
dotnet build .\SoftcomSupportAutomation\SoftcomSupportAutomation.csproj -c Release
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File .\tests\Verify-Portable.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Verify-Workflow.ps1
```

O script de EXE isolado também compila um pequeno host WinForms de teste em uma pasta temporária e abre/encerra somente suas próprias fixtures. Não executa o PDV nem instala o cliente remoto ou serviços.

| Área | Casos |
| --- | --- |
| Portabilidade | Somente EXE, metadados/UI/User-Agent 2.13.4, scripts incorporados, ZIP/RAR e cancelamento de extração. |
| Download | 10 MiB em blocos; retry seletivo e três tentativas; cancelamento por token/stopRequested na leitura e intervalo; timeout e transferência truncada; parcial removido. |
| Remoto | Identidade personalizada/legada, rejeição de outro produto, quatro caminhos nos dois Program Files sem duplicar; caminhos TOML específicos; configuração legada não confirma o perfil personalizado; campo de senha vazio/oculto e checkbox marcado. |
| Diálogos reais de fixture | PID registrado pela abertura de produção antes do login; Avalonia no texto/título e capitalização variada; erro com campo de texto não confundido com login; rejeição de outro processo, texto ausente/oculto, OK ausente/desabilitado e parada solicitada. Cada host é encerrado ao terminar. |
| Fluxos simulados | Reset Completo, Reinstalação Limpa com/sem DLLs e Prints preservados; falha na captura cancela a preparação paralela e impede desinstalação, preservação/alteração de LocalAppData e instalação. |

Resultados locais aprovados: build Release com **zero erros e três avisos MSB3277**, EXE único de **1.996.800 bytes**, FileVersion/AssemblyVersion 2.13.4.0 e ambos os scripts acima. O download de 10 MiB em 160 blocos levou **1.003 ms**, os cancelamentos responderam em **5–82 ms** após o sinal e o timeout de leitura de teste de 200 ms encerrou em **228 ms**. O asset publicado também foi aprovado pelo próprio validador de PE/assinatura da Central, sem executá-lo.

## Limites

Os logs fornecidos mostram download do remoto em cerca de dois segundos e falha posterior na confirmação do serviço. O log do Reset mostra espera pelo login e não contém a mensagem Avalonia. O responsável não possui o texto exato/versão antiga e solicitou reconhecimento simples do nome com OK. O novo tratamento foi verificado com janelas artificiais; o aviso real não foi reproduzido.

Não foram executados instalação/reinstalação real, senha, conexão remota, reinício, F11 ou captura do PDV real. A alteração de hospedagem não garante uma velocidade específica em todas as redes. Os três avisos MSB3277 de binding já investigados na v2.13.3 permanecem separados dessas alterações; o build deve continuar sem erros.
