using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Aspire.Hosting.MockServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding MockServer resources to the application model.
/// </summary>
public static class MockServerResourceBuilderExtensions
{
    /// <summary>
    /// Adds a MockServer container resource to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="port">An optional fixed host port to bind to the MockServer container.</param>
    /// <param name="targetPort">An optional container port to listen on. Defaults to <c>1080</c>.</param>
    /// <param name="tag">The image tag to use. Defaults to <c>latest</c>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<MockServerResource> AddMockServer(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? port = null,
        int? targetPort = null,
        string tag = "latest")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var resource = new MockServerResource(name);

        var healthCheckKey = $"{name}-mockserver-status";
        builder.Services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                healthCheckKey,
                sp => new MockServerStatusHealthCheck(resource, sp.GetRequiredService<IHttpClientFactory>()),
                failureStatus: default,
                tags: default));

        builder.Services.TryAddEventingSubscriber<WsdlExpectationSubscriber>();

        return builder.AddResource(resource)
            .WithImage("mockserver/mockserver", tag)
            .WithHttpEndpoint(port: port, targetPort: targetPort ?? MockServerResource.DefaultContainerPort, name: MockServerResource.HttpEndpointName)
            .WithHealthCheck(healthCheckKey);
    }

    /// <summary>
    /// Registers a WSDL file with the MockServer resource. Once the container is ready, the WSDL is
    /// submitted to MockServer's <c>PUT /mockserver/wsdl</c> endpoint, generating one SOAP expectation
    /// per operation declared in the document.
    /// <para>
    /// The paths to expectations corresponding to each operation in the WSDL will be automatically published as URLs
    /// which can be seen in the resource details.
    /// </para>
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> for the MockServer resource.</param>
    /// <param name="wsdlFilePath">
    /// The path to the WSDL file, relative to the AppHost project directory unless rooted.
    /// </param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<MockServerResource> WithWsdlFile(
        this IResourceBuilder<MockServerResource> builder,
        string wsdlFilePath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(wsdlFilePath);

        var resolvedPath = Path.IsPathRooted(wsdlFilePath)
            ? wsdlFilePath
            : Path.Combine(builder.ApplicationBuilder.AppHostDirectory, wsdlFilePath);

        builder.Resource.Annotations.Add(new WsdlAnnotation(resolvedPath));

        return builder;
    }
}
