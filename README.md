# Playwright Automated Testing

Automated browser tests for web applications using Playwright and C#.

## Installation

1. **Install .NET 8** (or later)
   - Download from https://dotnet.microsoft.com/download

2. **Clone this repository**
   ```bash
   git clone <repo-url>
   cd PlaywrightAutomatedTesting
   ```

3. **Build the project**
   ```bash
   dotnet build
   ```

## Running Tests

### Run all tests
```bash
dotnet run
```

### Run tests in a specific environment
```bash
dotnet run -- --env staging
dotnet run -- --env prod
```

### Run specific test categories
```bash
dotnet run -- --tests "Category=Smoke"
dotnet run -- --tests "Module=Auth"
```

### See the browser while tests run
```bash
dotnet run -- --headless false
```

### Combine options
```bash
dotnet run -- --env staging --tests "Category=Smoke" --headless false
```

## What Happens

- Tests run in a headless browser by default
- Test results are saved to `test-run-log.txt`
- Screenshots and traces are saved to `traces/` for debugging
- Browser authentication state is automatically saved and reused

## Filter Options

- **Environment**: `dev`, `staging`, `prod`
- **Category**: `Smoke`, `Regression`
- **Module**: `HomePage`, `Auth`, etc.

## Troubleshooting

If tests fail, check:
1. `test-run-log.txt` for test output
2. `traces/` folder for screenshots and recordings of failed tests
3. Verify you're connected to the correct environment
