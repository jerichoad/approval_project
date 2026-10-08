using System.Net;
using System.Net.Http.Json;
using backendApproval.Tests.Infrastructure;

namespace backendApproval.Tests;

public sealed class IdempotencyTests(ApiFactory factory) : ApiTestBase(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Same_client_request_id_returns_same_request()
    {
        var key = Guid.NewGuid().ToString();
        var body = CreateBody(Apps.Crm, "NonProduction", "Read", clientRequestId: key);

        var r1 = await Factory.ClientAs(Users.Alice).PostAsJsonAsync("/api/access-requests", body);
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        var d1 = await r1.Content.ReadFromJsonAsync<DetailDto>(Json);

        var r2 = await Factory.ClientAs(Users.Alice).PostAsJsonAsync("/api/access-requests", body);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        var d2 = await r2.Content.ReadFromJsonAsync<DetailDto>(Json);

        Assert.Equal(d1!.Id, d2!.Id);
    }

    [Fact]
    public async Task Reused_client_request_id_with_different_payload_returns_409()
    {
        var key = Guid.NewGuid().ToString();

        await Factory.ClientAs(Users.Alice).PostAsJsonAsync("/api/access-requests",
            CreateBody(Apps.Crm, "NonProduction", "Read", clientRequestId: key));

        var r2 = await Factory.ClientAs(Users.Alice).PostAsJsonAsync("/api/access-requests",
            CreateBody(Apps.Crm, "Production", "Read", clientRequestId: key));

        await AssertProblemCodeAsync(r2, HttpStatusCode.Conflict, "IDEMPOTENCY_KEY_REUSED");
    }

    [Fact]
    public async Task Concurrent_duplicate_submits_create_exactly_one_row()
    {
        var key = Guid.NewGuid().ToString();
        var body = CreateBody(Apps.Crm, "NonProduction", "Read", clientRequestId: key);

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Factory.ClientAs(Users.Alice).PostAsJsonAsync("/api/access-requests", body));

        var responses = await Task.WhenAll(tasks);

        var created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var ok = responses.Count(r => r.StatusCode == HttpStatusCode.OK);

        Assert.Equal(1, created);
        Assert.Equal(9, ok);

        var ids = new HashSet<Guid>();
        foreach (var r in responses)
        {
            var dto = await r.Content.ReadFromJsonAsync<DetailDto>(Json);
            ids.Add(dto!.Id);
        }
        Assert.Single(ids);
    }
}
