using FileFlowHR.Application.Abstractions;
using FileFlowHR.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FileFlowHR.Infrastructure.Persistence;

/// <summary>
/// Last line of defence for company isolation and history:
/// new rows get the current CompanyId, rows of another company can never be written,
/// HR rows are never deleted, and every change records who made it.
/// </summary>
public sealed class CompanyStampingInterceptor(ICompanyContext companyContext, ICurrentUser currentUser) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context) Apply(context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context) Apply(context);
        return base.SavingChanges(eventData, result);
    }

    private void Apply(DbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries<ICompanyOwned>())
        {
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException("HR records are never deleted. End them with a date instead.");
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;

            var companyId = companyContext.CompanyId
                ?? throw new InvalidOperationException("No company is known for this change.");
            var property = entry.Property(e => e.CompanyId);

            if (entry.State == EntityState.Added && property.CurrentValue == 0)
                property.CurrentValue = companyId;
            if (property.CurrentValue != companyId)
                throw new InvalidOperationException("A change to another company's data was blocked.");

            entry.Property(AuditColumns.UpdatedBy).CurrentValue = currentUser.PersonId;
        }
    }
}
