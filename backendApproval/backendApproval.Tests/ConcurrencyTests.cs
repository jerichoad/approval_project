using System.Net;
using System.Net.Http.Json;
using backendApproval.Tests.Infrastructure;

namespace backendApproval.Tests;

public sealed class ConcurrencyTests(ApiFactory factory) : ApiTestBase(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Two_concurrent_approvals_on_same_version_only_one_succeeds()
    {
        var created = await CreateAsync(Users.Alice, Apps.Crm, "Production", "Read");

        var body = new { expectedVersion = created.Version };
        var responses = await Task.WhenAll(
            Factory.ClientAs(Users.Bob).PostAsJsonAsync($"/api/access-requests/{created.Id}/approve", body),
            Factory.ClientAs(Users.Bob).PostAsJsonAsync($"/api/access-requests/{created.Id}/approve", body));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var detail = await GetDetailAsync(Users.Alice, created.Id);
        Assert.Equal("PendingSystemOwnerApproval", detail.Status);
        Assert.Equal(2, detail.Version);
        Assert.Single(detail.AuditTrail, e => e.EventType == "ManagerApproved");
    }

    [Fact]
    public async Task Approve_with_stale_version_returns_409_with_current_version()
    {
        var created = await CreateAsync(Users.Alice, Apps.Crm, "Production", "Read");

        var approve = await ApproveAsync(Users.Bob, created.Id, created.Version);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var stale = await ApproveAsync(Users.Bob, created.Id, created.Version);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var problem = await ProblemAsync(stale);
        Assert.Equal("STALE_VERSION", problem.GetProperty("code").GetString());
        Assert.Equal(2, problem.GetProperty("currentVersion").GetInt32());
        Assert.Equal("PendingSystemOwnerApproval", problem.GetProperty("currentStatus").GetString());
    }
}
