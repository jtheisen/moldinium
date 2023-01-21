namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class IgnoresAccessChecksToAttribute : Attribute
{
    public IgnoresAccessChecksToAttribute(String assemblyName)
    {
        AssemblyName = assemblyName;
    }

    public string AssemblyName { get; }
}