namespace EIP.Data.Connector.Application.Abstractions;

public interface IReferenceEndpointPolicy
{
    bool IsAllowed(string url);
}
