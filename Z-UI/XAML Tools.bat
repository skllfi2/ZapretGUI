@echo off
chcp 65001 >nul
python "%~dp0xaml_tools_gui.py"
if errorlevel 1 (
    echo.
    echo [error] Python not found or a launch error occurred.
    echo [ошибка] Python не найден или произошла ошибка запуска.
    pause
)
