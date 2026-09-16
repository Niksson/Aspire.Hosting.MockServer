using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MockServer.Client;
using MockServer.Client.Models;
using System.Collections.Immutable;

namespace Aspire.Hosting.MockServer;

/// <summary>
/// Handles <see cref="ResourceReadyEvent"/> for every <see cref="MockServerResource"/> in the
/// application model by importing its registered <see cref="WsdlAnnotation"/>s and publishing a
/// URL for each generated SOAP expectation.
/// </summary>
internal sealed class WsdlExpectationSubscriber : IDistributedApplicationEventingSubscriber
{
    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken)
    {
        eventing.Subscribe<ResourceReadyEvent>((@event, ct) => @event.Resource is MockServerResource resource
            ? HandleAsync(resource, @event, ct)
            : Task.CompletedTask);

        return Task.CompletedTask;
    }

    private static async Task HandleAsync(MockServerResource resource, ResourceReadyEvent @event, CancellationToken cancellationToken)
    {
        if (!resource.TryGetAnnotationsOfType<WsdlAnnotation>(out var wsdlAnnotations))
        {
            return;
        }

        var loggerService = @event.Services.GetRequiredService<ResourceLoggerService>();
        var logger = loggerService.GetLogger(resource);

        foreach (var annotation in wsdlAnnotations)
        {
            try
            {
                var wsdl = await File.ReadAllTextAsync(annotation.FilePath, cancellationToken).ConfigureAwait(false);
                var uri = new Uri(await resource.UriExpression.GetValueAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty);

                using var client = new MockServerClient(uri.Host, uri.Port, secure: uri.Scheme == "https");
                var expectations = await client.WsdlExpectationAsync(wsdl).ConfigureAwait(false);

                logger.LogInformation(
                    "Registered {Count} SOAP expectation(s) from WSDL {WsdlFile} on MockServer resource {ResourceName}",
                    expectations.Count,
                    annotation.FilePath,
                    resource.Name);

                await PublishExpectationUrlsAsync(resource, uri, expectations, @event.Services, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to import WSDL {WsdlFile} into MockServer resource {ResourceName}", annotation.FilePath, resource.Name);
            }
        }
    }

    private static Task PublishExpectationUrlsAsync(
        MockServerResource resource,
        Uri baseUri,
        List<Expectation> expectations,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var urls = expectations
            .Where(e => !string.IsNullOrEmpty(e.HttpRequest?.Path))
            .Select(e => new UrlSnapshot(MockServerResource.HttpEndpointName, new Uri(baseUri, e.HttpRequest!.Path!).ToString(), IsInternal: true)
            {
                DisplayProperties = new UrlDisplayPropertiesSnapshot(GetOperationName(e))
            })
            .ToImmutableArray();

        if (urls.IsEmpty)
        {
            return Task.CompletedTask;
        }

        var notificationService = services.GetRequiredService<ResourceNotificationService>();
        return notificationService.PublishUpdateAsync(resource, snapshot => snapshot with { Urls = snapshot.Urls.AddRange(urls) });
    }

    // The WSDL importer names each expectation "{ServiceName}.{OperationName}".
    private static string GetOperationName(Expectation expectation)
    {
        var id = expectation.Id;
        if (string.IsNullOrEmpty(id))
        {
            return expectation.HttpRequest?.Path ?? "operation";
        }

        return id.Split('.').Last();
    }
}
