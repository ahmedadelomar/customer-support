using CustomerSupport.Infrastructure.Persistence.Interceptors;

namespace CustomerSupport.Api.Services;

/// <summary>Supplies request metadata to the audit interceptor without leaking HTTP types downward.</summary>
public class AuditContextAccessor(IHttpContextAccessor accessor) : IAuditContextAccessor
{
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => Truncate(accessor.HttpContext?.Request.Headers.UserAgent.ToString(), 512);

    public string? CorrelationId => accessor.HttpContext?.TraceIdentifier;

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= max ? value : value[..max];
}
