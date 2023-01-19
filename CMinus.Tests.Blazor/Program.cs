using CMinus;
using CMinus.Injection;
using SampleApp;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;

// Add services to the container.
services.AddRazorPages();
services.AddServerSideBlazor();

var configuration = new DefaultDependencyProviderConfiguration(
    Baking: DefaultDependencyProviderBakingMode.Tracking,
    BakeAbstract: false,
    EnableOldModliniumModels: true
);

var provider = DependencyProvider.Create(configuration);

services.AddScoped<IJobListVm>(sp => provider.CreateInstance<IJobListVm>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
