using CMinus.Injection;
using SampleApp;
using System.Windows;

namespace CMinus.Tests.Wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var app = (App)Application.Current;

            JobList.DataContext = app.JobListApp.CreateDefaultJobList();
        }
    }
}
