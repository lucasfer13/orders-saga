namespace Orders.Api;

/// <summary>
/// Anchors WebApplicationFactory to this assembly. Every service has its own
/// Program in the global namespace, so Program itself is ambiguous from a test
/// project that references more than one of them.
/// </summary>
public sealed class ApiMarker;
