using System.Windows;
using EpicorLibrary;
using Ui.Properties;

namespace Ui
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow
    {
        public SettingsWindow()
        {
            InitializeComponent();
            Username.Text = Settings.Default.ResourceID;
        }

        private void Save_OnClick(object sender, RoutedEventArgs e)
        {
            Settings.Default.ResourceID = Username.Text;
            Settings.Default.Save();
            Close();
        }

        private void Reset_OnClick(object sender, RoutedEventArgs e)
        {
            Username.Text = new Epicor(null).ResourceId;
        }
    }
}
