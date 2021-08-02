rem すべての対象コードをビルド
@echo off
cd /d %~dp0

for /r %~dp0\..\unity\Assets\IzPhysBone %%i in (.pre.*.cs) do call :proc "%%i"

:good
    goto end

:fail
	exit /b 1

:proc
    set dstname=%1
    set dstname=%dstname:.pre.=%
    "..\..\P8\tool\CsPP\CsPP.exe" -compile %1 %dstname%
    @if errorlevel 1 goto fail
    goto :EOF

:end
pause

