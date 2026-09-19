using System.ComponentModel.DataAnnotations;

namespace ContextDepot.Infrastructure.Options;

public sealed class CurrentOwnerOptions
{
    [Required]
    public Guid Id { get; set; }

    [Required, MinLength(1), MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;
}
