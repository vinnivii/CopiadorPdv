# Fixture RAR

`sample.rar` vem dos testes oficiais do SharpCompress, sob licença MIT:

- Repositório: https://github.com/adamhathcock/sharpcompress
- Commit: `6128ec51eec4ca9f7b7b9f44b77bdb0cd8b3bd97`
- Caminho original: `tests/TestArchives/Archives/Rar5.comment.plain.rar`
- SHA-256: `9cd2ca8ea137e220987da76b33863f4be0e74fe166d1459d79241d0e721da2ed`
- Conteúdo: `payload.txt`, com `hello from RAR!` e uma quebra de linha.

A licença original está em `LICENSE.SharpCompress.txt`. É uma fixture de fonte de 127 bytes, não um artefato de build nem um instalador. Os testes nunca executam arquivos extraídos.

## Hosts de teste

`DialogHost.cs` cria janelas artificiais para UI Automation. `InstallerHost.cs` apenas retorna o código solicitado ou grava seu PID e aguarda; não chama Windows Installer, instala produtos ou altera serviços/registro. Ambos são compilados em uma pasta temporária pelo teste de EXE isolado. Os processos são encerrados ao final dos cenários.
