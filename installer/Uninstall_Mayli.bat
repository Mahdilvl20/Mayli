@echo off
title Uninstalling Mayli...
echo Stopping running Mayli processes...
taskkill /F /IM Mayli.exe >nul 2>&1

echo Removing Shortcuts...
del /f /q "%USERPROFILE%\Desktop\Mayli.lnk" >nul 2>&1
rd /s /q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Mayli" >nul 2>&1

echo Removing Application Files...
rd /s /q "%LOCALAPPDATA%\Programs\Mayli" >nul 2>&1

echo Mayli has been successfully uninstalled.
pause
