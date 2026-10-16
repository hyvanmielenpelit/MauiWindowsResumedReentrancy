using Microsoft.UI.Xaml;

namespace ActivationRepro.WinUI;

public partial class App : MauiWinUIApplication
{
	public App()
	{
		this.InitializeComponent();
		UnhandledException += (s, e) =>
		{
			ActivationRepro.App.Log($"UnhandledException: {e.Exception.GetType().Name}: {e.Exception.Message}"
				+ Environment.NewLine + "  Thrown at:" + Environment.NewLine + e.Exception.StackTrace
				+ Environment.NewLine + "  Raised from:" + Environment.NewLine + Environment.StackTrace);
			e.Handled = true;
		};
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
