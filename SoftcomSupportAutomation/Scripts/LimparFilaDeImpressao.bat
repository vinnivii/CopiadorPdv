@echo off
echo Parando o servico de impressao (Spooler)...
net stop spooler
echo.

echo Apagando arquivos travados na fila...
del /Q /F /S "%systemroot%\System32\Spool\Printers\*.*"
echo.

echo Reiniciando o servico de impressao...
net start spooler
echo.

echo Fila de impressao limpa com sucesso!
pause