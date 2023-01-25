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

services.AddScoped<JobList>(sp => provider.CreateInstance<JobList>());

await builder.Build().RunAsync();
