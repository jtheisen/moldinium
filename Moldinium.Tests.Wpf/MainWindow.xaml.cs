using Moldinium.Injection;
using SampleApp;
using System.Windows;

namespace Moldinium.Tests.Wpf
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

            var jobList = app.JobListApp.JobList;

            jobList.AddSimpleJobCommand.Execute(null);

            JobList.DataContext = jobList;
        }
    }
}
