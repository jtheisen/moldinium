using System.ComponentModel;

namespace Moldinium.Internals;

public interface INotifyingPropertyMixin : INotifyPropertyChanged
{
    void NotifyPropertyChanged(object o);
}

public interface ITracked { }

public struct TrackedPropertyMixin : ITracked { }

