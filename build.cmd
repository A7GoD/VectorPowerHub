@echo off
setlocal enabledelayedexpansion

echo ===============================================================================
echo  VectorPowerHub - Build Script
echo  Target: Windows x64 (.NET Framework 4.0+ / C# 5 Compatible)
echo ===============================================================================

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo [ERROR] csc.exe not found at %CSC%
    exit /b 1
)

if not exist bin (
    mkdir bin
)

echo [1/2] Compiling VectorPowerHub.exe...
"%CSC%" /target:winexe /out:bin\VectorPowerHub.exe /unsafe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.dll /r:System.Core.dll src\VectorPowerHubForm.cs src\PowerCoreEngine.cs

if %ERRORLEVEL% equ 0 (
    echo [2/2] Build SUCCESS: bin\VectorPowerHub.exe
    echo.
    echo To run: bin\VectorPowerHub.exe
    echo To verify headless: bin\VectorPowerHub.exe /test
    exit /b 0
) else (
    echo [ERROR] Build failed with exit code %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)
