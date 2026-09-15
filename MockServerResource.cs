namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a MockServer container.
/// </summary>
public class MockServerResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string HttpEndpointName = "http";
    internal const int DefaultContainerPort = 1080;

    private EndpointReference? _primaryEndpointReference;

    /// <summary>
    /// Gets the primary HTTP endpoint for the MockServer container.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpointReference ??= new(this, HttpEndpointName);

    /// <summary>
    /// Gets the host of the MockServer container.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port of the MockServer container.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);

    /// <summary>
    /// Gets the URI expression for the MockServer control-plane / data-plane endpoint.
    /// </summary>
    public ReferenceExpression UriExpression =>
        ReferenceExpression.Create($"{PrimaryEndpoint.Property(EndpointProperty.Scheme)}://{Host}:{Port}");

    /// <inheritdoc/>
    public ReferenceExpression ConnectionStringExpression => UriExpression;

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("Uri", UriExpression);
    }
}
