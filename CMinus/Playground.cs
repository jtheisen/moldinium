using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CMinus
{
    public interface Url
    {
        String Value { get; set; }
    }

    public interface LoggingVariableImplementation<T> : Implementation<Property<T>>
    {
        Property<T> Nested { get; }

        void Log(String text);

        T Value
        {
            get
            {
                Log("get");
                return Nested.Value;
            }
            set
            {
                Log("set");
                Nested.Value = value;
            }
        }
    }

    //public interface WebClientProvider : Provider<WebClient>
    //{
    //    new WebClient Get() => new WebClient();
    //}


    //public interface DefaultConstructingProvider<T> : Provider<T>
    //    where T : class, new()
    //{
    //    new T Get() => new T();
    //}



    //public interface Requests : Requires<WebClient>
    //{
    //    Task<String> DownloadString(Url url)
    //        => GetService().DownloadStringTaskAsync(url.Value);
    //}

    public interface PlaygroundSecrets
    {

    }

    public interface Playground
    {
        //String GetSomeString();

        //Action InjectedAction { get; }

        //String GetTruth();

        String Name { get; set; }

        //IPreacher Preacher { get; }

        void MethodWithImplementation() => Console.WriteLine($"My name is {Name}");
    }

    public interface IPreacher
    {
        String Truth { get; }
    }

    public class Preacher : IPreacher
    {
        public String Truth => "Tell it!";
    }
}
