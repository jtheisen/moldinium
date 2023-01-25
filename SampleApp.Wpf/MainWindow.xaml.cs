using Moldinium.Injection;
using SampleApp;
using System.Windows;

namespace SampleApp.Wpf
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

            var jobList = app.JobList;

            JobList.DataContext = jobList;
        }
    }
}
