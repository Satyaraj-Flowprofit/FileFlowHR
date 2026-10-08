namespace FileFlowHR.Domain.Common;

/// <summary>Base class for everything stored with a numeric primary key.</summary>
public abstract class Entity
{
    public long Id { get; protected set; }
}

/// <summary>
/// Data that belongs to one client company. The CompanyId is stamped by the persistence layer
/// from the signed-in user's company and every query is filtered by it.
/// </summary>
public interface ICompanyOwned
{
    long CompanyId { get; }
}

public abstract class CompanyOwnedEntity : Entity, ICompanyOwned
{
    public long CompanyId { get; private set; }
}
