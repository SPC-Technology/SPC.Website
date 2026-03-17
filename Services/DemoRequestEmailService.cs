using Microsoft.Extensions.Configuration;
using SPC.Website.Models;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Net;

namespace SPC.Website.Services;

public class DemoRequestEmailService
{
    private readonly IConfiguration configuration;

    public DemoRequestEmailService(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public async Task SendDemoRequestAsync(DemoRequestModel model)
    {
        var apiKey = configuration["SendGrid:ApiKey"];
        var fromEmail = configuration["SendGrid:FromEmail"];
        var fromName = configuration["SendGrid:FromName"];
        var toEmail = configuration["SendGrid:ToEmail"];

        if (string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(fromEmail) ||
            string.IsNullOrWhiteSpace(toEmail))
        {
            throw new InvalidOperationException("SendGrid configuration is incomplete.");
        }

        var client = new SendGridClient(apiKey);
        var from = new EmailAddress(fromEmail, fromName);
        var to = new EmailAddress(toEmail);

        var message = MailHelper.CreateSingleEmail(
            from,
            to,
            $"Phoebus ERP Demo Request - {model.Company}",
            BuildTextBody(model),
            BuildHtmlBody(model));

        var response = await client.SendEmailAsync(message);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Body.ReadAsStringAsync();

            throw new InvalidOperationException(
                $"SendGrid error: {response.StatusCode} {body}");
        }
    }

    private static string BuildTextBody(DemoRequestModel model)
    {
        return
            "Phoebus ERP Demo Request" + Environment.NewLine + Environment.NewLine +
            $"Name: {model.FullName}" + Environment.NewLine +
            $"Company: {model.Company}" + Environment.NewLine +
            $"Email: {model.Email}" + Environment.NewLine +
            $"Phone: {model.Phone}" + Environment.NewLine +
            $"Message: {model.Message}";
    }

    private static string BuildHtmlBody(DemoRequestModel model)
    {
        return $@"
<h2>Phoebus ERP Demo Request</h2>

<p><strong>Name:</strong> {Encode(model.FullName)}</p>
<p><strong>Company:</strong> {Encode(model.Company)}</p>
<p><strong>Email:</strong> {Encode(model.Email)}</p>
<p><strong>Phone:</strong> {Encode(model.Phone)}</p>

<p><strong>Message:</strong></p>
<p>{Encode(model.Message)}</p>
";
    }

    private static string Encode(string? value)
    {
        return WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
