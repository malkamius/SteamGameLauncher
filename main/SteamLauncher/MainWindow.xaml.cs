using System.Configuration;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Schema;

namespace SteamLauncher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool regionSettingsLoaded = false;
        public MainWindow()
        {
            InitializeComponent();

            LoadConfig();

            RefreshApps();
        }

        private void RefreshApps(bool force = false)
        {
            var apps = (from app in AppInfo.GetApps(force) 
                        where app.Type == "Game" 
                        orderby app.LastPlayed descending, app.Name
                        select app).ToList();
            var filterapps = (from app in apps where (string.IsNullOrEmpty(FilterTextBox.Text) || (app?.Name?.Contains(FilterTextBox.Text, StringComparison.InvariantCultureIgnoreCase) ?? false)) select app).ToList();
            GameCountTextBlock.Text = $"{apps.Count} installed ({filterapps.Count} showing)";
            SteamAppsListView.ItemsSource = filterapps;
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowPosition();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            
        }

        private void SaveWindowPosition()
        {
            if (!regionSettingsLoaded) return;
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings.Remove("WindowLeft");
            config.AppSettings.Settings.Remove("WindowTop");
            config.AppSettings.Settings.Remove("WindowWidth");
            config.AppSettings.Settings.Remove("WindowHeight");
            config.AppSettings.Settings.Add("WindowLeft", this.Left.ToString());
            config.AppSettings.Settings.Add("WindowTop", this.Top.ToString());
            config.AppSettings.Settings.Add("WindowWidth", this.Width.ToString());
            config.AppSettings.Settings.Add("WindowHeight", this.Height.ToString());
            config.Save(ConfigurationSaveMode.Modified);
        }

        private void LoadConfig()
        {
            
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            string? left = config.AppSettings.Settings["WindowLeft"]?.Value;
            string? top = config.AppSettings.Settings["WindowTop"]?.Value;
            string? width = config.AppSettings.Settings["WindowWidth"]?.Value;
            string? height = config.AppSettings.Settings["WindowHeight"]?.Value;

            if (!string.IsNullOrEmpty(left) && double.TryParse(left, out var leftpos) && leftpos >= 0)
            {
                double screenWidth = SystemParameters.PrimaryScreenWidth;
                if (leftpos < screenWidth)
                {
                    this.Left = leftpos;
                }
                else
                {
                    this.Left = screenWidth - this.Width;
                }
            }

            if (!string.IsNullOrEmpty(top) && double.TryParse(top, out var toppos) && toppos >= 0)
            {
                double screenHeight = SystemParameters.PrimaryScreenHeight;
                if (toppos < screenHeight)
                {
                    this.Top = toppos;
                }
                else
                {
                    this.Top = screenHeight - this.Height;
                }
            }

            if (!string.IsNullOrEmpty(width) && double.TryParse(width, out var winWidth) && winWidth > 0)
            {
                this.Width = winWidth;
            }

            if (!string.IsNullOrEmpty(height) && double.TryParse(height, out var winHeight) && winHeight > 0)
            {
                this.Height = winHeight;
            }

            FilterTextBox.Text = config.AppSettings.Settings["Filter"]?.Value ?? "";
            if (FilterTextBox.Text != "")
            {
                FilterTextBox.SelectAll();
                FilterTextBox.Focus();
            }
            regionSettingsLoaded = true;
        }

        private void Item_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            
            if (sender is StackPanel item && item.DataContext is AppInfo app)
            {
                var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = $"steam://rungameid/{app.AppId}",
                    UseShellExecute = true
                });
            }
        }

        private void SteamAppsListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SaveWindowPosition();
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            SaveWindowPosition();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            RefreshApps(true);
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings.Remove("Filter");
            config.AppSettings.Settings.Add("Filter", FilterTextBox.Text);
            config.Save(ConfigurationSaveMode.Modified);
            RefreshApps();
        }
    }
}