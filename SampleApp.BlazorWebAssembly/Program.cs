using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Moldinium;
using Moldinium.Injection;
using SampleApp;
using SampleApp.BlazorWebAssembly;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var services = builder.Services;

services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

var configuration = new DefaultDependencyProviderConfiguration(
    Baking: DefaultDependencyProviderBakingMode.Tracking,
    BakeAbstract: false,
    IsMoldiniumType: t => t.IsInterface && !t.Name.StartsWithFollowedByCapital("I")
);

var provider = DependencyProvider.Create(configuration);

services.AddScoped<JobListApp>(sp => provider.CreateInstance<JobListApp>());

await builder.Build().RunAsync();
