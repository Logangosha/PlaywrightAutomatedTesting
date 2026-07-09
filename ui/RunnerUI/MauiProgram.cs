using Microsoft.Extensions.Logging;
using RunnerUI.Services;

namespace RunnerUI;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		// The runner derives all its paths (results/, traces/, storageState.json) and
		// the working dir for `dotnet test` from Paths.ProjectRoot, which by default is
		// three levels up from the executing assembly. That heuristic points at the UI's
		// own bin, so point it at the real repo root (walk up to the runner's csproj).
		SetRunnerProjectRoot();

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

		// The "remote control" seam: register the runner's backend + the host that
		// drives it, so Blazor pages can use them via DI.
		builder.Services.AddSingleton<IDiscovery, Discovery>();
		builder.Services.AddSingleton<RunnerHost>();
		builder.Services.AddSingleton<ArtifactOpener>();
		builder.Services.AddSingleton<RunHistory>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

	// Walks up from the app's base directory to the folder containing the runner's
	// csproj and treats that as the project root. Works when running from the source
	// tree (the dev scenario); if not found, Paths keeps its default heuristic.
	private static void SetRunnerProjectRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null &&
		       !File.Exists(Path.Combine(dir.FullName, "PlaywrightAutomatedTesting.csproj")))
			dir = dir.Parent;

		if (dir is not null)
			Paths.SetProjectRoot(dir.FullName);
	}
}
