namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Annotation recording a WSDL file registered against a <see cref="MockServerResource"/>.
/// </summary>
/// <param name="FilePath">The resolved, absolute path to the WSDL file.</param>
public sealed record WsdlAnnotation(string FilePath) : IResourceAnnotation;
