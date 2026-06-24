@echo off
setlocal

set "APP_DIR=%~dp0"
set "URL=http://26.38.252.71:5298/Pos/Login"
set "LISTEN=http://0.0.0.0:5298"

if not exist "%APP_DIR%ControlTaxiWeb.exe" (
  echo No se encontro ControlTaxiWeb.exe en:
  echo %APP_DIR%
  pause
  exit /b 1
)

start "" /b wscript.exe "%APP_DIR%INICIAR_CONTROL_TAXI_OCULTO.vbs"
timeout /t 5 /nobreak >nul

start "" "%URL%"
exit /b 0
