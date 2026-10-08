using FileFlowHR.Domain.Common;

namespace FileFlowHR.Domain.Recruitment;

public enum ApplicationSource { Internal, External }
public enum ApplicationStatus { Submitted, Screening, Interview, Offer, Hired, Rejected, Withdrawn }

/// <summary>One person applying to one posting. Internal and external applicants share this table.</summary>
public sealed class JobApplication : CompanyOwnedEntity
{
    private static readonly Dictionary<ApplicationStatus, ApplicationStatus[]> AllowedMoves = new()
    {
        [ApplicationStatus.Submitted] = [ApplicationStatus.Screening, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn],
        [ApplicationStatus.Screening] = [ApplicationStatus.Interview, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn],
        [ApplicationStatus.Interview] = [ApplicationStatus.Offer, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn],
        [ApplicationStatus.Offer] = [ApplicationStatus.Hired, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn],
        [ApplicationStatus.Hired] = [],
        [ApplicationStatus.Rejected] = [],
        [ApplicationStatus.Withdrawn] = [],
    };

    private JobApplication() { }

    public long JobPostingId { get; private set; }
    public long PersonId { get; private set; }
    public ApplicationSource Source { get; private set; }
    public string? ResumePath { get; private set; }
    public DateTime AppliedAt { get; private set; }
    public ApplicationStatus Status { get; private set; }

    public bool IsClosed => AllowedMoves[Status].Length == 0;

    public static IReadOnlyList<ApplicationStatus> NextSteps(ApplicationStatus status) => AllowedMoves[status];

    public static JobApplication Submit(JobPosting posting, long personId, ApplicationSource source,
        string? resumePath, DateTime appliedAtUtc)
    {
        if (!posting.AcceptsApplications(DateOnly.FromDateTime(appliedAtUtc)))
            throw new DomainException("This job is not accepting applications.");
        if (source == ApplicationSource.Internal && !posting.IsVisibleToStaff)
            throw new DomainException("This job is not open to internal applicants.");
        if (source == ApplicationSource.External && !posting.IsVisibleToPublic)
            throw new DomainException("This job is not open to external applicants.");

        return new JobApplication
        {
            JobPostingId = posting.Id,
            PersonId = personId,
            Source = source,
            ResumePath = resumePath,
            AppliedAt = appliedAtUtc,
            Status = ApplicationStatus.Submitted,
        };
    }

    public void MoveTo(ApplicationStatus next)
    {
        if (!AllowedMoves[Status].Contains(next))
            throw new DomainException($"An application cannot move from {Status} to {next}.");
        Status = next;
    }
}
