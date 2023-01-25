using System.ComponentModel;

namespace Moldinium.Combined;

public interface INotifyingPropertyMixin : INotifyPropertyChanged
{
    void NotifyPropertyChanged(object o);
}

public interface ITracked { }

public struct TrackedPropertyMixin : ITracked { }

