using System.Diagnostics;
using System.Security.Cryptography;

namespace SampleApp;

public interface JobListApp
{
    // FIXME: passing job nesting level shouldn't be necessary
    Func<SimpleJobConfig, JobNestingLevel, JobList> CreateJobList { get; init; }

    JobList CreateDefaultJobList()
    {
        return CreateJobList(new SimpleJobConfig(TimeSpan.FromSeconds(3), 100), new JobNestingLevel(0));
    }
}

public record CommandConfig(Action Execute, Boolean CanExecute = true);

public interface Command : System.Windows.Input.ICommand
{
    CommandConfig Config { get; init; }

    event EventHandler? System.Windows.Input.ICommand.CanExecuteChanged { add { } remove { } }

    bool System.Windows.Input.ICommand.CanExecute(object? parameter) => Config.CanExecute;

    void System.Windows.Input.ICommand.Execute(object? parameter) => Config.Execute();
}

public interface ILogger
{
    void Log(String message);
}

public interface JobList
{
    Func<CommandConfig, Command> NewCommand { get; init; }

    Func<CancellationToken, SimpleJob> NewSimpleJob { get; init; }
    Func<CancellationToken, ComplexJob> NewComplexJob { get; init; }

    CancellationTokenSource? Cts { get; set; }

    CancellationToken Ct => GetCts().Token;

    IList<Job> Items { get; set; }

    CancellationTokenSource GetCts() => Cts ?? (Cts = new CancellationTokenSource());

    Command AddSimpleJobCommand => NewCommand(new CommandConfig(() => AddAndRunJob(NewSimpleJob(Ct))));
    Command AddComplexJobCommand => NewCommand(new CommandConfig(() => AddAndRunJob(NewComplexJob(Ct))));
    Command CancelCommand => NewCommand(new CommandConfig(() => Cancel()));

    async void AddAndRunJob(Job job)
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

public record JobNestingLevel(Int32 Level);

public interface Job
{
    CancellationToken Ct { get; init; }

    JobNestingLevel? NestingLevel { get; init; }

    ILogger? Logger { get; init; }

    Boolean HasEnded { get; set; }

    Exception? Exception { get; set; }

    String StatusString => this switch
    {
        { Exception: OperationCanceledException } => "cancelled",
        { Exception: Exception e } => $"error: {e.Message}",
        { HasEnded: true } => "completed",
        _ => "running"
    };

    async Task Run()
    {
        try
        {
            await RunImpl();
        }
        catch (Exception ex)
        {
            Logger?.Log($"Job aborted at {DateTime.Now}");

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

public interface SimpleJob : Job
{
    SimpleJobConfig Config { get; init; }

    Int32 Progress { get; set; }

    async Task Job.RunImpl()
    {
        var n = Config.Steps;

        var ms = Config.Duration.TotalMilliseconds;

        for (var i = 0; i < n; ++i)
        {
            await Task.Delay((Int32)(ms / n), Ct);

            Progress = 100 * i / n;
        }

        Progress = 100;
    }
}

public interface ComplexJob : Job
{
    Func<JobNestingLevel, SimpleJob> CreateSimpleJob { get; init; }

    IList<Job> SubJobs { get; set; }

    async Task Job.RunImpl()
    {
        for (var i = 0; i < 3; ++i)
        {
            var subJob = CreateSimpleJob(new JobNestingLevel(NestingLevel?.Level ?? 0 + 1));

            SubJobs.Add(subJob);

            await subJob.Run();
        }

        await Task.Delay(1000, Ct);
    }
}
