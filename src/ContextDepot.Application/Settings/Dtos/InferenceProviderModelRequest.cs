namespace ContextDepot.Application.Settings.Dtos;

public sealed record InferenceProviderModelRequest(string Model, int? Dimensions, int TimeoutSeconds);
