using System.Net;

namespace SampleApp;

public interface ITaskListVm
{
    Func<ITaskConfig> CreateTaskConfig { get; init; }

    ITaskList TaskList { get; init; }
}

public interface ITaskList
{
    Func<ITaskList, ITaskConfig, ITaskListItem> CreateItem { get; init; }

    IList<ITaskListItem> Items { get; set; }

    void Remove(ITaskListItem item) => Items.Remove(item);

    void Add(ITaskConfig config) => Items.Add(CreateItem(this, config));
}

public interface ITaskConfig
{
    String Url { get; set; }
}

public interface ITaskListItem
{
    Func<ITaskRequest> CreateRequest { get; init; }

    ITaskList Owner { get; init; }

    ITaskConfig Config { get; init; }

    Boolean IsEditing { get; set; }

    Boolean CanEdit { get; set; }

    ITaskRequest? CurrentRequest { get; set; }

    void Remove() => Owner.Remove(this);

    void Start()
    {
        CurrentRequest = CreateRequest();

        CurrentRequest.Request();
    }
}

public interface ITaskRequest
{
    ITaskConfig Config { get; init; }

    HttpClient HttpClient { get; init; }

    Boolean IsCompleted { get; set; }

    HttpStatusCode StatusCode { get; set; }

    Exception? Exception { get; set; }

    async void Request()
    {
        try
        {
            using var response = await HttpClient.GetAsync(Config.Url);

            StatusCode = response.StatusCode;
        }
        catch (Exception ex)
        {
            Exception = ex;
        }
        finally
        {
            IsCompleted = true;
        }
    }
}
