using System.Text.Json.Serialization;
using backendApproval.Auth;
using backendApproval.Contracts;
using backendApproval.Data;
using backendApproval.Errors;
using backendApproval.Observability;
using backendApproval.Policies;
using backendApproval.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);

builder.Services.AddControllers(o =>
        o.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()))
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var correlationId = context.HttpContext.Items[CorrelationIdMiddleware.Header] as string;
            var fieldErrors = context.ModelState
                .Where(kvp => kvp.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage)
                        ? "Nilai tidak valid."
                        : e.ErrorMessage).ToArray());

            var firstMessage = fieldErrors.Values.SelectMany(v => v).FirstOrDefault()
                ?? "Validasi request gagal.";

            var body = ApiErrorResponse.Error(
                code: "VALIDATION_ERROR",
                message: firstMessage,
                correlationId: correlationId,
                fieldErrors: fieldErrors);

            return new ObjectResult(body)
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        };
    })
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(allowIntegerValues: false)));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddProblemDetails();
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
// 1) Correlation paling luar  -> correlationId tetap ada di response error (termasuk dari exception handler)
// 2) Exception handler        -> fallback terakhir; mengubah exception yang lolos dari ExecuteAsync() controller
//                                menjadi envelope { Status: "E", Data: {...} }
// 3) Current user             -> 401 { Status: "E", Data: { code: "UNAUTHENTICATED" } } untuk header yang tidak dikenal
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseMiddleware<CurrentUserMiddleware>();
app.MapControllers();

app.Run();

public partial class Program { }
