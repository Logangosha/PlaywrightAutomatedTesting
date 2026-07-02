# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Build
```bash
dotnet build
```

### Run all tests
```bash
dotnet run
```

Default values: `env=dev`, `auth=manual`, `tests=all`, `headless=false`

### Run tests with specific filters
```bash
dotnet run -- --tests "Category=Smoke"
dotnet run -- --tests "Module=HomePage"
dotnet run -- --tests "Category=Smoke&Module=Auth"
```

Valid filter keys: `Category`, `Module`. Combine multiple filters with `&`.

### Run tests in headed mode (browser visible)
```bash
dotnet run -- --headless false
```

### Run tests in a specific environment
```bash
dotnet run -- --env prod
dotnet run -- --env staging
```

### Run with specific authentication mode
```bash
dotnet run -- --auth manual
dotnet run -- --auth auto
```

### Combine options
```bash
dotnet run -- --env staging --auth auto --tests "Category=Smoke" --headless false
```

## Architecture

### Test Execution Pipeline
The custom test runner (`Test.cs`) orchestrates a three-stage pipeline:

1. **Context Stage**: Writes runtime configuration (`testRunContext.json`) containing environment, auth mode, test filters, and headless flag.
2. **Authentication Stage**: Calls `AuthenticationProvider.AuthenticateAsync()` to establish session state (manual login via browser or automated login).
3. **Test Execution Stage**: Spawns `dotnet test` process with xUnit, filtering tests by trait if specified.

The runner captures test output, writes logs to `test-run-log.txt`, and displays a summary showing passed/failed/skipped counts and duration.

### Core Components

**TestBase** (`src/tests/TestBase.cs`)
- Implements `IAsyncLifetime` to manage Playwright browser lifecycle
- Launches Chromium browser with headless mode from `TestRunContext`
- Creates a new browser context with persisted storage state (from prior authentication)
- Enables tracing (screenshots, snapshots, sources) for each test
- Stops tracing on cleanup and saves `.zip` file to `traces/` directory with test name and timestamp

**TestRunContext** (`src/utilities/config/runtime/TestRunContext.cs`)
- Singleton that reads `testRunContext.json` (written by Test.cs)
- Provides runtime values to tests: `env`, `auth`, `tests`, `headless`

**EnvironmentProvider** (`src/utilities/config/env/EnvironmentProvider.cs`)
- Reads `environments.json` to get environment-specific configuration (e.g., base URLs)
- Selects config based on environment name from `TestRunContext`

**AuthenticationProvider** (`src/utilities/auth/AuthenticationProvider.cs`)
- Supports two auth modes: `manual` (opens browser, waits for user to log in) and `auto` (placeholder, not yet implemented)
- Saves browser storage state (cookies, session tokens) to `storageState.json`
- Tests reuse this saved state via `StorageStatePath` when creating browser contexts

### Test Organization
Tests use xUnit traits for categorization and filtering:
- **Category** trait (e.g., "Smoke", "Regression") identifies test severity/scope
- **Module** trait (e.g., "HomePage", "Auth") identifies the feature area
- Test.cs filters tests using `dotnet test --filter` with trait-based queries

### Configuration Files
- `environments.json` — environment-specific URLs and endpoints (copied to output on build)
- `testRunContext.json` — runtime configuration written by Test.cs at startup (location: `src/utilities/config/runtime/`)
- `storageState.json` — persisted browser session state from authentication (location: `src/utilities/auth/`)
- `traces/` — test trace artifacts (screenshots, snapshots, network logs) for debugging
