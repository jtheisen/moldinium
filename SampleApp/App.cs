namespace SampleApp;

public interface IJobListVm
{
    IJobList JobList { get; init; }
}

public interface IJobList
{
    Func<SimpleJob> NewSimpleJob { get; init; }
    Func<ComplexJob> NewComplexJob { get; init; }

    CancellationTokenSource Cts { get; set; }

    IList<IJob> Items { get; set; }

    void AddSimpleJob() => AddAndRunJob(NewSimpleJob());
    void AddComplexJob() => AddAndRunJob(NewComplexJob());

    async void AddAndRunJob(IJob job)
    {
        Items.Add(job);

        await job.Run();

        await Task.Delay(1000);

        Items.Remove(job);
    }

    void Cancel()
    {
        Cts.Cancel();

        Cts = new CancellationTokenSource();
    }
}

public interface IJob
{
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
            Exception = ex;
        }
        finally
        {
            HasEnded = true;
        }
    }

    Task RunImpl();
}

public interface SimpleJob : IJob
{
    CancellationToken Ct { get; init; }

    async Task IJob.RunImpl() => await Task.Delay(4000, Ct);
}

public interface ComplexJob : IJob
{
    Func<SimpleJob> CreateSimpleJob { get; init; }

    List<IJob> SubJobs { get; set; }

    async Task IJob.RunImpl()
    {
        for (var i = 0; i < 3; ++i)
        {
            var subJob = CreateSimpleJob();

            SubJobs.Add(subJob);

            await subJob.Run();
        }
    }
}
