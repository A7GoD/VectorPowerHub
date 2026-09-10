# ==============================================================================
# VectorPowerHub Makefile
# Target: Windows x64 (.NET Framework 4.0+ / C# 5 Compatible)
# ==============================================================================

CSC = C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe
CSCFLAGS = /target:winexe /unsafe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.dll /r:System.Core.dll

SRCS = src/VectorPowerHubForm.cs src/PowerCoreEngine.cs
TARGET = bin/VectorPowerHub.exe
TARGET_WIN = bin\VectorPowerHub.exe

.PHONY: all clean run test topology bench render graphify help

all: $(TARGET)

$(TARGET): $(SRCS)
	@if not exist bin mkdir bin
	"$(CSC)" $(CSCFLAGS) /out:$(TARGET_WIN) $(subst /,\,$^)

test: $(TARGET)
	@echo [TEST] Running headless telemetry self-test...
	@$(TARGET_WIN) /test

run: $(TARGET)
	@start "" $(TARGET_WIN)

topology: $(TARGET)
	@start "" $(TARGET_WIN) /topology

bench: $(TARGET)
	@start "" $(TARGET_WIN) /bench

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
	@echo   make render   - Render offscreen UI preview images to previews/
	@echo   make graphify - Re-extract and update graphify knowledge graph
	@echo   make clean    - Remove build artifacts
