using System.ComponentModel.DataAnnotations;
using SPC.Website.Models;
using SPC.Website.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();
builder.Host.UseSystemd();

var httpEndpointUrl = builder.Configuration["Kestrel:Endpoints:Http:Url"] ?? "http://0.0.0.0:7002";

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient("ServerAPI", client =>
{
    client.BaseAddress = new Uri(NormalizeBaseUrl(httpEndpointUrl));
});

builder.Services.AddScoped<DemoRequestEmailService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapPost("/api/demo-request", async (
    DemoRequestModel request,
    DemoRequestEmailService emailService) =>
{
    if (!string.IsNullOrWhiteSpace(request.Website))
    {
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

    await emailService.SendDemoRequestAsync(request);
    return Results.Ok(new { message = "Thank you. We will contact you shortly." });
});

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
