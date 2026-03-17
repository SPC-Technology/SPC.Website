using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.Features;
using SPC.Website.Models;
using SPC.Website.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();
builder.Host.UseSystemd();
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

var httpEndpointUrl = builder.Configuration["Kestrel:Endpoints:Http:Url"] ?? "http://0.0.0.0:7002";
var downloadsOptions = builder.Configuration.GetSection("Downloads").Get<DownloadsOptions>() ?? new DownloadsOptions();

builder.Services.Configure<DownloadsOptions>(builder.Configuration.GetSection("Downloads"));
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = downloadsOptions.UploadMaxRequestBodyBytes > 0
        ? downloadsOptions.UploadMaxRequestBodyBytes
        : 1073741824;
});
builder.Services.AddControllers();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient("ServerAPI", client =>
{
    client.BaseAddress = new Uri(NormalizeBaseUrl(httpEndpointUrl));
});

builder.Services.AddScoped<DemoRequestEmailService>();
builder.Services.AddSingleton<DownloadBrowserService>();

var app = builder.Build();

app.UseSerilogRequestLogging();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapPost("/api/demo-request", async (
    DemoRequestModel request,
    DemoRequestEmailService emailService,
    ILogger<Program> logger) =>
{
    if (!string.IsNullOrWhiteSpace(request.Website))
    {
        logger.LogInformation("Demo request ignored by honeypot for company {Company}.", request.Company);
        return Results.Ok();
    }

    var validationResults = new List<ValidationResult>();
    var validationContext = new ValidationContext(request);

    var isValid = Validator.TryValidateObject(
        request,
        validationContext,
        validationResults,
        validateAllProperties: true);

    if (!isValid)
    {
        logger.LogWarning("Demo request validation failed for company {Company} and email {Email}.", request.Company, request.Email);
        var errors = validationResults
            .SelectMany(result => result.MemberNames.Select(name => new { name, result.ErrorMessage }))
            .GroupBy(item => item.name)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(item => item.ErrorMessage ?? "Invalid value.")
                    .ToArray());

        return Results.ValidationProblem(errors);
    }

    try
    {
        logger.LogInformation("Submitting demo request for company {Company} and email {Email}.", request.Company, request.Email);
        await emailService.SendDemoRequestAsync(request);
        logger.LogInformation("Demo request email sent successfully for company {Company}.", request.Company);
        return Results.Ok(new { message = "Thank you. We will contact you shortly." });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Demo request email failed for company {Company} and email {Email}.", request.Company, request.Email);
        return Results.Problem(
            title: "Unable to process request",
            detail: "Unable to submit your request at the moment. Please try again later.",
            statusCode: 500);
    }
});

app.MapControllers();

app.MapRazorComponents<SPC.Website.App>()
    .AddInteractiveServerRenderMode();

app.Run();

static string NormalizeBaseUrl(string url)
{
    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
    {
        return "http://localhost:7002/";
    }

    if (uri.Host is "0.0.0.0" or "*" or "+")
    {
        var builder = new UriBuilder(uri)
        {
            Host = "localhost"
        };

        return builder.Uri.ToString();
    }

    return uri.ToString();
}
