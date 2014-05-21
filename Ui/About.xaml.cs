using System.Windows;

namespace Ui
{
    /// <summary>
    /// Interaction logic for About.xaml
    /// </summary>
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();
        }

        private void AboutDismiss_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
