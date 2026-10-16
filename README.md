# MauiWindowsResumedReentrancy

Re-entrant window activation during `Window.Resumed` throws "Window was already
deactivated", then "Window was already activated", on .NET MAUI for Windows.

This is a minimal repro for a .NET MAUI issue (link to be added once it is filed).

## What it shows

On Windows, `MauiWinUIWindow.OnActivated` sets its private `_isActivated` flag **before**
it raises `Window.Resumed`, while `Window.IsActivated` flips only after the `Resumed`
handlers return. If a window activation message is dispatched re-entrantly inside a
`Resumed` handler:

1. The nested deactivation passes `MauiWinUIWindow`'s gate, and `IWindow.Deactivated()`
   throws `Window was already deactivated`, because `Window.IsActivated` is still `false`.
2. The outer activation then sets `Window.IsActivated = true`, so the two states now
   disagree.
3. The next activation passes the gate and throws `Window was already activated`.

The repro triggers the re-entrancy deterministically with `ShowWindow(SW_MINIMIZE)`
inside the `Resumed` handler. In production the same happens with any call that
dispatches window messages from the STA UI thread. One example is MAUI's own
`Connectivity.NetworkAccess`, whose WinRT and COM calls can dispatch a `WM_ACTIVATE`.

## Running it

Requirements: Windows 10 or 11, a .NET 10 SDK, and the MAUI workload
(`dotnet workload install maui`).

```powershell
dotnet build ActivationRepro\ActivationRepro.csproj -c Debug
.\ActivationRepro\bin\Debug\net10.0-windows10.0.19041.0\win-x64\ActivationRepro.exe
```

Do not touch anything while it runs. The app minimizes and restores itself twice, then
quits after about 6 seconds. Afterwards, open `%TEMP%\ActivationRepro.log`.

All of the relevant code is in `ActivationRepro/App.xaml.cs` (lifecycle logging and the
self-driving test) and `ActivationRepro/Platforms/Windows/App.xaml.cs` (logs
`UnhandledException` with both stack traces). The rest is the unchanged
`dotnet new maui` template, restricted to Windows and pinned to
`Microsoft.Maui.Controls` 10.0.110.

## Expected vs actual

**Expected:** a `Window.Deactivated` event for the nested minimize, no exceptions, and
`Window.IsActivated` matching the window's real state throughout.

**Actual** (MAUI 10.0.110, Windows 11 25H2, abridged):

```text
[IsActivated=True]  Window.Activated
[IsActivated=True]  Step 1: ShowWindow(SW_MINIMIZE)
[IsActivated=False] Window.Deactivated
[IsActivated=False] Step 2: ShowWindow(SW_RESTORE), the Resumed handler minimizes again
[IsActivated=False] Window.Resumed: enter
[IsActivated=False] Window.Resumed: ShowWindow(SW_MINIMIZE)
[IsActivated=False] UnhandledException: InvalidOperationException: Window was already deactivated
[IsActivated=False] Window.Resumed: exit
[IsActivated=True]  Window.Activated
[IsActivated=True]  Window.Resumed: enter
[IsActivated=True]  Window.Resumed: exit
[IsActivated=True]  UnhandledException: InvalidOperationException: Window was already activated
[IsActivated=True]  Step 3: ShowWindow(SW_RESTORE)
[IsActivated=True]  Done
```

If another window takes focus before step 1, the first `Window.Deactivated` appears
earlier and step 1 logs none of its own. Both exceptions still occur, because all the
repro needs is for the window to be inactive when step 2 restores it.

The full log includes the stack where each exception was thrown and the stack from which
the `UnhandledException` was raised. The latter shows the nested window procedure inside
`ShowWindow`, inside the `Resumed` handler.
