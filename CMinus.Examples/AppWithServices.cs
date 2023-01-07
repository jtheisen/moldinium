using System;

namespace CMinus.Examples.AppWithServices;

public record ConnectionString(String Value);

public interface Logger
{
    void Info(string message);
}

public interface Db
{
    Logger Log { get; init; }

    ConnectionString ConnectionString { get; init; }

    void Query()
    {
        Log.Info($"Querying with connection string {ConnectionString.Value}");
    }
}

public interface WatcherService
{
    Func<WatcherServiceJob> CreateJob { get; init; }

    void Run()
    {
        for (var i = 0; i < 3; ++i)
        {
            CreateJob().Run();
        }
    }
}

public interface WatcherServiceJob
{
    Db Db { get; init; }

    void Run()
    {
        Db.Query();
    }
}

public record AppSettings(String ConnectionString);

public interface App : Provider<ConnectionString>
{
    AppSettings Settings { get; init; }

    WatcherService WatcherService { get; init; }

    new ConnectionString Provide() => new ConnectionString(Settings.ConnectionString);

    void Run()
    {
        WatcherService.Run();
    }
}
