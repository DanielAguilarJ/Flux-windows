using System.Windows.Controls;

namespace ChronoGuard.App.Views.Tutorial
{
    public partial class TutorialStep1 : Page
    {
        public TutorialStep1()
        {
            InitializeComponent();
        }
        private void Next_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            NavigationService?.Navigate(new TutorialStep2());
        }
    }
}
