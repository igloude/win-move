using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WinMove.Config;
using WinMove.Core;
using WinMove.Native;

namespace WinMove.UI.Pages;

public sealed partial class HotkeysPage : Page
{
    private ConfigManager? _configManager;
    private List<HotkeyBindingViewModel> _bindings = new();

    public HotkeysPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _configManager = e.Parameter as ConfigManager;
        LoadConfig();
    }

    private void LoadConfig()
    {
        if (_configManager == null) return;

        _bindings.Clear();
        var config = _configManager.CurrentConfig;

        foreach (var (key, binding) in config.Hotkeys)
        {
            if (!ConfigManager.TryParseAction(binding.Action, out var actionType))
                continue;

            _bindings.Add(new HotkeyBindingViewModel
            {
                ConfigKey = key,
                ActionType = actionType,
                FriendlyName = ConfigManager.GetFriendlyActionName(actionType),
                Binding = new HotkeyBinding
                {
                    Modifiers = new List<string>(binding.Modifiers),
                    Key = binding.Key,
                    Action = binding.Action
                }
            });
        }

        HotkeyList.ItemsSource = null;
        HotkeyList.ItemsSource = _bindings;
    }

    private async void OnChangeClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var vm = (HotkeyBindingViewModel)button.Tag;

        var capturedModifiers = new HashSet<uint>();
        uint capturedPrimaryKey = 0;
        bool captured = false;

        var statusText = new TextBlock { Text = "Press a key combination...", FontSize = 16 };

        var dialog = new ContentDialog
        {
            Title = $"Set hotkey for: {vm.FriendlyName}",
            Content = statusText,
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        var app = (App)Application.Current;
        var hook = app.KeyboardHook;

        void OnKeyState(uint vkCode, bool isDown)
        {
            if (captured) return;

            if (IsModifierKey(vkCode))
            {
                if (isDown) capturedModifiers.Add(NormalizeModifier(vkCode));
                else capturedModifiers.Remove(NormalizeModifier(vkCode));

                // Update display with current modifiers
                if (capturedModifiers.Count > 0)
                {
                    var modNames = capturedModifiers.Select(ModifierVkToName).OrderBy(n => n);
                    DispatcherQueue.TryEnqueue(() =>
                        statusText.Text = string.Join(" + ", modNames) + " + ...");
                }
                else
                {
                    DispatcherQueue.TryEnqueue(() =>
                        statusText.Text = "Press a key combination...");
                }
            }
            else if (isDown && capturedModifiers.Count > 0)
            {
                capturedPrimaryKey = vkCode;
                captured = true;

                // Build the binding
                var modList = capturedModifiers.Select(ModifierVkToName).OrderBy(n => n).ToList();
                var keyName = ConfigManager.VkToKeyName(capturedPrimaryKey);

                vm.Binding.Modifiers = modList;
                vm.Binding.Key = keyName;
                vm.NotifyChanged();

                DispatcherQueue.TryEnqueue(() => dialog.Hide());
            }
        }

        hook.KeyStateChanged += OnKeyState;

        try
        {
            await dialog.ShowAsync();
        }
        finally
        {
            hook.KeyStateChanged -= OnKeyState;
        }
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var vm = (HotkeyBindingViewModel)button.Tag;

        vm.Binding.Modifiers = new List<string>();
        vm.Binding.Key = "";
        vm.NotifyChanged();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_configManager == null) return;

        var config = new AppConfig
        {
            Version = 1,
            EdgeSnappingEnabled = _configManager.CurrentConfig.EdgeSnappingEnabled
        };

        foreach (var vm in _bindings)
        {
            config.Hotkeys[vm.ConfigKey] = vm.Binding;
        }

        _configManager.Save(config);
    }

    private async void OnResetDefaults(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Reset to Defaults",
            Content = "Reset all keybindings to defaults?",
            PrimaryButtonText = "Reset",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (File.Exists(ConfigManager.ConfigFilePath))
                File.Delete(ConfigManager.ConfigFilePath);

            _configManager?.Reload();
            LoadConfig();
        }
    }

    private static bool IsModifierKey(uint vk) => vk is
        0x5B or 0x5C or    // VK_LWIN, VK_RWIN
        0x10 or 0xA0 or 0xA1 or  // VK_SHIFT, VK_LSHIFT, VK_RSHIFT
        0x11 or 0xA2 or 0xA3 or  // VK_CONTROL, VK_LCONTROL, VK_RCONTROL
        0x12 or 0xA4 or 0xA5;    // VK_MENU, VK_LMENU, VK_RMENU

    private static uint NormalizeModifier(uint vk) => vk switch
    {
        0x5B or 0x5C => 0x5B,        // Win
        0x10 or 0xA0 or 0xA1 => 0x10, // Shift
        0x11 or 0xA2 or 0xA3 => 0x11, // Control
        0x12 or 0xA4 or 0xA5 => 0x12, // Alt/Menu
        _ => vk
    };

    private static string ModifierVkToName(uint vk) => vk switch
    {
        0x5B => "Win",
        0x10 => "Shift",
        0x11 => "Ctrl",
        0x12 => "Alt",
        _ => $"0x{vk:X2}"
    };
}

public class HotkeyBindingViewModel : INotifyPropertyChanged
{
    public string ConfigKey { get; set; } = "";
    public ActionType ActionType { get; set; }
    public string FriendlyName { get; set; } = "";
    public HotkeyBinding Binding { get; set; } = new();

    public string ShortcutDisplay
    {
        get
        {
            if (string.IsNullOrEmpty(Binding.Key))
                return "(none)";
            var parts = new List<string>(Binding.Modifiers) { Binding.Key };
            return string.Join(" + ", parts);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NotifyChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShortcutDisplay)));
    }
}
