namespace ContextDepot.Application.Contexts.Enums;

/// <summary>Internal persistence policy; existing callers retain Upsert semantics.</summary>
public enum ContextWriteMode
{
    Upsert,
    CreateOnly
}
