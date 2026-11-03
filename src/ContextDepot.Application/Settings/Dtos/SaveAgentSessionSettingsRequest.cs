using System.ComponentModel.DataAnnotations;

namespace ContextDepot.Application.Settings.Dtos;

public sealed record SaveAgentSessionSettingsRequest([Required] bool? Enabled);
