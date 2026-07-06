# Components — Provided & Required Interfaces

Each component exposes one **provided** interface and depends only on the interfaces it
**requires**. Runner is the composition root; Config and Results are leaves.

| Component | Provides | Requires | Analogy (`X is an IY`) |
|---|---|---|---|
| **Config** | `IConfig` | — | A **recipe card** is an `IConfig`: the user keeps a whole box of them and hands the chef the one they want cooked tonight. |
| **Auth** | `IAuth` | `IConfig` | A **hotel front desk** is an `IAuth`: takes your details, checks you in, and hands back a room key (`storageState.json`). |
| **Actions** | `IActions` | `IConfig` | A **playlist** is an `IActions`: just the ordered list of what to run — the speaker (xUnit) actually plays it. |
| **Runner** | `IRunner` | `IConfig`, `IAuth`, `IActions`, `IResults` | A **head chef** is an `IRunner`: takes the recipe card handed to them, gets checked into the kitchen, cooks each dish in order, and sends plates to the pass. |
| **Results** | `IResults` | — | A **scoreboard** is an `IResults`: takes the final numbers and shows them to the crowd. |

## Runner internals (state pattern)

These live *inside* the runner component — they are its choreography, not peers of the
components above.

| Type | Role |
|---|---|
| `IState` | One phase of the run: `RunAsync(ctx)` does the work and returns the next state (`null` = terminal). |
| `RunState` | Enum label for the current phase — the observable "where are we". |
| `RunContext` | Shared data across states (config, session path, raw results) **plus** `Current`. |
