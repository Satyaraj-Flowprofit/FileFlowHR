using FluentValidation;
using FileFlowHR.Application.Abstractions;
using FileFlowHR.Application.Common;
using FileFlowHR.Application.Common.Messaging;
using FileFlowHR.Application.Common.Security;
using FileFlowHR.Domain.Recruitment;
using Microsoft.EntityFrameworkCore;

namespace FileFlowHR.Application.Features.Recruitment;

public sealed record PostingRow(long Id, long PositionId, string PositionCode, string Title, string Visibility, string Status,
    DateOnly OpenDate, DateOnly? CloseDate, int Applications, int Active);

[RequiresPermission(Permissions.PostingsManage)]
public sealed record ListPostingsQuery : IQuery<Result<IReadOnlyList<PostingRow>>>;

public sealed class ListPostingsHandler(IAppDbContext db, IAccessScopeResolver scope) : IRequestHandler<ListPostingsQuery, Result<IReadOnlyList<PostingRow>>>
{
    public async Task<Result<IReadOnlyList<PostingRow>>> Handle(ListPostingsQuery request, CancellationToken ct)
    {
        var visible = await scope.PositionIdsAsync(Permissions.PostingsManage, ct);
        var postings = db.JobPostings.AsNoTracking();
        if (visible is not null) postings = postings.Where(p => visible.Contains(p.PositionId));

        var closed = new[] { ApplicationStatus.Hired, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn };
        var rows = await (from j in postings
                          join pos in db.Positions on j.PositionId equals pos.Id
                          orderby j.OpenDate descending
                          select new
                          {
                              j.Id, j.PositionId, pos.Code, j.Title, j.Visibility, j.Status, j.OpenDate, j.CloseDate,
                              Total = db.JobApplications.Count(a => a.JobPostingId == j.Id),
                              Active = db.JobApplications.Count(a => a.JobPostingId == j.Id && !closed.Contains(a.Status)),
                          }).ToListAsync(ct);
        return rows.Select(r => new PostingRow(r.Id, r.PositionId, r.Code, r.Title, r.Visibility.ToString(), r.Status.ToString(),
            r.OpenDate, r.CloseDate, r.Total, r.Active)).ToList();
    }
}

public sealed record PostingDto(long Id, long PositionId, string Title, string Description, PostingVisibility Visibility,
    DateOnly OpenDate, DateOnly? CloseDate, PostingStatus Status);

[RequiresPermission(Permissions.PostingsManage)]
public sealed record GetPostingQuery(long Id) : IQuery<Result<PostingDto>>;

public sealed class GetPostingHandler(IAppDbContext db, IAccessScopeResolver scope) : IRequestHandler<GetPostingQuery, Result<PostingDto>>
{
    public async Task<Result<PostingDto>> Handle(GetPostingQuery request, CancellationToken ct)
    {
        var j = await db.JobPostings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        var visible = await scope.PositionIdsAsync(Permissions.PostingsManage, ct);
        if (j is null || (visible is not null && !visible.Contains(j.PositionId))) return Result<PostingDto>.NotFound("Job posting");
        return new PostingDto(j.Id, j.PositionId, j.Title, j.Description, j.Visibility, j.OpenDate, j.CloseDate, j.Status);
    }
}

[RequiresPermission(Permissions.PostingsManage)]
public sealed record SavePostingCommand(long? Id, long PositionId, string Title, string Description, PostingVisibility Visibility,
    DateOnly OpenDate, DateOnly? CloseDate) : ICommand<Result<long>>;

public sealed class SavePostingValidator : AbstractValidator<SavePostingCommand>
{
    public SavePostingValidator()
    {
        RuleFor(x => x.PositionId).GreaterThan(0).WithMessage("Choose the position this job is for.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).NotEmpty();
    }
}

public sealed class SavePostingHandler(IAppDbContext db, IAccessScopeResolver scope) : IRequestHandler<SavePostingCommand, Result<long>>
{
    public async Task<Result<long>> Handle(SavePostingCommand request, CancellationToken ct)
    {
        var visible = await scope.PositionIdsAsync(Permissions.PostingsManage, ct);
        if (visible is not null && !visible.Contains(request.PositionId))
            return Result<long>.Fail(FailureKind.Forbidden, ["You can only post jobs for positions in your area."]);
        if (!await db.Positions.AnyAsync(p => p.Id == request.PositionId && p.EffectiveTo == null, ct))
            return Result<long>.NotFound("Open position");

        if (request.Id is { } id)
        {
            var existing = await db.JobPostings.FindAsync([id], ct);
            if (existing is null || existing.PositionId != request.PositionId) return Result<long>.NotFound("Job posting");
            existing.Update(request.Title, request.Description, request.Visibility, request.OpenDate, request.CloseDate);
            await db.SaveChangesAsync(ct);
            return existing.Id;
        }

        var posting = JobPosting.Draft(request.PositionId, request.Title, request.Description, request.Visibility, request.OpenDate, request.CloseDate);
        db.JobPostings.Add(posting);
        await db.SaveChangesAsync(ct);
        return posting.Id;
    }
}

public enum PostingAction { Publish, Close }

[RequiresPermission(Permissions.PostingsManage)]
public sealed record ChangePostingStatusCommand(long Id, PostingAction Action) : ICommand<Result>;

public sealed class ChangePostingStatusHandler(IAppDbContext db, IAccessScopeResolver scope, TimeProvider clock)
    : IRequestHandler<ChangePostingStatusCommand, Result>
{
    public async Task<Result> Handle(ChangePostingStatusCommand request, CancellationToken ct)
    {
        var posting = await db.JobPostings.FindAsync([request.Id], ct);
        var visible = await scope.PositionIdsAsync(Permissions.PostingsManage, ct);
        if (posting is null || (visible is not null && !visible.Contains(posting.PositionId))) return Result.NotFound("Job posting");

        if (request.Action == PostingAction.Publish) posting.Publish();
        else posting.Close(clock.Today());
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
