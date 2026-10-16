using System.Runtime.InteropServices;

namespace ActivationRepro;

public partial class App : Application
{
	public static readonly string LogPath = Path.Combine(Path.GetTempPath(), "ActivationRepro.log");
	static Window? _window;
	static bool _minimizeInsideNextResumed;

	public App()
	{
		InitializeComponent();
		File.WriteAllText(LogPath, "");
	}

	public static void Log(string s) =>
		File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} [IsActivated={_window?.IsActivated}] {s}{Environment.NewLine}");

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell());
		_window = window;
		window.Activated += (s, e) => Log("Window.Activated");
		window.Deactivated += (s, e) => Log("Window.Deactivated");
		window.Resumed += (s, e) =>
		{
			Log("Window.Resumed: enter");
#if WINDOWS
			if (_minimizeInsideNextResumed)
			{
				_minimizeInsideNextResumed = false;
				// Stands in for any call that dispatches window messages from inside the handler
				Log("Window.Resumed: ShowWindow(SW_MINIMIZE)");
				ShowWindow(Hwnd(window), SW_MINIMIZE);
			}
#endif
			Log("Window.Resumed: exit");
		};
		window.Created += async (s, e) => await RunAsync(window);
		return window;
	}

	static async Task RunAsync(Window window)
	{
#if WINDOWS
		await Task.Delay(2000);
		Log("Step 1: ShowWindow(SW_MINIMIZE)");
		ShowWindow(Hwnd(window), SW_MINIMIZE);
		await Task.Delay(1000);

		_minimizeInsideNextResumed = true;
		Log("Step 2: ShowWindow(SW_RESTORE), the Resumed handler minimizes again");
		ShowWindow(Hwnd(window), SW_RESTORE);
		await Task.Delay(1500);

		Log("Step 3: ShowWindow(SW_RESTORE)");
		ShowWindow(Hwnd(window), SW_RESTORE);
		await Task.Delay(1500);

		Log("Done");
		Current?.Quit();
#else
		await Task.CompletedTask;
#endif
	}

#if WINDOWS
	const int SW_MINIMIZE = 6;
	const int SW_RESTORE = 9;

	[DllImport("user32.dll")]
	static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

	static IntPtr Hwnd(Window window) =>
		WinRT.Interop.WindowNative.GetWindowHandle((Microsoft.UI.Xaml.Window)window.Handler!.PlatformView!);
#endif
}
