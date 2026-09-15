using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspire.Hosting.MockServer;

// MockServer's readiness endpoint requires a PUT (not GET), so the built-in
// WithHttpHealthCheck helper can't be used directly.
internal sealed class MockServerStatusHealthCheck(MockServerResource resource, IHttpClientFactory httpClientFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!resource.TryGetEndpoints(out var endpoints) || endpoints.SingleOrDefault(e => e.Name == MockServerResource.HttpEndpointName) is not { } endpoint)
        {
            return HealthCheckResult.Unhealthy("MockServer endpoint is not yet allocated");
        }

        var uri = new Uri(await resource.UriExpression.GetValueAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty);

        try
        {
            using var httpClient = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Put, new Uri(uri, "/mockserver/status"));
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"MockServer status endpoint returned {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Failed to reach MockServer status endpoint", ex);
        }
    }
}
