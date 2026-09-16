# ==============================================================================
# VectorPowerHub Makefile
# Target: Windows x64 (.NET Framework 4.0+ / C# 5 Compatible)
# ==============================================================================

SHELL = cmd.exe
.SHELLFLAGS = /c

CSC = C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
CSCFLAGS = /target:winexe /unsafe /win32manifest:app.manifest /win32icon:app.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.dll /r:System.Core.dll

SRCS = src/*.cs
TARGET = bin/VectorPowerHub.exe
TARGET_WIN = bin\VectorPowerHub.exe
MSI_TARGET = bin/VectorPowerHub.msi
MSI_TARGET_WIN = bin\VectorPowerHub.msi
BUILD_MSI_EXE = bin\BuildMsi.exe

.PHONY: all clean run test topology bench render graphify help msi install uninstall

all: $(TARGET)

$(TARGET): $(SRCS)
	@if not exist bin mkdir bin
	$(CSC) $(CSCFLAGS) /out:$(TARGET_WIN) src\*.cs

test: $(TARGET)
	@echo [TEST] Running headless telemetry self-test...
	@$(TARGET_WIN) /test

run: $(TARGET)
	@start "" $(TARGET_WIN)

topology: $(TARGET)
	@start "" $(TARGET_WIN) /topology

bench: $(TARGET)
	@start "" $(TARGET_WIN) /bench

msi: $(TARGET)
	@if not exist $(BUILD_MSI_EXE) $(CSC) /out:$(BUILD_MSI_EXE) tools\BuildMsi.cs
	@$(BUILD_MSI_EXE)

install: msi
	@echo [INSTALL] Installing $(MSI_TARGET_WIN)...
	@msiexec /i $(MSI_TARGET_WIN) /qn

uninstall:
	@echo [UNINSTALL] Uninstalling VectorPowerHub...
	@if exist $(MSI_TARGET_WIN) (msiexec /x $(MSI_TARGET_WIN) /qn) else (msiexec /x {B95C3C8F-7B83-49F1-99A8-1E92A1A88301} /qn)

render: $(TARGET)
	@echo [RENDER] Rendering offscreen UI snapshots to previews/...
	@$(TARGET_WIN) /render

graphify:
	@graphify . --code-only
	@graphify cluster-only .

clean:
	@if exist bin rmdir /s /q bin
	@if exist previews rmdir /s /q previews
	@echo [CLEAN] Removed build artifacts.

help:
	@echo VectorPowerHub Build Targets:
	@echo   make          - Build $(TARGET) (default)
	@echo   make test     - Run headless telemetry self-test
	@echo   make run      - Launch VectorPowerHub
	@echo   make topology - Launch directly to 24-core topology view
	@echo   make bench    - Launch directly to benchmark suite
	@echo   make msi      - Build Windows Installer $(MSI_TARGET_WIN)
	@echo   make install  - Install MSI silently (/qn)
	@echo   make uninstall- Uninstall MSI cleanly (/qn)
	@echo   make render   - Render offscreen UI preview images to previews/
	@echo   make graphify - Re-extract and update graphify knowledge graph
	@echo   make clean    - Remove build artifacts
