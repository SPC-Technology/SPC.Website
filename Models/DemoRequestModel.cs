using System.ComponentModel.DataAnnotations;

namespace SPC.Website.Models;

public class DemoRequestModel
{
    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public string Company { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Message { get; set; }

    public string? Website { get; set; }
}
