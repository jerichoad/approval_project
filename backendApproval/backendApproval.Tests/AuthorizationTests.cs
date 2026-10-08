using System.Net;
using System.Net.Http.Json;
using backendApproval.Tests.Infrastructure;

namespace backendApproval.Tests;

public sealed class AuthorizationTests(ApiFactory factory) : ApiTestBase(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Carol_cannot_approve_at_manager_stage()
    {
        var created = await CreateAsync(Users.Alice, Apps.Crm, "Production", "Read");
        var response = await ApproveAsync(Users.Carol, created.Id, created.Version);
        await AssertProblemCodeAsync(response, HttpStatusCode.Forbidden, "NOT_ASSIGNED_APPROVER");
    }

    [Fact]
    public async Task Dana_cannot_approve_crm_request()
    {
        var created = await CreateAsync(Users.Alice, Apps.Crm, "NonProduction", "Read");
        var response = await ApproveAsync(Users.Dana, created.Id, created.Version);
        await AssertProblemCodeAsync(response, HttpStatusCode.Forbidden, "NOT_ASSIGNED_APPROVER");
    }

    [Fact]
    public async Task Dana_cannot_approve_crm_with_wrong_version_still_403()
    {
        var created = await CreateAsync(Users.Alice, Apps.Crm, "NonProduction", "Read");
        var response = await ApproveAsync(Users.Dana, created.Id, 999);
        await AssertProblemCodeAsync(response, HttpStatusCode.Forbidden, "NOT_ASSIGNED_APPROVER");
    }

    [Fact]
    public async Task Erin_auditor_cannot_approve()
    {
        var created = await CreateAsync(Users.Alice, Apps.Crm, "NonProduction", "Read");
        var response = await ApproveAsync(Users.Erin, created.Id, created.Version);
        await AssertProblemCodeAsync(response, HttpStatusCode.Forbidden, "NOT_ASSIGNED_APPROVER");
    }

    [Fact]
    public async Task Erin_can_view_all_requests()
    {
        await CreateAsync(Users.Alice, Apps.Crm, "NonProduction", "Read");
        var response = await Factory.ClientAs(Users.Erin).GetAsync("/api/access-requests?scope=all");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<List<SummaryDto>>(Json);
        Assert.NotEmpty(data!);
    }

    [Fact]
    public async Task Non_auditor_gets_403_for_scope_all()
    {
        var response = await Factory.ClientAs(Users.Alice).GetAsync("/api/access-requests?scope=all");
        await AssertProblemCodeAsync(response, HttpStatusCode.Forbidden, "FORBIDDEN");
    }

    [Fact]
    public async Task Unknown_user_header_returns_401()
    {
        var response = await Factory.ClientAs(Users.Unknown).GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Forbidden_to_view_others_request()
    {
        var created = await CreateAsync(Users.Alice, Apps.Crm, "NonProduction", "Read");
        var response = await Factory.ClientAs(Users.Dana).GetAsync($"/api/access-requests/{created.Id}");
        await AssertProblemCodeAsync(response, HttpStatusCode.Forbidden, "FORBIDDEN");
    }
}
