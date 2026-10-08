using FileFlowHR.Application.Abstractions;
using FileFlowHR.Application.Common;
using FileFlowHR.Application.Common.Messaging;
using FileFlowHR.Application.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace FileFlowHR.Application.Features.People;

/// <summary>Creates a login for a person and emails them an invitation link.</summary>
[RequiresPermission(Permissions.UsersManage)]
public sealed record InviteUserCommand(long PersonId) : ICommand<Result>;

public sealed class InviteUserHandler(IAppDbContext db, IUserAccountService accounts) : IRequestHandler<InviteUserCommand, Result>
{
    public async Task<Result> Handle(InviteUserCommand request, CancellationToken ct)
    {
        var person = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PersonId, ct);
        if (person is null) return Result.NotFound("Person");
        if (string.IsNullOrWhiteSpace(person.Email)) return Result.Invalid("Add an email address to this person first.");
        return await accounts.InviteAsync(person.Id, person.Email, ct);
    }
}

[RequiresPermission(Permissions.UsersManage)]
public sealed record ListUsersQuery : IQuery<Result<IReadOnlyList<UserRow>>>;

public sealed record UserRow(UserAccountDto Account, string? PersonName);

public sealed class ListUsersHandler(IAppDbContext db, IUserAccountService accounts) : IRequestHandler<ListUsersQuery, Result<IReadOnlyList<UserRow>>>
{
    public async Task<Result<IReadOnlyList<UserRow>>> Handle(ListUsersQuery request, CancellationToken ct)
    {
        var list = await accounts.ListAsync(ct);
        var names = await db.People.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.FirstName + " " + p.LastName, ct);
        return list.Select(a => new UserRow(a, a.PersonId is { } id ? names.GetValueOrDefault(id) : null)).ToList();
    }
}

[RequiresPermission(Permissions.UsersManage)]
public sealed record SetUserEnabledCommand(long UserAccountId, bool Enabled) : ICommand<Result>;

public sealed class SetUserEnabledHandler(IUserAccountService accounts, ICurrentUser user) : IRequestHandler<SetUserEnabledCommand, Result>
{
    public Task<Result> Handle(SetUserEnabledCommand request, CancellationToken ct) =>
        request.UserAccountId == user.UserAccountId && !request.Enabled
            ? Task.FromResult(Result.Invalid("You cannot disable your own login."))
            : accounts.SetEnabledAsync(request.UserAccountId, request.Enabled, ct);
}

[RequiresPermission(Permissions.UsersManage)]
public sealed record ResendInvitationCommand(long UserAccountId) : ICommand<Result>;

public sealed class ResendInvitationHandler(IUserAccountService accounts) : IRequestHandler<ResendInvitationCommand, Result>
{
    public Task<Result> Handle(ResendInvitationCommand request, CancellationToken ct) => accounts.ResendInvitationAsync(request.UserAccountId, ct);
}
