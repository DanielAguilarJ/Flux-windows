using System.Windows;

namespace ChronoGuard.App.Views.Tutorial
{
    public partial class TutorialWindow : Window
    {
        public TutorialWindow()
        {
            InitializeComponent();
            TutorialFrame.Content = new TutorialStep1();
        }
    }
}
