@echo off
title Ajuste Registro - RDP Warning

:: ===== Verifica se esta como admin =====
net session >nul 2>&1
if %errorlevel% neq 0 (
echo Solicitando privilegios de administrador...
powershell -Command "Start-Process '%~f0' -Verb RunAs"
exit
)

echo ===============================
echo Configurando chave de registro...
echo ===============================

:: Cria o caminho caso nao exista
reg add "HKLM\Software\Policies\Microsoft\Windows NT\Terminal Services\Client" /f

:: Cria/atualiza o valor
reg add "HKLM\Software\Policies\Microsoft\Windows NT\Terminal Services\Client" ^
/v RedirectionWarningDialogVersion ^
/t REG_DWORD ^
/d 1 ^
/f

echo.
echo Configuracao aplicada com sucesso!
echo.

pause
:: ===============================
:: Execute normalmente (ele vai pedir elevacao sozinho)
:: ===============================
