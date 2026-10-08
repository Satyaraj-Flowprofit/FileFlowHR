using FileFlowHR.Domain.Common;

namespace FileFlowHR.Domain.Recruitment;

public enum PostingVisibility { Internal, External, Both }
public enum PostingStatus { Draft, Open, Closed, Filled }

/// <summary>A job advert for an empty position.</summary>
public sealed class JobPosting : CompanyOwnedEntity
{
    private JobPosting() { }

    public long PositionId { get; private set; }
    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";
    public PostingVisibility Visibility { get; private set; }
    public DateOnly OpenDate { get; private set; }
    public DateOnly? CloseDate { get; private set; }
    public PostingStatus Status { get; private set; }

    public bool IsVisibleToStaff => Visibility is PostingVisibility.Internal or PostingVisibility.Both;
    public bool IsVisibleToPublic => Visibility is PostingVisibility.External or PostingVisibility.Both;

    public bool AcceptsApplications(DateOnly today) =>
        Status == PostingStatus.Open && OpenDate <= today && (CloseDate is null || CloseDate > today);

    public static JobPosting Draft(long positionId, string title, string description,
        PostingVisibility visibility, DateOnly openDate, DateOnly? closeDate)
    {
        var posting = new JobPosting { PositionId = positionId, Status = PostingStatus.Draft };
        posting.Update(title, description, visibility, openDate, closeDate);
        return posting;
    }

    public void Update(string title, string description, PostingVisibility visibility, DateOnly openDate, DateOnly? closeDate)
    {
        if (Status is PostingStatus.Closed or PostingStatus.Filled)
            throw new DomainException("A closed or filled posting cannot be changed.");
        if (closeDate is not null && closeDate <= openDate)
            throw new DomainException("The close date must be after the open date.");

        Title = Guard.Required(title, "Posting title", 150);
        Description = Guard.Required(description, "Description", 20000);
        Visibility = visibility;
        OpenDate = openDate;
        CloseDate = closeDate;
    }

    public void Publish()
    {
        if (Status != PostingStatus.Draft)
            throw new DomainException("Only a draft posting can be published.");
        Status = PostingStatus.Open;
    }

    public void Close(DateOnly today)
    {
        if (Status != PostingStatus.Open)
            throw new DomainException("Only an open posting can be closed.");
        Status = PostingStatus.Closed;
        CloseDate ??= today > OpenDate ? today : OpenDate.AddDays(1);
    }

    public void MarkFilled(DateOnly today)
    {
        if (Status is PostingStatus.Filled)
            throw new DomainException("This posting is already filled.");
        if (Status is PostingStatus.Open) Close(today);
        Status = PostingStatus.Filled;
    }
}
