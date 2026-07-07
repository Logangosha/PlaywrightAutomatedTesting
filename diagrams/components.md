# Components — Provided & Required Interfaces

Each component exposes one **provided** interface and depends only on the interfaces it
**requires**. Runner is the composition root; Config, Results, and Discovery are leaves.

| Component | Provides | Requires | Analogy (`X is an IY`) |
|---|---|---|---|
| **Config** | `IConfig` | — | A **recipe card** is an `IConfig`: the user keeps a whole box of them and hands the chef the one they want cooked tonight. |
| **Auth** | `IAuth` | `IConfig` | A **hotel front desk** is an `IAuth`: takes your details, checks you in, and hands back a room key (`storageState.json`). |
| **Actions** | `IActions` | `IConfig` | A **playlist** is an `IActions`: just the ordered list of what to run — the speaker (xUnit) actually plays it. |
| **Runner** | `IRunner` | `IConfig`, `IAuth`, `IActions`, `IResults` | A **head chef** is an `IRunner`: takes the recipe card handed to them, gets checked into the kitchen, cooks each dish in order, and sends plates to the pass. |
| **Results** | `IResults` | — | A **scoreboard** is an `IResults`: takes the final numbers and shows them to the crowd. |
| **Discovery** | `IDiscovery` | — | A **menu** is an `IDiscovery`: it lists every dish the kitchen *can* make so the diner knows what to order — it cooks nothing, just tells you what's on offer. |

## Discovery sits *beside* the run pipeline, not inside it

Discovery is consumed by the **frontend**, not the runner — it answers "what *can* run?"
*before* a config exists, so a UI can draw the Site → Env → Module tree, offer a selection,
and resolve the auto-auth login test up front. The runner's state machine is unchanged.

```
                 ┌─ requires IDiscovery ─▶  Discovery   (the menu: what CAN run)
   Frontend / UI ┤
                 └─ requires IRunner ────▶  Runner      (the kitchen: RUN this order)
```

Because it's a leaf that requires no config, discovery never participates in a run — it
only reads test metadata (via reflection over the `[Trait]` attributes). See CLAUDE.md
for the reflection mechanism and its constraint.

## Runner internals (state pattern)

These live *inside* the runner component — they are its choreography, not peers of the
components above.

| Type | Role |
|---|---|
| `IState` | One phase of the run: `RunAsync(ctx)` does the work and returns the next state (`null` = terminal). |
| `RunState` | Enum label for the current phase — the observable "where are we". |
| `RunContext` | Shared data across states (config, session path, raw results) **plus** `Current`. |
