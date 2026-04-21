using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BlackoutRugbyDashboard.Models;

public class TeamDashboardRequest : IValidatableObject
{
    [Required]
    [Display(Name = "API endpoint")]
    public string BaseEndpoint { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    [Display(Name = "Team ID")]
    public int TeamId { get; set; }

    [Display(Name = "Member ID")]
    public int? MemberId { get; set; }

    [Display(Name = "Member key")]
    public string? MemberKey { get; set; }

    [Display(Name = "Season")]
    public int Season { get; set; } = 60;

    public bool HasCredentials => MemberId.HasValue && !string.IsNullOrWhiteSpace(MemberKey);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasMemberId = MemberId.HasValue;
        var hasMemberKey = !string.IsNullOrWhiteSpace(MemberKey);

        if (hasMemberId != hasMemberKey)
        {
            yield return new ValidationResult(
                "Member ID and member key must be supplied together if you want owner-level data.",
                new[] { nameof(MemberId), nameof(MemberKey) });
        }
    }
}
