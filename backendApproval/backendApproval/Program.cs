using System.Text.Json.Serialization;
using backendApproval.Auth;
using backendApproval.Data;
using backendApproval.Errors;
using backendApproval.Observability;
using backendApproval.Policies;
using backendApproval.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);

builder.Services.AddControllers(o =>
        o.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()))
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(allowIntegerValues: false)));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = c =>
{
    if (c.HttpContext.Items[CorrelationIdMiddleware.Header] is string id)
        c.ProblemDetails.Extensions["correlationId"] = id;
});
builder.Services.AddExceptionHandler<AppExceptionHandler>();

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
     .UseSnakeCaseNamingConvention());

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CorrelationContext>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddSingleton<IApprovalPolicy, ApprovalPolicyV1>();
builder.Services.AddSingleton<ApprovalPolicyRegistry>();
builder.Services.AddScoped<AccessRequestService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Urutan penting:
// 1) Correlation paling luar  -> log & ProblemDetails dari exception handler tetap punya CorrelationId
// 2) Exception handler        -> mengubah AppException menjadi ProblemDetails 4xx, sisanya 500
// 3) Current user             -> 401 untuk header yang tidak dikenal
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseMiddleware<CurrentUserMiddleware>();
app.MapControllers();

app.Run();

public partial class Program { }
