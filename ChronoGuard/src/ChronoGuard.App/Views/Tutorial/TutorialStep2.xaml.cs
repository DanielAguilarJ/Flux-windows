using System.Windows.Controls;

namespace ChronoGuard.App.Views.Tutorial
{
    public partial class TutorialStep2 : Page
    {
        public TutorialStep2()
        {
            InitializeComponent();
        }
        private void Next_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            NavigationService?.Navigate(new TutorialStep3());
        }
    }
}
