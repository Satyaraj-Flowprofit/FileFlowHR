using FileFlowHR.Application.Common;
using FileFlowHR.Application.Features.People;
using FileFlowHR.Application.Features.Recruitment;
using FileFlowHR.Domain.People;
using FileFlowHR.Domain.Recruitment;

namespace FileFlowHR.Tests.Application;

public class PermissionAndHiringTests : IAsyncLifetime
{
    private TestApp _app = default!;
    public async Task InitializeAsync() => _app = await TestApp.CreateAsync();
    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task Employee_cannot_manage_users()
    {
        await _app.SignInAsync("acme", "ravi.kumar@gmail.com");
        var result = await _app.Send(new ListUsersQuery());
        Assert.Equal(FailureKind.Forbidden, result.Kind);
    }

    [Fact]
    public async Task Signed_out_visitor_cannot_list_people()
    {
        _app.SignOut();
        var result = await _app.Send(new ListPeopleQuery());
        Assert.Equal(FailureKind.Forbidden, result.Kind);
    }

    [Fact]
    public async Task Recruiter_sees_only_postings_in_her_business_unit()
    {
        await _app.SignInAsync("acme", "sara.kim@acme.com"); // recruiter for Engineering only
        var postings = await _app.Send(new ListPostingsQuery());
        Assert.Equal(["Software Engineer, Chennai"], postings.Value!.Select(p => p.Title));
    }

    [Fact]
    public async Task Hiring_an_applicant_creates_employment_and_fills_the_posting()
    {
        await _app.SignInAsync("acme", "priya.nair@acme.com");
        var sales = (await _app.Send(new ListPostingsQuery())).Value!.Single(p => p.Title.StartsWith("Sales Executive"));
        var tom = (await _app.Send(new ListApplicationsQuery(sales.Id))).Value!.Applications.Single(a => a.PersonName == "Tom Baker");

        foreach (var step in new[] { ApplicationStatus.Interview, ApplicationStatus.Offer })
            Assert.True((await _app.Send(new MoveApplicationCommand(tom.Id, step))).Succeeded);
        var hire = await _app.Send(new HireApplicantCommand(tom.Id, DateOnly.FromDateTime(DateTime.Today), EmploymentType.FullTime));
        Assert.True(hire.Succeeded, string.Join(" ", hire.Errors));

        var person = (await _app.Send(new GetPersonQuery(tom.PersonId))).Value!;
        Assert.True(person.IsEmployee);
        Assert.Contains(person.Assignments, a => a.PositionId == sales.PositionId && a.To is null);
        var posting = (await _app.Send(new ListApplicationsQuery(sales.Id))).Value!.Posting;
        Assert.Equal(PostingStatus.Filled, posting.Status);
    }

    [Fact]
    public async Task A_position_cannot_have_two_people_at_once()
    {
        await _app.SignInAsync("acme", "priya.nair@acme.com");
        var people = (await _app.Send(new ListPeopleQuery())).Value!;
        var maria = people.Single(p => p.Name == "Maria Lopez");
        var ravi = people.Single(p => p.Name == "Ravi Kumar");
        var mariaPosition = (await _app.Send(new GetPersonQuery(maria.Id))).Value!.Assignments.First(a => a.To is null).PositionId;

        var result = await _app.Send(new AssignPositionCommand(ravi.Id, mariaPosition, AssignmentType.Primary,
            DateOnly.FromDateTime(DateTime.Today), null));
        Assert.False(result.Succeeded);
    }
}
