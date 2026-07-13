namespace RunnerUI;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		MainPage = new MainPage();
	}

	// Explicit Title — without this the window/taskbar title text is blank (it isn't
	// picked up from ApplicationTitle in the csproj automatically in this template).
	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = base.CreateWindow(activationState);
		window.Title = "Playwright Automation Tool";
		return window;
	}
}
