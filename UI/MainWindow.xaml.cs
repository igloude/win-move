using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinMove.Config;

namespace WinMove.UI;

public sealed partial class MainWindow : Window
{
    private readonly ConfigManager _configManager;

    public bool IsClosed { get; private set; }

    public MainWindow(ConfigManager configManager)
    {
        _configManager = configManager;
        this.InitializeComponent();

        // Set window size
        var appWindow = this.AppWindow;
        appWindow.Resize(new Windows.Graphics.SizeInt32(1200, 980));

        // Set window icon
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "app.ico");
        if (File.Exists(iconPath))
            appWindow.SetIcon(iconPath);

        // Navigate to default page
        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(Pages.HotkeysPage), _configManager);

        this.Closed += (s, e) => IsClosed = true;
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            Type? pageType = tag switch
            {
                "HotkeysPage" => typeof(Pages.HotkeysPage),
                "GeneralPage" => typeof(Pages.GeneralPage),
                "AboutPage" => typeof(Pages.AboutPage),
                _ => null
            };
            if (pageType != null)
                ContentFrame.Navigate(pageType, _configManager);
        }
    }

    public void NavigateToAbout()
    {
        foreach (var item in NavView.FooterMenuItems)
        {
            if (item is NavigationViewItem navItem && navItem.Tag?.ToString() == "AboutPage")
            {
                NavView.SelectedItem = navItem;
                break;
            }
        }
    }
}
