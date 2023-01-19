using CMinus.Injection;
using SampleApp;
using System.Windows;

namespace CMinus.Tests.Wpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public IJobListApp JobListApp { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var configuration = new DefaultDependencyProviderConfiguration(
                Baking: DefaultDependencyProviderBakingMode.TrackingAndNotifyPropertyChanged,
                BakeAbstract: false,
                EnableOldModliniumModels: true
            );

            var provider = DependencyProvider.Create(configuration);

            JobListApp = provider.CreateInstance<IJobListApp>();
        }
    }
}
