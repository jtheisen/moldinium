using Moldinium;
using Moldinium.Misc;
using SampleApp;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;

services.AddControllersWithViews().AddRazorRuntimeCompilation();

services.AddSingletonMoldiniumRoot<JobList>(c => c
    .SetMode(MoldiniumDefaultMode.Basic)
    .SetDefaultIListAndICollectionTypes(typeof(ConcurrentList<>), typeof(ConcurrentList<>))
    .IdentifyMoldiniumTypes(t => t.IsInterface && !t.Name.StartsWithFollowedByCapital("I"))
);

var app = builder.Build();

app.Services.ValidateMoldiniumRoot<JobList>();

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
