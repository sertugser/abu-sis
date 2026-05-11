namespace SIS.API.Security;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class PublicEndpointJustificationAttribute : Attribute
{
    public PublicEndpointJustificationAttribute(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A public endpoint justification is required.", nameof(reason));

        Reason = reason;
    }

    public string Reason { get; }
}
