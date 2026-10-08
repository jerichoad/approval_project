using System.Net;
using System.Net.Http.Json;
using backendApproval.Data;
using backendApproval.Domain.Entities;
using backendApproval.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace backendApproval.Tests;

public sealed class WorkflowTests(ApiFactory factory) : ApiTestBase(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Standard_request_is_approved_after_manager_approval()
    {
        var created = await CreateAsync(Users.Alice, Apps.Crm, "NonProduction", "Read");
        Assert.Equal("PendingManagerApproval", created.Status);
        Assert.False(created.IsHighRisk);

        var response = await ApproveAsync(Users.Bob, created.Id, created.Version);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await GetDetailAsync(Users.Alice, created.Id);
        Assert.Equal("Approved", detail.Status);
        Assert.Equal(2, detail.Version);
        Assert.NotNull(detail.DecidedAt);
        AssertAuditInvariant(detail);
    }

    [Fact]
    public async Task Production_request_requires_system_owner_then_approved()
    {
        var created = await CreateAsync(Users.Alice, Apps.Crm, "Production", "Read");
        Assert.True(created.IsHighRisk);

        var managerApprove = await ApproveAsync(Users.Bob, created.Id, created.Version);
        Assert.Equal(HttpStatusCode.OK, managerApprove.StatusCode);

        var afterManager = await GetDetailAsync(Users.Alice, created.Id);
        Assert.Equal("PendingSystemOwnerApproval", afterManager.Status);
        Assert.Equal(2, afterManager.Version);

        var ownerApprove = await ApproveAsync(Users.Carol, created.Id, afterManager.Version);
        Assert.Equal(HttpStatusCode.OK, ownerApprove.StatusCode);

        var final = await GetDetailAsync(Users.Alice, created.Id);
        Assert.Equal("Approved", final.Status);
        Assert.Equal(3, final.Version);
        AssertAuditInvariant(final);
    }

    [Fact]
    public async Task Admin_access_on_finance_portal_waits_for_dana()
    {
        var created = await CreateAsync(Users.Alice, Apps.FinancePortal, "NonProduction", "Admin");
        Assert.True(created.IsHighRisk);

        var managerApprove = await ApproveAsync(Users.Bob, created.Id, created.Version);
        Assert.Equal(HttpStatusCode.OK, managerApprove.StatusCode);

        var afterManager = await GetDetailAsync(Users.Alice, created.Id);
        Assert.Equal("PendingSystemOwnerApproval", afterManager.Status);

        var ownerApprove = await ApproveAsync(Users.Dana, created.Id, afterManager.Version);
        Assert.Equal(HttpStatusCode.OK, ownerApprove.StatusCode);

        var final = await GetDetailAsync(Users.Alice, created.Id);
        Assert.Equal("Approved", final.Status);
    }

    [Fact]
    public async Task Reject_stores_reason_and_audit_and_blocks_further_actions()
    {
        var created = await CreateAsync(Users.Alice, Apps.Crm, "NonProduction", "Read");

        var rejectResponse = await RejectAsync(Users.Bob, created.Id, created.Version, "Justifikasi tidak cukup jelas");
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);

        var detail = await GetDetailAsync(Users.Alice, created.Id);
        Assert.Equal("Rejected", detail.Status);
        Assert.Equal("Justifikasi tidak cukup jelas", detail.RejectionReason);
        Assert.NotNull(detail.DecidedAt);
        AssertAuditInvariant(detail);

        var furtherApprove = await ApproveAsync(Users.Bob, created.Id, detail.Version);
        await AssertProblemCodeAsync(furtherApprove, HttpStatusCode.Conflict, "INVALID_TRANSITION");
    }

    [Fact]
    public async Task Reject_without_reason_returns_400()
    {
        var created = await CreateAsync(Users.Alice, Apps.Crm, "NonProduction", "Read");

        var response = await Factory.ClientAs(Users.Bob)
            .PostAsJsonAsync($"/api/access-requests/{created.Id}/reject",
                new { expectedVersion = created.Version, reason = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task User_without_manager_gets_422()
    {
        var response = await Factory.ClientAs(Users.Bob)
            .PostAsJsonAsync("/api/access-requests", CreateBody(Apps.Crm, "NonProduction", "Read"));

        await AssertProblemCodeAsync(response, HttpStatusCode.UnprocessableEntity, "NO_MANAGER");
    }

    [Fact]
    public async Task System_owner_requesting_own_high_risk_app_gets_422()
    {
        // Seed tidak punya user yang punya manager sekaligus System Owner, jadi dibuat khusus di test ini.
        var ownerId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var email = $"owner-{ownerId:N}@example.local";
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User { Id = ownerId, Email = email, DisplayName = "Owner", ManagerId = SeedData.BobId });
            db.Applications.Add(new Application
            {
                Id = appId, Code = $"APP_{ownerId:N}"[..20], Name = "Owned App", SystemOwnerId = ownerId
            });
            await db.SaveChangesAsync();
        }

        var highRisk = await Factory.ClientAs(email)
            .PostAsJsonAsync("/api/access-requests", CreateBody(appId, "Production", "Read"));
        await AssertProblemCodeAsync(highRisk, HttpStatusCode.UnprocessableEntity, "NO_ELIGIBLE_APPROVER");

        var lowRisk = await Factory.ClientAs(email)
            .PostAsJsonAsync("/api/access-requests", CreateBody(appId, "NonProduction", "Read"));
        Assert.Equal(HttpStatusCode.Created, lowRisk.StatusCode);
    }

    [Fact]
    public async Task Unknown_application_gets_422()
    {
        var response = await Factory.ClientAs(Users.Alice)
            .PostAsJsonAsync("/api/access-requests", CreateBody(Guid.NewGuid(), "NonProduction", "Read"));
        await AssertProblemCodeAsync(response, HttpStatusCode.UnprocessableEntity, "UNKNOWN_APPLICATION");
    }

    [Fact]
    public async Task Integer_enum_value_returns_400()
    {
        var payload = new
        {
            clientRequestId = Guid.NewGuid().ToString(),
            applicationId = Apps.Crm,
            environment = 7,
            accessLevel = "Read",
            justification = "Test enum invalid"
        };

        var response = await Factory.ClientAs(Users.Alice).PostAsJsonAsync("/api/access-requests", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Blank_justification_returns_400()
    {
        var response = await Factory.ClientAs(Users.Alice)
            .PostAsJsonAsync("/api/access-requests", CreateBody(Apps.Crm, "NonProduction", "Read", justification: "   "));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bob_inbox_contains_only_pending_direct_reports()
    {
        var created = await CreateAsync(Users.Alice, Apps.Crm, "NonProduction", "Read");

        var inboxResponse = await Factory.ClientAs(Users.Bob).GetAsync("/api/approvals/inbox");
        Assert.Equal(HttpStatusCode.OK, inboxResponse.StatusCode);
        var inbox = await DataAsync<List<SummaryDto>>(inboxResponse);

        Assert.Contains(inbox!, r => r.Id == created.Id && r.Status == "PendingManagerApproval");
    }

    [Fact]
    public async Task Error_response_contains_correlation_id()
    {
        var created = await CreateAsync(Users.Alice, Apps.Crm, "NonProduction", "Read");

        var client = Factory.ClientAs(Users.Dana);
        var response = await client.PostAsJsonAsync($"/api/access-requests/{created.Id}/approve",
            new { expectedVersion = created.Version });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-Id"));

        var problem = await ProblemAsync(response);
        Assert.True(problem.TryGetProperty("correlationId", out _));
    }
}
