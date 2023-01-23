using Moldinium.Injection;
using Microsoft.Extensions.DependencyInjection;
using SampleApp;
using System;
using System.Windows;

namespace Moldinium.Tests.Wpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, ILogger
    {
        public JobListApp JobListApp { get; private set; } = null!;

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
                EnableOldModliniumModels: true,
                Services: serviceProvider,
                IsMoldiniumType: t => t.IsInterface && !t.Name.StartsWithFollowedByCapital("I")
            );

            var provider = DependencyProvider.Create(configuration);

            JobListApp = provider.CreateInstance<JobListApp>();
        }

        void ILogger.Log(string message)
        {
            Log?.Invoke(message);
        }
    }
}
