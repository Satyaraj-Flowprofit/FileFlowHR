using FileFlowHR.Domain.Common;
using FileFlowHR.Domain.Organisation;
using FileFlowHR.Domain.People;
using FileFlowHR.Domain.Recruitment;
using FileFlowHR.Domain.Security;

namespace FileFlowHR.Tests.Domain;

public class DomainRuleTests
{
    private static readonly DateOnly Today = new(2026, 10, 8);

    [Fact]
    public void Interim_cover_needs_an_end_date()
    {
        var ex = Assert.Throws<DomainException>(() => PositionAssignment.Create(1, 2, AssignmentType.Interim, Today, null));
        Assert.Contains("end date", ex.Message);
    }

    [Fact]
    public void Org_tree_refuses_a_reporting_loop()
    {
        // 2 reports to 1, 3 reports to 2. Making 1 report to 3 would be a loop.
        (long PositionId, long ManagerPositionId)[] lines = [(2, 1), (3, 2)];
        Assert.True(OrgTree.WouldCreateCycle(1, 3, lines));
        Assert.False(OrgTree.WouldCreateCycle(3, 1, lines));
        Assert.Equal([2L, 3L], OrgTree.Descendants([1], lines).Order());
    }

    [Fact]
    public void Nobody_can_give_themselves_a_role()
    {
        Assert.Throws<DomainException>(() =>
            RoleGrant.Create(5, GrantableRole.HrAdmin, ScopeType.Company, null, Today, null, grantedBy: 5));
    }

    [Fact]
    public void Application_follows_the_hiring_steps()
    {
        var posting = JobPosting.Draft(1, "Quality Analyst", "Checks", PostingVisibility.Both, Today, null);
        posting.Publish();
        var application = JobApplication.Submit(posting, 7, ApplicationSource.External, null, Today.ToDateTime(TimeOnly.MinValue));

        Assert.Throws<DomainException>(() => application.MoveTo(ApplicationStatus.Hired)); // cannot skip to hired
        application.MoveTo(ApplicationStatus.Screening);
        application.MoveTo(ApplicationStatus.Rejected);
        Assert.True(application.IsClosed);
    }

    [Fact]
    public void Staff_only_job_refuses_public_applicants()
    {
        var posting = JobPosting.Draft(1, "Team Lead", "Internal move", PostingVisibility.Internal, Today, null);
        posting.Publish();
        Assert.Throws<DomainException>(() =>
            JobApplication.Submit(posting, 7, ApplicationSource.External, null, Today.ToDateTime(TimeOnly.MinValue)));
    }
}
