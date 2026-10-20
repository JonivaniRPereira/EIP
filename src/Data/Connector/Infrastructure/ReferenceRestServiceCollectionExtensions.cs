using Microsoft.Extensions.DependencyInjection;

namespace EIP.Data.Connector.Infrastructure;

public static class ReferenceRestServiceCollectionExtensions
{
    public static IServiceCollection AddReferenceRestHttpClient(this IServiceCollection services)
    {
        services.AddHttpClient(HttpReferenceRestClient.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        return services;
    }
}
