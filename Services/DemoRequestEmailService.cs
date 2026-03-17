using SendGrid;
using SendGrid.Helpers.Mail;

using SPC.Website.Models;

using System.Net;

namespace SPC.Website.Services;

public class DemoRequestEmailService
{
    private readonly IConfiguration configuration;
    private readonly ILogger<DemoRequestEmailService> logger;

    public DemoRequestEmailService(IConfiguration configuration, ILogger<DemoRequestEmailService> logger)
    {
        this.configuration = configuration;
        this.logger = logger;
    }

    public async Task SendDemoRequestAsync(DemoRequestModel model)
    {
        var apiKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = configuration["SendGrid:ApiKey"];
        }

        var fromEmail = configuration["SendGrid:FromEmail"];
        var fromName = configuration["SendGrid:FromName"];
        var toEmail = configuration["SendGrid:ToEmail"];
        var recipients = ParseRecipients(toEmail);

        if (string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(fromEmail) ||
            recipients.Count == 0)
        {
            logger.LogError(
                "SendGrid configuration is incomplete. FromEmail configured: {HasFromEmail}. Recipient count: {RecipientCount}. ApiKey configured: {HasApiKey}.",
                !string.IsNullOrWhiteSpace(fromEmail),
                recipients.Count,
                !string.IsNullOrWhiteSpace(apiKey));
            throw new InvalidOperationException("SendGrid configuration is incomplete.");
        }

        logger.LogInformation(
            "Sending demo request email for company {Company} to {RecipientCount} recipients.",
            model.Company,
            recipients.Count);

        var client = new SendGridClient(apiKey);
        var from = new EmailAddress(fromEmail, fromName);

        var message = MailHelper.CreateSingleEmailToMultipleRecipients(
            from,
            recipients,
            $"☎️ Phoebus ERP Demo Request - {model.Company}",
            BuildTextBody(model),
            BuildHtmlBody(model));

        var response = await client.SendEmailAsync(message);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Body.ReadAsStringAsync();

            logger.LogError(
                "SendGrid failed for company {Company}. StatusCode: {StatusCode}. ResponseBody: {ResponseBody}",
                model.Company,
                response.StatusCode,
                body);

            throw new InvalidOperationException(
                $"SendGrid error: {response.StatusCode} {body}");
        }

        logger.LogInformation("SendGrid accepted demo request email for company {Company}.", model.Company);
    }

    private static string BuildTextBody(DemoRequestModel model)
    {
        return
            "🟢 Phoebus ERP Demo Request" + Environment.NewLine + Environment.NewLine +
            $"Name: {model.FullName}" + Environment.NewLine +
            $"🌐 Company: {model.Company}" + Environment.NewLine +
            $"✉︎ Email: {model.Email}" + Environment.NewLine +
            $"📞 Phone: {model.Phone}" + Environment.NewLine +
            $"📢 Message: {model.Message}";
    }

    private static string BuildHtmlBody(DemoRequestModel model)
    {
        return $@"
<h2>🟢 Phoebus ERP Demo Request</h2>

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

    private static List<EmailAddress> ParseRecipients(string? toEmail)
    {
        return (toEmail ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => new EmailAddress(email))
            .ToList();
    }
}
