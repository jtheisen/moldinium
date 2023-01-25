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

            services.AddSingletonMoldiniumRoot<SampleApp.JobList>(c => c
                .SetMode(MoldiniumDefaultMode.TrackingAndNotifyPropertyChanged)
                .IdentifyMoldiniumTypes(t => t.IsInterface && !t.Name.StartsWithFollowedByCapital("I"))
            );

            var serviceProvider = services.BuildServiceProvider();

            JobList = serviceProvider.GetRequiredService<SampleApp.JobList>();
        }

        void ILogger.Log(string message)
        {
            Log?.Invoke(message);
        }
    }
}
