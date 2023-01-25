namespace Moldinium.Combined;

public interface IScopedMethodImplementation : IImplementation
{
    bool Before();

    void After();

    bool AfterError();
}

public struct ScopedMethodImplementation : IScopedMethodImplementation
{
    // Tracking needs to implement defering scopes

    public bool Before() => true;

    public void After() { }

    public bool AfterError() => true;
}
