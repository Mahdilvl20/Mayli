@echo off
title Installing Mayli...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install_Mayli.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Installation encountered an error. Press any key to exit.
    pause >nul
)
