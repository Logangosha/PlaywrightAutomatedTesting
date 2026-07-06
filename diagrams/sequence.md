# Sequence — Program Flow

The user keeps a collection of configs and hands **one** to the runner per run.
The runner then orchestrates Config, Auth, Actions, and Results through their interfaces.

```mermaid
sequenceDiagram
    actor User
    participant Runner as Runner : IRunner
    participant Config as Config : IConfig
    participant Auth as Auth : IAuth
    participant Actions as Actions : IActions (xUnit)
    participant Results as Results : IResults

    Note over User,Config: User keeps many configs<br/>(facebook.json, portal-smoke.json, ...)

    User->>Runner: Run(chosen config)

    Runner->>Config: Read()
    Config-->>Runner: env, auth, actions, headless

    Runner->>Runner: verify env is reachable
    alt env unreachable
        Runner->>Results: Report(failure)
        Results-->>User: show error, stop
    end

    Runner->>Auth: Authenticate(config)
    alt mode = none
        Auth-->>Runner: skip, no session
    else mode = manual
        Auth->>User: open browser, wait for login
        User-->>Auth: logged in (ENTER)
        Auth->>Auth: save storageState.json
        Auth-->>Runner: session saved
        alt login confirmed
            User-->>Auth: logged in (ENTER)
            Auth->>Auth: save storageState.json
            Auth-->>Runner: session saved
        else timed out / cancelled
            Auth-->>Runner: auth failed
        end
    else mode = auto
        Auth->>Auth: run login steps, save storageState.json
        Auth-->>Runner: session saved
        alt login steps succeed
            Auth->>Auth: run login steps, save storageState.json
            Auth-->>Runner: session saved
        else bad credentials / steps failed
            Auth-->>Runner: auth failed
        end
    end

    opt auth failed
        Runner->>Results: Report(auth failure)
        Results-->>User: show error, stop
    end

    Runner->>Actions: Run(selector)
    Actions->>Actions: dotnet test --filter<br/>(xUnit iterates, TestBase traces each)
    Actions-->>Runner: raw results (TRX)

    Runner->>Results: Report(results)
    Results-->>User: display summary
```
