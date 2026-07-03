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

## Test Traces

Each test run creates a timestamped folder with all test traces organized by module and test method.

### Trace Folder Structure

```
traces/
└── {timestamp}/                           ← Test run (e.g., 2026-07-03_14-30-45)
    ├── HomePage/                          ← Module from [Trait("Module", "HomePage")]
    │   ├── Auth_PreTestState_...zip
    │   └── MemberSearch_AfterLogin_...zip
    └── MemberManagement/
        └── MemberSearch_SearchExisting_...zip
```

### Opening a Trace

1. Locate the test run folder by timestamp in `traces/`
2. Navigate to the module folder (e.g., `HomePage`)
3. Extract the `.zip` file for the test method you want to inspect
4. Open in Playwright Inspector:
   ```bash
   playwright show-trace {path-to-trace}.zip
   ```
   Or drag the `.zip` file to https://trace.playwright.dev

### Example

If test `MemberSearch_SearchExistingMember_DisplaysMatchingMember` failed on `2026-07-03_14-30-45`:

1. Open `traces/2026-07-03_14-30-45/MemberManagement/`
2. Extract `MemberSearch_SearchExistingMember_DisplaysMatchingMember.zip`
3. View with: `npx playwright show-trace traces/2026-07-03_14-30-45/MemberManagement/MemberSearch_SearchExistingMember_DisplaysMatchingMember.zip`

## Filter Options

- **Environment**: `dev`, `staging`, `prod`
- **Category**: `Smoke`, `Regression`
- **Module**: `HomePage`, `Auth`, etc.

## Troubleshooting

If tests fail, check:
1. `test-run-log.txt` for test output and failure messages
2. `traces/{timestamp}/{module}/{testname}.zip` for screenshots, DOM snapshots, and network logs
   - Organized by test run timestamp, module, and test method name
   - Extract and view with Playwright Inspector (see **Test Traces** section above)
3. Verify you're connected to the correct environment
4. Check `storageState.json` to ensure authentication is persisted correctly
