namespace Moldinium.Common.Misc;

class TypeNameWriter
{
    public StringWriter writer = new StringWriter();

    public void Visit(Type type)
    {
        var name = type.Name;

        var lastBacktickAt = name.LastIndexOf('`');

        if (lastBacktickAt >= 0)
        {
            name = name[0..lastBacktickAt];
        }

        writer.Write(name);

        if (type.IsGenericType)
        {
            writer.Write("<");
            foreach (var arg in type.GetGenericArguments())
            {
                if (arg.IsGenericTypeParameter)
                {
                    writer.Write("*");
                }
                else
                {
                    Visit(arg);
                }
            }
            writer.Write(">");
        }
    }
}

public static class TypeExtensions
{
    public static String GetNameWithGenericArguments(this Type type)
    {
        var writer = new TypeNameWriter();
        writer.Visit(type);
        return writer.writer.ToString();
    }
}