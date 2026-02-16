using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinMove.Config;
using WinMove.Core;
using WinMove.Helpers;
using WinMove.UI;

namespace WinMove;

public partial class App : Application
{
    private readonly ConfigManager _configManager;
    private readonly HotkeyManager _hotkeyManager;
    private readonly WindowManipulator _manipulator;
    private readonly WindowDetector _windowDetector;
    private readonly KeyboardHook _keyboardHook;
    private readonly ModifierSession _modifierSession;
    private readonly DragHandler _dragHandler;
    private readonly SnapCycleTracker _snapTracker = new();
    private readonly TrayIconManager _trayIcon;
    private readonly DispatcherQueue _dispatcherQueue;

    private MainWindow? _mainWindow;

    public KeyboardHook KeyboardHook => _keyboardHook;

    public App()
    {
        this.InitializeComponent();

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _configManager = new ConfigManager();
        _manipulator = new WindowManipulator();
        _windowDetector = new WindowDetector();

        _keyboardHook = new KeyboardHook();

        _modifierSession = new ModifierSession();
        _modifierSession.BuildLookup(_configManager.CurrentConfig);
        _modifierSession.ActionTriggered += OnModifierSessionAction;

        _dragHandler = new DragHandler(_manipulator, _keyboardHook, _dispatcherQueue);
        _dragHandler.EdgeSnappingEnabled = _configManager.CurrentConfig.EdgeSnappingEnabled;
        _hotkeyManager = new HotkeyManager(_configManager, OnHotkeyAction);

        _keyboardHook.KeyStateChanged += _modifierSession.OnKeyStateChanged;
        _keyboardHook.Install();

        _configManager.ConfigChanged += OnConfigChanged;

        _trayIcon = new TrayIconManager(
            onShowSettings: ShowMainWindow,
            onShowAbout: ShowAbout,
            onExit: ExitApplication);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _hotkeyManager.RegisterAll();
        _trayIcon.Show();
        // App starts tray-only — no window on launch
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null || _mainWindow.IsClosed)
        {
            _mainWindow = new MainWindow(_configManager);
            _mainWindow.Closed += (s, e) => _mainWindow = null;
        }

        _mainWindow.Activate();
    }

    private void ShowAbout()
    {
        ShowMainWindow();
        _mainWindow?.NavigateToAbout();
    }

    private void ExitApplication()
    {
        _hotkeyManager.UnregisterAll();
        _dragHandler.Dispose();
        _hotkeyManager.Dispose();
        _keyboardHook.Dispose();
        _configManager.Dispose();
        _trayIcon.Dispose();

        Exit();
    }

    private void OnHotkeyAction(ActionType action, uint modFlags, uint vk)
    {
        _modifierSession.OnHotkeyFired(modFlags, vk);

        if (_modifierSession.ConsumeIfFired())
            return;

        DispatchAction(action);
    }

    private void OnModifierSessionAction(ActionType action)
    {
        DispatchAction(action);
    }

    private void DispatchAction(ActionType action)
    {
        if (_dragHandler.IsDragging)
        {
            if (action is ActionType.MoveDrag or ActionType.ResizeDrag)
            {
                if (action == ActionType.MoveDrag)
                    _dragHandler.StartMoveDrag(IntPtr.Zero);
                else
                    _dragHandler.StartResizeDrag(IntPtr.Zero);
            }
            return;
        }

        var hwnd = _windowDetector.GetWindowUnderCursor();
        if (hwnd == IntPtr.Zero) return;

        if (action is not (ActionType.SnapLeft or ActionType.SnapRight))
        {
            _snapTracker.Reset();
        }

        switch (action)
        {
            case ActionType.MoveDrag:
                _dragHandler.StartMoveDrag(hwnd);
                break;
            case ActionType.ResizeDrag:
                _dragHandler.StartResizeDrag(hwnd);
                break;
            case ActionType.Minimize:
                _manipulator.Minimize(hwnd);
                break;
            case ActionType.Maximize:
                _manipulator.Maximize(hwnd);
                break;
            case ActionType.Restore:
                _manipulator.Restore(hwnd);
                break;
            case ActionType.ToggleMinimize:
                _manipulator.ToggleMinimize(hwnd);
                break;
            case ActionType.OpacityUp:
                _manipulator.AdjustOpacity(hwnd, increase: true);
                break;
            case ActionType.OpacityDown:
                _manipulator.AdjustOpacity(hwnd, increase: false);
                break;
            case ActionType.SnapLeft:
            case ActionType.SnapRight:
                _snapTracker.Snap(hwnd, action, _manipulator);
                break;
        }
    }

    private void OnConfigChanged(AppConfig newConfig)
    {
        // Marshal to UI thread if needed
        if (!_dispatcherQueue.HasThreadAccess)
        {
            _dispatcherQueue.TryEnqueue(() => OnConfigChanged(newConfig));
            return;
        }

        _hotkeyManager.UnregisterAll();
        _hotkeyManager.RegisterAll();

        _modifierSession.BuildLookup(newConfig);
        _dragHandler.EdgeSnappingEnabled = newConfig.EdgeSnappingEnabled;

        _trayIcon.ShowNotification("win-move", "Configuration reloaded");
    }
}
