# WinUI 3 System Tray Lifecycle & {x:Bind} Initialization

## Context & Problem Statement

Smart Calendar Manager runs as a background tray application using `H.NotifyIcon.WinUI` and Windows App SDK / WinUI 3. When starting automatically with Windows (via `--startup` or `--minimized`), the application hides the main window immediately upon launch:

```csharp
if (startMinimized)
{
    Window.AppWindow.Hide();
}
```

In standard WinUI 3 compilation, the XAML markup compiler treats `Window` classes differently from `Page` or `UserControl`:
- `Window` does not implement `FrameworkElement` and lacks `Loaded` or `Loading` events.
- Generated binding code (`MainWindow.g.cs`) hooks bindings evaluation exclusively to the `Window.Activated` event:
  ```csharp
  element1.Activated += bindings.Activated;
  ```

Because `Window.Activate()` is intentionally omitted when starting minimized to prevent flashing or stealing focus, `Window.Activated` never fires. Consequently:
- `Bindings.Initialize()` is never invoked.
- All `{x:Bind}` targets inside `Window.xaml` (such as `TaskbarIcon.ContextFlyout` items: "Abrir", "Sincronizar", "Minimizar", "Salir") remain unassigned (`null`).
- Clicking menu items in the system tray silently fails because `H.NotifyIcon` translates clicks into native Win32 `WM_COMMAND` messages that inspect the item's `Command` property, find `null`, and do nothing.

---

## Architectural Resolution

To make system tray menus 100% resilient across both visible and headless/minimized launches:

1. **Pre-initialization of Commands:**
   Instantiate all `IRelayCommand` properties before `InitializeComponent()` runs:
   ```csharp
   ShowPanelCommand = new RelayCommand(ShowPanel);
   HidePanelCommand = new RelayCommand(HidePanel);
   SyncNowCommand = new AsyncRelayCommand(async () => ...);
   ExitCommand = new RelayCommand(ExitApplication);
   InitializeComponent();
   ```

2. **Forced Bindings Initialization:**
   Immediately call `Bindings.Initialize()` in the `MainWindow` constructor:
   ```csharp
   Bindings.Initialize();
   ```
   This triggers `Bindings.Update()` eagerly without waiting for `Window.Activated`.

3. **Dual Wiring via C# Code-Behind:**
   Assign unique `x:Name` attributes to each `MenuFlyoutItem` in XAML (`OpenMenuItem`, `SyncMenuItem`, `MinimizeMenuItem`, `ExitMenuItem`) and wire both `Command` and `Click` handlers imperatively:
   ```csharp
   ExitMenuItem.Command = ExitCommand;
   ExitMenuItem.Click += (s, e) => ExitApplication();
   ```

This provides complete defense-in-depth against differences in Win32 popup message dispatching, framework threading, and hidden window states.
