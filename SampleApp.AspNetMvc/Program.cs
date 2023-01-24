using Moldinium;
using Moldinium.Injection;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;

// Add services to the container.
services.AddControllersWithViews();

var configuration = new DefaultDependencyProviderConfiguration(
    Baking: DefaultDependencyProviderBakingMode.Basic,
    IsMoldiniumType: t => t.IsInterface && !t.Name.StartsWithFollowedByCapital("I")
);

services.AddMoldiniumRoot<JobList>(configuration);

var app = builder.Build();

var scope = app.Services.GetRequiredService<Scope<JobList>>();

Debug.WriteLine(scope.CreateDependencyReport());

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
