using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using backendApproval.Data;
using backendApproval.Domain;
using backendApproval.Domain.Entities;
using backendApproval.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace backendApproval.Tests;

public sealed class DatabaseGuardTests(ApiFactory factory) : ApiTestBase(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Updating_audit_event_is_rejected_by_database()
    {
        var created = await CreateAsync(Users.Alice, Apps.Crm, "NonProduction", "Read");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var audit = await db.AuditEvents.FirstAsync(e => e.AccessRequestId == created.Id);
        audit.Reason = "tampered";

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.IsType<PostgresException>(ex.InnerException);
    }

    [Fact]
    public async Task Second_save_with_same_original_version_throws_concurrency()
    {
        var created = await CreateAsync(Users.Alice, Apps.Crm, "NonProduction", "Read");

        using var scope1 = Factory.Services.CreateScope();
        using var scope2 = Factory.Services.CreateScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<AppDbContext>();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();

        var req1 = await db1.AccessRequests.SingleAsync(r => r.Id == created.Id);
        var req2 = await db2.AccessRequests.SingleAsync(r => r.Id == created.Id);

        req1.Status = AccessRequestStatus.Approved;
        req1.DecidedAt = DateTimeOffset.UtcNow;
        req1.UpdatedAt = DateTimeOffset.UtcNow;
        req1.Version = 2;

        req2.Status = AccessRequestStatus.Approved;
        req2.DecidedAt = DateTimeOffset.UtcNow;
        req2.UpdatedAt = DateTimeOffset.UtcNow;
        req2.Version = 2;

        await db1.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync());
    }
}
