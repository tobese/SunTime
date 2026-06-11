.PHONY: sony android wasm all clean help

# ── Android / device ─────────────────────────────────────────────────────────
#
# Debug builds use Fast Deployment: dotnet run pushes the APK *and* the managed
# assemblies to the device in one step.  Plain `adb install` only pushes the
# APK and leaves the assemblies missing, causing a native crash on startup.
#
# The Sony Xperia XQ-BC52 is auto-detected by the .NET Android tooling as long
# as it is connected over ADB (USB or wireless).

sony: ## Deploy debug build to connected Sony Xperia (ADB)
	dotnet run --project SunTime.csproj \
	    -f net10.0-android \
	    -p:RuntimeIdentifier=android-arm64

# Build the Android APK without deploying (CI / release prep).
android: ## Build Android APK without deploying
	dotnet build SunTime.csproj \
	    -f net10.0-android \
	    -p:RuntimeIdentifier=android-arm64

# ── WebAssembly ───────────────────────────────────────────────────────────────

wasm: ## Start WASM dev server (port 5001)
	@lsof -ti:5001 | xargs kill -9 2>/dev/null; true
	dotnet run --project SunTime.csproj -f net10.0-browserwasm

# ── Convenience ──────────────────────────────────────────────────────────────

all: android wasm ## Build Android APK and WASM

clean: ## Clean build artifacts
	dotnet clean SunTime.csproj

help: ## Show available targets
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' Makefile | awk 'BEGIN {FS = ":.*?## "}; {printf "  %-12s %s\n", $$1, $$2}'
