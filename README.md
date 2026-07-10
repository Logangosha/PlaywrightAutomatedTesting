# Playwright Automation Tool

A Windows desktop app for running automated browser tests against a website.

## What it is

The app runs [Playwright](https://playwright.dev/) tests through a real browser. You select a site, an environment, and which tests to run; the app logs in if needed, runs the tests, and reports which passed and which failed. Each test that fails is saved as a trace (a screenshot-by-screenshot recording) you can replay.

## Why use it

Manual testing is slow and inconsistent. This tool runs the same steps the same way every time, runs multiple tests at once, and records what happened so failures can be diagnosed without reproducing them by hand.

## How to use it

### Install (once, Windows only)

1. Copy the project to a short path like `C:\PAT`. Deep folders can exceed the Windows path limit and break the build.
2. Double-click `setup.bat`. It installs the dependencies and opens the app when finished. The first run can take several minutes.
   - Approve the permission (UAC) prompt.
   - If SmartScreen appears, choose **More info → Run anyway**.
3. Use the **Playwright Automation Tool** desktop shortcut afterward.

The app rebuilds itself on each launch so it always runs the current tests.

### Run tests

1. Open the app.
2. Select a site and environment.
3. Select which tests to run (all, or a specific group).
4. Enter login credentials if the tests require them.
5. Click **Run**.

### Read the results

| Verdict | Meaning |
|---|---|
| **Passed** | All selected tests passed. |
| **Failed** | One or more tests failed. Open the trace to see where. |
| **Faulted** | The run could not complete — bad setting, unreachable site, or failed login. The message states the cause. |

## More detail

- [README.md](README.md) — config files, command-line runner, exit codes, troubleshooting.
- [src/tests/README.md](src/tests/README.md) — writing tests.
- [CLAUDE.md](CLAUDE.md) and [diagrams/](diagrams/) — architecture and internals.
