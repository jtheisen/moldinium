using System.Reflection.Emit;
using System.Reflection;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Reflection.Metadata;
using System.Text;

namespace Moldinium;

public struct Blob : IEquatable<Blob>
{
    Byte[] bytes;
    Int32 hash;

    static HashAlgorithm hashAlgorithm;

    static String[] algorithmsToTry = new[] { "MD5", "SHA1" }; 

    static Blob()
    {
        foreach (var name in algorithmsToTry)
        {
            if (HashAlgorithm.Create(name) is HashAlgorithm found)
            {
                hashAlgorithm = found;

                return;
            }
        }

        throw new Exception($"The Platform supports none of these hash algorithms: {String.Join(", ", algorithmsToTry)}");
    }

    public Blob(Byte[] bytes)
    {
        this.bytes = bytes;

        var hashBytes = hashAlgorithm.ComputeHash(bytes);

        hash = BitConverter.ToInt32(hashBytes, 0);
    }

    public static implicit operator Blob(Byte[] bytes) => new Blob(bytes);

    public override string ToString()
    {
        return Convert.ToHexString(bytes);
    }

    public override int GetHashCode()
    {
        return hash;
    }

    public override bool Equals(object? obj)
    {
        return obj is Blob other ? Equals(other) : false;
    }

    public bool Equals(Blob other)
    {
        return bytes.AsSpan().SequenceEqual(other.bytes.AsSpan());
    }
}

public record EquatableMethodNameAndSignature(String MethodName, EquatableMethodSignature Signature);

public class ExtraMethodInfo
{
    public EquatableMethodNameAndSignature MethodNameAndSignature { get; }
    public MethodInfo Method { get; }
    public String? ContainerName { get; }
    public String UnqualifiedName { get; }
    public String QualifiedName { get; }
    public Boolean IsPrivateImplementation { get; }
    public Boolean IsImplemented { get; }
    public Boolean IsImplementable { get; }

    private String GetMetaCode() => this switch
    {
        { IsPrivateImplementation: true } => "p",
        { IsImplementable: true } and { IsImplemented: true } => "v",
        { IsImplementable: true} => "a",
        { IsImplemented: true } => "c",
        _ => "?"
    };

    public override string ToString()
    {
        return $"{GetMetaCode()};{QualifiedName}";
    }

    public ExtraMethodInfo(MethodInfo method)
    {
        var signature = new EquatableMethodSignature(method);

        IsImplemented = method.GetMethodBody() is not null;
        IsImplementable = !method.IsPrivate && !method.IsStatic && method.IsVirtual;
        Method = method;

        if (TryParsePrivateName(method.Name, out var containerName, out var baseMethodName))
        {
            UnqualifiedName = baseMethodName;
            ContainerName = containerName;
            QualifiedName = $"{containerName}.{baseMethodName}";
            IsPrivateImplementation = true;
        }
        else
        {
            containerName = method.DeclaringType?.FullName?.Replace('+', '.') ?? "";

            UnqualifiedName = method.Name;
            ContainerName = containerName;
            QualifiedName = $"{containerName}.{method.Name}";
            IsPrivateImplementation = false;
        }

        MethodNameAndSignature = new EquatableMethodNameAndSignature(UnqualifiedName, signature);
    }

    static Boolean TryParsePrivateName(String methodName, out String containerName, out String baseMethodName)
    {
        var lastDotAt = methodName.LastIndexOf('.');

        if (lastDotAt >= 0)
        {
            containerName = methodName[..lastDotAt];
            baseMethodName = methodName[(lastDotAt + 1)..];

            return true;
        }
        else
        {
            containerName = String.Empty;
            baseMethodName = String.Empty;

            return false;
        }
    }

}

public record EquatableMethodSignature(Blob SignatureBlob, Int32 GenericParameterCount)
{
    public EquatableMethodSignature(MethodInfo method)
        : this(GetSignature(method), GetGenericTypeParameterCount(method))
    {
    }

    static Int32 GetGenericTypeParameterCount(MethodInfo method)
    {
        var genericParameters = method.GetGenericArguments();

        if (genericParameters.Any(t => !t.IsGenericMethodParameter)) throw new Exception($"Expected {method} to have only generic type parameters");

        return genericParameters.Length;
    }

    static Byte[] GetSignature(MethodInfo method) => GetSignatureHelper(method).GetSignature();

    static SignatureHelper GetSignatureHelper(MethodInfo method)
    {
        var s = SignatureHelper.GetMethodSigHelper(method.CallingConvention, method.ReturnType);

        foreach (var p in method.GetParameters())
        {
            s.AddArgument(p.ParameterType, p.GetRequiredCustomModifiers(), p.GetOptionalCustomModifiers());
        }

        return s;
    }
}

public static class MethodSignatures
{
    static ConcurrentDictionary<MethodInfo, ExtraMethodInfo> signatures
        = new ConcurrentDictionary<MethodInfo, ExtraMethodInfo>();

    public static ExtraMethodInfo GetExtraMethodInfo(MethodInfo method)
        => signatures.GetOrAdd(method, m => new ExtraMethodInfo(m));

    public static EquatableMethodSignature GetSignature(MethodInfo method)
        => GetExtraMethodInfo(method).MethodNameAndSignature.Signature;
}

public abstract class AbstractSignatureHelper
{
    public abstract Byte[] GetSignature(MethodInfo methodInfo);
}

public class DotNetSignatureHelper : AbstractSignatureHelper
{
    public override byte[] GetSignature(MethodInfo method)
    {
        var s = SignatureHelper.GetMethodSigHelper(method.CallingConvention, method.ReturnType);

        foreach (var p in method.GetParameters())
        {
            s.AddArgument(p.ParameterType, p.GetRequiredCustomModifiers(), p.GetOptionalCustomModifiers());
        }

        return s.GetSignature();
    }
}

public class CustomSignatureHelper : AbstractSignatureHelper
{
    class SignatureEncoder
    {
        StringWriter writer = new StringWriter();

        public String GetString() => writer.ToString();

        public void EncodeMethod(MethodInfo method)
        {
            if (method.ReturnParameter is ParameterInfo returnParameter)
            {
                EncodeParameter(returnParameter, 0);
            }

            var paramters = method.GetParameters();

            for (var i = 0; i < paramters.Length; ++i)
            {
                EncodeParameter(paramters[i], i + 1);
            }
        }

        void EncodeParameter(ParameterInfo parameter, Int32 position)
        {
            writer.Write(position);
            writer.Write('[');
            EncodeType(parameter.ParameterType);
            foreach (var m in parameter.GetRequiredCustomModifiers())
            {
                EncodeModifier(m, true);
            }
            foreach (var m in parameter.GetOptionalCustomModifiers())
            {
                EncodeModifier(m, false);
            }
            EncodeType(parameter.ParameterType);

            writer.Write(']');
        }

        void EncodeModifier(Type modifier, Boolean isRequired)
        {
            writer.Write(isRequired ? 'r' : 'o');
            EncodeType(modifier);
        }

        void EncodeType(Type type)
        {
            writer.Write('(');
            writer.Write(type.Name);
            writer.Write('|');
            writer.Write(type.TypeHandle.Value.ToInt64());
            writer.Write(')');
        }
    }

    public override byte[] GetSignature(MethodInfo method)
    {
        var encoder = new SignatureEncoder();

        encoder.EncodeMethod(method);

        return Encoding.UTF8.GetBytes(encoder.GetString());
    }
}
