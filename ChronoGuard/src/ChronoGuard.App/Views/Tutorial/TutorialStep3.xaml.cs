using System.Windows;
using System.Windows.Controls;

namespace ChronoGuard.App.Views.Tutorial
{
    public partial class TutorialStep3 : Page
    {
        public TutorialStep3()
        {
            InitializeComponent();
        }
        private void Finish_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }
    }
}
