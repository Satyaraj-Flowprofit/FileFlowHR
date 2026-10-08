using FileFlowHR.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FileFlowHR.Application.Features.People;

public interface IPersonNumberGenerator
{
    Task<string> NextAsync(CancellationToken ct);
}

/// <summary>P0001, P0002, ... per company. The unique index catches the rare clash between two saves.</summary>
public sealed class PersonNumberGenerator(IAppDbContext db) : IPersonNumberGenerator
{
    public async Task<string> NextAsync(CancellationToken ct)
    {
        var numbers = await db.People.Where(p => p.PersonNumber.StartsWith("P")).Select(p => p.PersonNumber).ToListAsync(ct);
        var max = numbers.Select(n => int.TryParse(n.AsSpan(1), out var v) ? v : 0).DefaultIfEmpty(0).Max();
        return $"P{max + 1:0000}";
    }
}
