using System.Diagnostics;

namespace SampleApp;

public interface IJobListApp
{
    Func<SimpleJobConfig, IJobList> CreateJobList { get; init; }

    IJobList CreateDefaultJobList()
    {
        return CreateJobList(new SimpleJobConfig(TimeSpan.FromSeconds(3), 100));
    }
}

public record CommandConfig(Action Execute, Boolean CanExecute = true);

public interface ICommand : System.Windows.Input.ICommand
{
    CommandConfig Config { get; init; }

    event EventHandler? System.Windows.Input.ICommand.CanExecuteChanged { add { } remove { } }

    bool System.Windows.Input.ICommand.CanExecute(object? parameter) => Config.CanExecute;

    void System.Windows.Input.ICommand.Execute(object? parameter) => Config.Execute();
}

public interface IJobList
{
    Func<CommandConfig, ICommand> NewCommand { get; init; }

    Func<CancellationToken, SimpleJob> NewSimpleJob { get; init; }
    Func<CancellationToken, ComplexJob> NewComplexJob { get; init; }

    CancellationTokenSource? Cts { get; set; }

    CancellationToken Ct => GetCts().Token;

    IList<IJob> Items { get; set; }

    CancellationTokenSource GetCts() => Cts ?? (Cts = new CancellationTokenSource());

    ICommand AddSimpleJobCommand => NewCommand(new CommandConfig(() => AddAndRunJob(NewSimpleJob(Ct))));
    ICommand AddComplexJobCommand => NewCommand(new CommandConfig(() => AddAndRunJob(NewComplexJob(Ct))));
    ICommand CancelCommand => NewCommand(new CommandConfig(() => Cancel()));

    async void AddAndRunJob(IJob job)
    {
        Items.Add(job);

        await job.Run();

        await Task.Delay(3000);

        Items.Remove(job);
    }

    void Cancel()
    {
        Cts?.Cancel();

        Debug.WriteLine($"Cancelled at {DateTime.Now}");

        Cts = null;
    }
}

public interface IJob
{
    CancellationToken Ct { get; init; }

    Boolean HasEnded { get; set; }

    Exception? Exception { get; set; }

    async Task Run()
    {
        try
        {
            await RunImpl();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Job cancelled at {DateTime.Now}");

            Exception = ex;
        }
        finally
        {
            HasEnded = true;
        }
    }

    Task RunImpl();
}

public record SimpleJobConfig(TimeSpan Duration, Int32 Steps);

public interface SimpleJob : IJob
{
    SimpleJobConfig Config { get; init; }

    Int32 Progress { get; set; }

    async Task IJob.RunImpl()
    {
        var n = Config.Steps;

        var ms = Config.Duration.TotalMilliseconds;

        for (var i = 0; i < n; ++i)
        {
            await Task.Delay((Int32)(ms / n), Ct);

            Progress = 100 * i / n;
        }
    }
}

public interface ComplexJob : IJob
{
    Func<SimpleJob> CreateSimpleJob { get; init; }

    IList<IJob> SubJobs { get; set; }

    async Task IJob.RunImpl()
    {
        for (var i = 0; i < 3; ++i)
        {
            var subJob = CreateSimpleJob();

            SubJobs.Add(subJob);

            await subJob.Run();
        }

        await Task.Delay(1000, Ct);
    }
}
