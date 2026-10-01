namespace ContextDepot.Infrastructure.Exceptions;

// Diagnostic codes stay language-neutral; unexpected failures are localized at the host boundary.
public static class InfrastructureErrorCodes
{
    public const string AppearanceSettingMissing = nameof(AppearanceSettingMissing);
    public const string AppearanceSettingsInvalid = nameof(AppearanceSettingsInvalid);
    public const string ApplicationSettingJsonInvalid = nameof(ApplicationSettingJsonInvalid);
    public const string ApplicationSettingKeyDuplicated = nameof(ApplicationSettingKeyDuplicated);
    public const string ApplicationSettingKeyUnsupported = nameof(ApplicationSettingKeyUnsupported);
    public const string ApplicationSettingValueInvalid = nameof(ApplicationSettingValueInvalid);
    public const string ApplicationSettingsBuilderRequired = nameof(ApplicationSettingsBuilderRequired);
    public const string ApplicationSettingsMissing = nameof(ApplicationSettingsMissing);
    public const string ApplicationSettingsProviderNotInitialized = nameof(ApplicationSettingsProviderNotInitialized);
    public const string DepotAccessContextAlreadyInitialized = nameof(DepotAccessContextAlreadyInitialized);
    public const string DynamicVectorRecordsUnsupported = nameof(DynamicVectorRecordsUnsupported);
    public const string EmbeddingProfileFingerprintInvalid = nameof(EmbeddingProfileFingerprintInvalid);
    public const string EmbeddingRouteCountInvalid = nameof(EmbeddingRouteCountInvalid);
    public const string EmbeddingRouteRequired = nameof(EmbeddingRouteRequired);
    public const string IndexRepairSettingsInvalid = nameof(IndexRepairSettingsInvalid);
    public const string InferenceProviderUrlInvalid = nameof(InferenceProviderUrlInvalid);
    public const string InferenceRuntimeSnapshotNotLoaded = nameof(InferenceRuntimeSnapshotNotLoaded);
    public const string OrderedVectorRetrievalUnsupported = nameof(OrderedVectorRetrievalUnsupported);
    public const string PostgreSqlIdentifierInvalid = nameof(PostgreSqlIdentifierInvalid);
    public const string QueryVectorDimensionsInvalid = nameof(QueryVectorDimensionsInvalid);
    public const string QueryVectorTypeUnsupported = nameof(QueryVectorTypeUnsupported);
    public const string RetrievalSettingsInvalid = nameof(RetrievalSettingsInvalid);
    public const string VectorCollectionPropertiesRequired = nameof(VectorCollectionPropertiesRequired);
    public const string VectorCoverageSettingsInvalid = nameof(VectorCoverageSettingsInvalid);
    public const string VectorFilterPropertyNotFilterable = nameof(VectorFilterPropertyNotFilterable);
    public const string VectorFilterPropertyTypeUnsupported = nameof(VectorFilterPropertyTypeUnsupported);
    public const string VectorFilterPropertyUnknown = nameof(VectorFilterPropertyUnknown);
    public const string VectorKeyTypeUnsupported = nameof(VectorKeyTypeUnsupported);
    public const string VectorPropertyTypeUnsupported = nameof(VectorPropertyTypeUnsupported);
    public const string VectorRecordDimensionsInvalid = nameof(VectorRecordDimensionsInvalid);
    public const string VectorRecordPropertyMissing = nameof(VectorRecordPropertyMissing);
    public const string VectorRecordPropertyUnsupported = nameof(VectorRecordPropertyUnsupported);
    public const string WebDepotRequired = nameof(WebDepotRequired);
    public const string VectorStoreSchemaRequired = nameof(VectorStoreSchemaRequired);
}
