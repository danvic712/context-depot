using ContextDepot.Application.Embeddings;
using Microsoft.Extensions.Options;

namespace ContextDepot.Application.Tests.Embeddings;

public sealed class EmbeddingOptionsTests
{
    [Fact]
    public void Invalid_embedding_options_fail_validation()
    {
        var result = new EmbeddingOptionsValidator().Validate(
            Options.DefaultName,
            new EmbeddingOptions
            {
                Provider = "",
                Dimensions = 0
            });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Profile_identity_contains_only_provider_model_and_dimensions()
    {
        var profile = EmbeddingProfile.From(new EmbeddingOptions
        {
            Provider = " configured ",
            Model = " model ",
            Dimensions = 3
        });

        Assert.Equal("configured\nmodel\n3", profile.Key.ToString());
        Assert.Equal(profile.Key, new EmbeddingProfileKey("configured", "model", 3));
    }
}
