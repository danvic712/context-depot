using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Infrastructure.Exceptions;

namespace ContextDepot.Infrastructure.AgentSessions;

/// <summary>Canonical wire form shared by immutable plans and evidence; never log plaintext.</summary>
public static class CapturePlanCodec
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 8 };
    public static string Serialize(ValidatedExtractionPlan plan)
    {
        if (plan.Version != ValidatedExtractionPlan.CurrentVersion || plan.Candidates is null || plan.Candidates.Count > 3 ||
            plan.Candidates.Where((x, i) => x is null || x.Index != i).Any() ||
            plan.Candidates.Any(x => string.IsNullOrEmpty(x.Key) || x.Key.Length > 200 ||
                x.Kind is not ("fact" or "preference" or "goal" or "decision") || x.Summary is not { Length: >= 8 and <= 2000 } ||
                x.Evidence is not { Count: >= 1 and <= 3 } ||
                x.Evidence.Any(e => e is null || e.Start < 0 || e.Length <= 0 || e.Text is null || e.Length != e.Text.Length) ||
                x.Evidence.Sum(e => e.Length) > 2000 ||
                x.Evidence.Zip(x.Evidence.Skip(1)).Any(e => (long)e.First.Start + e.First.Length > e.Second.Start)))
            throw new InvalidOperationException(InfrastructureErrorCodes.SessionMemoryContentUnavailable);
        var text = JsonSerializer.Serialize(plan, Json);
        if (text.Length > 32768) throw new InvalidOperationException(InfrastructureErrorCodes.SessionMemoryResponseInvalid);
        return text;
    }
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static ValidatedExtractionPlan Read(string plaintext, string hash)
    {
        if (plaintext.Length > 32768 || Hash(plaintext) != hash) throw new InvalidOperationException(InfrastructureErrorCodes.SessionMemoryContentUnavailable);
        var plan = JsonSerializer.Deserialize<ValidatedExtractionPlan>(plaintext, Json) ?? throw new InvalidOperationException(InfrastructureErrorCodes.SessionMemoryContentUnavailable);
        if (Serialize(plan) != plaintext) throw new InvalidOperationException(InfrastructureErrorCodes.SessionMemoryContentUnavailable);
        return plan;
    }
}
