public enum RunState
{
    ReadingConfig,
    VerifyingEnv,
    Authenticating,
    RunningActions,
    Reporting,
    Completed,
    Faulted
}

// RUCONTEXT HOLDS THE STATE OF THE RUNNER AS IT PROGRESSES THROUGH ITS STATE MACHINE.
// IT IS PASSED TO EACH STATE, WHICH READS WHAT CAME BEFORE AND WRITES WHAT IT DID FOR THE NEXT STATE TO USE.
// IT IS ALSO THE SINGLE SOURCE OF TRUTH FOR "WHERE ARE WE" AT ANY INSTANT, INCLUDING ON A CRASH.
public class RunContext
{
    public RunContext(string configPath, Action<string> onStatus)
    {
        ConfigPath = configPath;
        OnStatus = onStatus;
    }
    public RunState Current { get; set; } = RunState.ReadingConfig;
    public string ConfigPath { get; }
    public IConfig? Config { get; set; }
    public string? StorageStatePath { get; set; }
    public string Timestamp { get; } = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
    public DateTime Started { get; } = DateTime.Now;
    public List<TestResult> Tests { get; set; } = new();
    public string? LogPath { get; set; }
    public RunResult? Result { get; set; }
    // ONSTATUS IS A CALLBACK FUNCTION THAT STATES CAN USE TO REPORT PROGRESS OR STATUS MESSAGES BACK TO THE USER INTERFACE OR LOGGING SYSTEM.
    public Action<string> OnStatus { get; }
}
