using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Moldinium;
using SampleApp;
using SampleApp.BlazorWebAssembly;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var services = builder.Services;

services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

services.AddSingletonMoldiniumRoot<JobList>(c => c
    .SetMode(MoldiniumDefaultMode.Tracking)
    .IdentifyMoldiniumTypes(t => t.IsInterface && !t.Name.StartsWithFollowedByCapital("I"))
);

var host = builder.Build();

host.Services.ValidateMoldiniumRoot<JobList>();

await host.RunAsync();
