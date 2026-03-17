using System.ComponentModel.DataAnnotations;
using SPC.Website.Models;
using SPC.Website.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient("ServerAPI", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["App:BaseUrl"] ?? "https://localhost:5001/");
});

builder.Services.AddScoped<DemoRequestEmailService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
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
