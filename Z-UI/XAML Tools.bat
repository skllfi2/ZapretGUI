@echo off
python "%~dp0xaml_tools_gui.py"
if errorlevel 1 (
    echo.
    echo [ошибка] Python не найден или произошла ошибка запуска.
    pause
)
