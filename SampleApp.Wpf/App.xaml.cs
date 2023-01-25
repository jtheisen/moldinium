using Moldinium.Injection;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using Moldinium;

namespace SampleApp.Wpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, ILogger
    {
        public SampleApp.JobList JobList { get; private set; } = null!;

        public Action<String>? Log { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();
            services.AddSingleton<ILogger>(this);
            var serviceProvider = services.BuildServiceProvider();

            var configuration = new DefaultDependencyProviderConfiguration(
                Baking: DefaultDependencyProviderBakingMode.TrackingAndNotifyPropertyChanged,
                BakeAbstract: false,
                Services: serviceProvider,
                IsMoldiniumType: t => t.IsInterface && !t.Name.StartsWithFollowedByCapital("I")
            );

            var provider = DependencyProvider.Create(configuration);

            JobList = provider.CreateInstance<SampleApp.JobList>();
        }

        void ILogger.Log(string message)
        {
            Log?.Invoke(message);
        }
    }
}
