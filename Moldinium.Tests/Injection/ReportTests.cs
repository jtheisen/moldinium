using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;
using Moldinium;

namespace Testing.Injection;

[TestClass]
public class ReportTests
{
    interface App
    {
        Func<CancellationToken, Job> NewJob { get; init; }

        async Task Run(CancellationToken ct)
        {
            await using var job = NewJob(ct);

            await job.Run();
        }
    }

    interface Job : IAsyncDisposable
    {
        Func<RequestConfig, Request> NewRequest { get; init; }

        async Task Run()
        {
            var request = NewRequest(new RequestConfig(new Uri("...")));

            await request.FetchContent();
        }

        // Our job needs this for mysterious reasons
        async ValueTask IAsyncDisposable.DisposeAsync() { /* ... */ }
    }

    record RequestConfig(Uri Url);

    interface Request
    {
        RequestConfig Config { get; init; }

        CancellationToken Ct { get; init; }

        HttpClient HttpClient { get; init; }

        async Task FetchContent() { /* ... */ }
    }

    [TestMethod]
    public void ReportTest()
    {
        var services = MoldiniumServices.Create<App>(c => c.SetMode(MoldiniumDefaultMode.Basic), services => services
            .AddHttpClient()
        );

        services.ValidateMoldiniumRoot<App>(out var report);

        Console.WriteLine(report);
    }
}
