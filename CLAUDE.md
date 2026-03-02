# Tactadile

Windows system tray utility for managing windows with global hotkeys and mouse gestures, targeting the window under the cursor. C# / .NET 8 / WinUI 3 (unpackaged).

## Build & Run

```bash
dotnet build -p:Platform=x64
./bin/x64/Debug/net8.0-windows10.0.19041.0/Tactadile.exe
```

- **`dotnet run` does NOT work** — WinUI 3 resolves the wrong output path. Always build then launch the exe.
- ARM: substitute `arm64` for `x64` everywhere.
- Hot-reload: `.\dev.ps1` (PowerShell) watches `.cs`/`.xaml` files, auto-kills, rebuilds, and relaunches.
- Single-instance mutex — kill existing `Tactadile.exe` before relaunching.
- Debug builds default to **Pro license** (`#if DEBUG` in `LicenseManager`), so all features are testable.

## Architecture

```
Program.cs          Single-instance mutex, STA thread, DispatcherQueue
  App.xaml.cs       Manual wiring (no DI container), central event hub
    KeyboardHook    WH_KEYBOARD_LL → fires KeyStateChanged
    MouseHook       WH_MOUSE_LL → fires MouseButtonPressed, MouseScrolled
    ModifierSession Tracks held modifiers, detects key switches → ActionTriggered
    GestureEngine   Arms on modifier hold, polls cursor at 60fps, runs recognizers → GestureTriggered
    HotkeyManager   RegisterHotKey + hidden message window for WM_HOTKEY → OnHotkeyAction
    ─── all three dispatch to App.DispatchAction() ───
    WindowManipulator  Move, resize, snap, opacity, minimize, maximize, nudge
    DragHandler        Coordinates drag operations with edge snapping
    SnapCycleTracker   Cycles snap widths: 2/3 → 1/2 → 1/3
    ConfigManager      JSON config in %APPDATA%\Tactadile\config.json, FileSystemWatcher hot-reload
    LaunchRuleEngine   WindowEventHook for EVENT_OBJECT_SHOW → delayed window auto-positioning
    LicenseManager     RSA-signed tokens, DPAPI-encrypted storage, #if DEBUG = Pro
```

**Dual-path hotkey dispatch**: `RegisterHotKey` (WM_HOTKEY via `HotkeyManager`) is the primary path. `KeyboardHook` via `ModifierSession` is the fallback for race conditions and key-switch detection. `ModifierSession.ConsumeIfFired()` prevents double-dispatch.

## Key Patterns

- **Hook delegate GC prevention**: Every hook stores its callback delegate as a class field (e.g. `_hookProc = HookCallback`). If you create a new hook, you MUST store the delegate — otherwise the GC collects it and the hook silently dies.
- **Lookup tables**: `ModifierSession.BuildLookup()` and `GestureEngine.BuildLookup()` build `Dictionary<(uint mods, uint vk/GestureType), ActionType>` for O(1) dispatch. These are rebuilt on every config change.
- **Stateless recognizers**: `SwipeRecognizer` and `EdgeFlickRecognizer` are static classes operating on `CursorRingBuffer` snapshots. `ShakeRecognizer` is stateful (tracks reversals) and must be `Reset()` when disarming.
- **No SendInput from hook callbacks**: Risk of ~300ms timeout killing the hook. `DragHandler` uses `DispatcherQueueTimer` to defer `SendInput` calls. `EdgeSnapHelper.Signature` (`0x574D4F56`) marks synthetic keystrokes so `KeyboardHook` skips them.
- **Menu mask key**: When suppressing Win+key combos, `KeyboardHook.SendMenuMaskKey()` injects VK `0xE8` to prevent Windows from opening the Start Menu on Win release.

## Threading

- Main thread: WinUI `DispatcherQueue`. All hooks installed from this thread, so hook callbacks arrive on it too.
- `ConfigManager.OnFileChanged`: Fires on ThreadPool → 300ms debounce timer → `App.OnConfigChanged` marshals to UI via `DispatcherQueue.TryEnqueue()`.
- `LaunchRuleEngine`: `System.Threading.Timer` for delays (ThreadPool) → re-dispatches to UI thread.

## File Map

```
Config/            ActionType/GestureType/ZoneType enums, HotkeyConfig data models, ConfigManager
Core/              HotkeyManager, ModifierSession, GestureEngine, KeyboardHook, MouseHook,
                   DragHandler, WindowManipulator, WindowDetector, SnapCycleTracker,
                   EdgeSnapHelper, KeystrokeSender, MonitorHelper, ZoneCalculator,
                   LaunchRuleEngine, WindowEventHook, ProcessInfoHelper
Core/Recognizers/  CursorRingBuffer, CursorSample, SwipeRecognizer, ShakeRecognizer, EdgeFlickRecognizer
Helpers/           TrayIconManager (Shell_NotifyIcon), StartupHelper (registry Run key)
Licensing/         LicenseManager, LicenseClient, LicenseToken, LicenseTier, BuildMetadata
Native/            NativeMethods (P/Invoke), NativeConstants, NativeStructs
UI/                MainWindow (NavigationView shell)
UI/Pages/          HotkeysPage, GeneralPage, AutoPositionPage, LicensePage, AboutPage
```

## Workflows

### Adding a new action

1. Add value to `Config/ActionType.cs` enum
2. Add default binding in `ConfigManager.CreateDefaultConfig()` Hotkeys dictionary
3. Add entry in `ConfigManager.DisplayOrder` list (null = section divider)
4. Add friendly name in `ConfigManager.GetFriendlyActionName()`
5. Add handling in `App.DispatchAction()` — global actions go before `GetWindowUnderCursor()`, window-targeted after
6. Implement in `WindowManipulator` or `KeystrokeSender` as needed

### Adding a new gesture type

1. Add value to `Config/GestureType.cs` enum
2. Create recognizer in `Core/Recognizers/` (static class for stateless, instance for stateful)
3. Wire into `GestureEngine.EvaluateMotionGestures()` (motion) or `OnMouseButton()`/`OnMouseScroll()` (discrete)
4. Add default binding in `ConfigManager.CreateDefaultConfig()` Gestures dictionary

### Adding a new settings page

1. Create `UI/Pages/NewPage.xaml` + `UI/Pages/NewPage.xaml.cs`
2. Add `NavigationViewItem` in `UI/MainWindow.xaml` with `Tag="NewPage"`
3. Add case in `MainWindow.NavView_SelectionChanged` tag switch
4. Pages receive `NavigationContext(ConfigManager, LicenseManager)` via `OnNavigatedTo`

## Conventions

- `_camelCase` private fields, `PascalCase` everything else, `camelCase` locals/params
- File-scoped namespaces (`namespace Tactadile.Core;`)
- `sealed class` by default for non-inherited types
- Nullable enabled, implicit usings enabled
- Native struct fields match Win32 names (e.g. `cbSize`, `dwFlags`)
- JSON serialization: `System.Text.Json`, `PropertyNameCaseInsensitive`, `WhenWritingNull`

## Commits

```
feat: short imperative description
fix: short imperative description
chore: short imperative description
```
