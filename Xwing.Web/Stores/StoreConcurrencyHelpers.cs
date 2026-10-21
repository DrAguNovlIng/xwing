using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xwing.Web.Data;
using Xwing.Web.Models;

namespace Xwing.Web.Stores;

public sealed class StoreConcurrencyHelpers
{
    private readonly ILogger<StoreConcurrencyHelpers> logger;

    public StoreConcurrencyHelpers(ILogger<StoreConcurrencyHelpers> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        this.logger = logger;
    }

    public async Task<Result> SaveChangesAsync(XwingDbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Ok();
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(exception, "Concurrency conflict while saving changes.");
            return Result.Fail("The record was changed by someone else. Refresh and try again.");
        }
    }
}
