namespace SampleApp;

/* The root of our application
 * 
 * The interface has no "I" prefix as it's conceptually no longer an "interface".
 */
public interface JobList
{
    ILogger? Logger { get; init; }

    Func<CommandConfig, Command> NewCommand { get; init; }

    Func<CancellationToken, SimpleJob> NewSimpleJob { get; init; }
    Func<CancellationToken, ComplexJob> NewComplexJob { get; init; }

    CancellationTokenSource? Cts { get; set; }

    Boolean HaveCancellableJobs => Cts is not null;

    CancellationToken Ct => GetCts().Token;

    IList<IJob> Items { get; set; }

    CancellationTokenSource GetCts() => Cts ?? (Cts = new CancellationTokenSource());

    Command AddSimpleJobCommand => MakeCommand(() => AddAndRunJob(NewSimpleJob(Ct)));
    Command AddComplexJobCommand => MakeCommand(() => AddAndRunJob(NewComplexJob(Ct)));
    Command CancelCommand => MakeCommand(() => Cancel(), HaveCancellableJobs);

    Command MakeCommand(Action execute, Boolean isEnabled = true)
        => NewCommand(new CommandConfig(execute, isEnabled));

    async void AddAndRunJob(IJob job)
    {
        Items.Add(job);

        await job.Run();

        await Task.Delay(3000);

        Items.Remove(job);

        if (Items.Count == 0)
        {
            Cts = null;
        }
    }

    void Cancel()
    {
        Cts?.Cancel();

        Logger?.Log($"Cancelled at {DateTime.Now}");

        Cts = null;
    }
}

/* We can still use polymorphism, and this type is indeed an
 * interface semantically, hence the "I" in "IJob".
 */

public record JobNestingLevel(Int32 Level);

public interface IJob
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

/* "SimpleJob"s are the first implementation.
 */

public record SimpleJobConfig(TimeSpan Duration, Int32 Steps);

public interface SimpleJob : IJob
{
    SimpleJobConfig? Config { get; init; }

    Int32 Progress { get; set; }

    async Task IJob.RunImpl()
    {
        var config = Config ?? new SimpleJobConfig(TimeSpan.FromSeconds(3), 50);

        var n = config.Steps;

        var ms = config.Duration.TotalMilliseconds;

        for (var i = 0; i < n; ++i)
        {
            Ct.ThrowIfCancellationRequested();

            await Task.Delay((Int32)(ms / n));

            Progress = 100 * i / n;
        }

        Progress = 100;
    }
}

/* "ComplexJob"s are the second implementation and use nested "SimpleJob"s.
 */

public interface ComplexJob : IJob
{
    Func<JobNestingLevel, SimpleJob> CreateSimpleJob { get; init; }

    IList<IJob> SubJobs { get; set; }

    async Task IJob.RunImpl()
    {
        for (var i = 0; i < 3; ++i)
        {
            var subJob = CreateSimpleJob(new JobNestingLevel((NestingLevel?.Level ?? 0) + 1));

            SubJobs.Add(subJob);

            await subJob.Run();
        }

        await Task.Delay(1000, Ct);
    }
}

/* Commands are usually not used anywhere but in the XAML world, but they can
 * be, and we want to one app to be nicely bindable to all UI frameworks.
 * 
 * It also could as well be a class here, but that I could no longer claim that
 * this sample app only uses interfaces and records.
 */

public record CommandConfig(Action Execute, Boolean CanExecute = true);

public interface Command : System.Windows.Input.ICommand
{
    CommandConfig Config { get; init; }

    event EventHandler? System.Windows.Input.ICommand.CanExecuteChanged { add { } remove { } }

    Boolean IsDisabled => !Config.CanExecute;

    Boolean System.Windows.Input.ICommand.CanExecute(Object? parameter) => Config.CanExecute;

    void System.Windows.Input.ICommand.Execute(Object? parameter) => Config.Execute();
}

/* This is to demonstrate how a concrete implementation can be injected
 * from outside this application.
 */

public interface ILogger
{
    void Log(String message);
}
