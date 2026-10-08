using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace backendApproval.Tests.Infrastructure;

public static class Users
{
    public const string Alice = "alice@example.local";
    public const string Bob = "bob@example.local";
    public const string Carol = "carol@example.local";
    public const string Dana = "dana@example.local";
    public const string Erin = "erin@example.local";
    public const string Unknown = "stranger@example.local";
}

public static class Apps
{
    public static readonly Guid Crm = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid FinancePortal = Guid.Parse("10000000-0000-0000-0000-000000000002");
}

public sealed record AuditDto(
    string EventType, string Actor, string? FromStatus, string ToStatus,
    string? Reason, int RequestVersion, DateTimeOffset OccurredAt);

public sealed record DetailDto(
    Guid Id,
    string ClientRequestId,
    string Environment,
    string AccessLevel,
    string Justification,
    string Status,
    string PolicyVersion,
    bool IsHighRisk,
    string? RejectionReason,
    int Version,
    DateTimeOffset? DecidedAt,
    List<string> AllowedActions,
    List<AuditDto> AuditTrail);

public sealed record SummaryDto(Guid Id, string Status, int Version);

public sealed record Envelope<T>(
    [property: System.Text.Json.Serialization.JsonPropertyName("Status")] string Status,
    [property: System.Text.Json.Serialization.JsonPropertyName("Data")] T Data);

public abstract class ApiTestBase(ApiFactory factory)
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected ApiFactory Factory { get; } = factory;

    protected static object CreateBody(Guid applicationId, string environment, string accessLevel,
                                       string? clientRequestId = null, string justification = "Butuh akses untuk test") =>
        new
        {
            clientRequestId = clientRequestId ?? Guid.NewGuid().ToString(),
            applicationId,
            environment,
            accessLevel,
            justification
        };

    protected static async Task<T> DataAsync<T>(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(Json);
        Assert.NotNull(envelope);
        Assert.Equal("S", envelope!.Status);
        return envelope.Data;
    }

    protected async Task<DetailDto> CreateAsync(string user, Guid app, string environment, string accessLevel)
    {
        var response = await Factory.ClientAs(user)
            .PostAsJsonAsync("/api/access-requests", CreateBody(app, environment, accessLevel));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await DataAsync<DetailDto>(response);
    }

    protected async Task<DetailDto> GetDetailAsync(string user, Guid id)
    {
        var response = await Factory.ClientAs(user).GetAsync($"/api/access-requests/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await DataAsync<DetailDto>(response);
    }

    protected Task<HttpResponseMessage> ApproveAsync(string user, Guid id, int expectedVersion) =>
        Factory.ClientAs(user).PostAsJsonAsync($"/api/access-requests/{id}/approve", new { expectedVersion });

    protected Task<HttpResponseMessage> RejectAsync(string user, Guid id, int expectedVersion, string? reason) =>
        Factory.ClientAs(user).PostAsJsonAsync($"/api/access-requests/{id}/reject", new { expectedVersion, reason });

    protected static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("E", body.GetProperty("Status").GetString());
        return body.GetProperty("Data");
    }

    protected static async Task AssertProblemCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await ProblemAsync(response);
        Assert.Equal(code, problem.GetProperty("code").GetString());
    }

    protected static void AssertAuditInvariant(DetailDto detail)
    {
        Assert.Equal(detail.Version, detail.AuditTrail.Count);
        Assert.Equal(detail.Status, detail.AuditTrail.Last().ToStatus);
        Assert.Equal(Enumerable.Range(1, detail.Version), detail.AuditTrail.Select(a => a.RequestVersion));
    }
}
