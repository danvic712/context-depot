using ContextDepot.Domain.Entities;
using ContextDepot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.CurrentOwner;

public sealed class CurrentOwnerBootstrapper(ContextDepotDbContext db, IOptions<CurrentOwnerOptions> options)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var configured = options.Value;
        var owners = await db.Owners.ToListAsync(cancellationToken);
        var unexpected = owners.Where(owner => owner.Id != configured.Id).ToArray();
        if (unexpected.Length > 0)
        {
            throw new InvalidOperationException("OwnerConfigurationMismatch");
        }

        var current = owners.SingleOrDefault(owner => owner.Id == configured.Id);
        if (current is null)
        {
            db.Owners.Add(new Owner(configured.Id, configured.DisplayName.Trim(), DateTimeOffset.UtcNow));
        }
        else if (!string.Equals(current.DisplayName, configured.DisplayName.Trim(), StringComparison.Ordinal))
        {
            current.UpdateDisplayName(configured.DisplayName.Trim(), DateTimeOffset.UtcNow);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
