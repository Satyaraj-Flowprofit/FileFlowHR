using FileFlowHR.Application.Common;
using FileFlowHR.Application.Features.People;
using FileFlowHR.Application.Features.Recruitment;
using FileFlowHR.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace FileFlowHR.Tests.Application;

/// <summary>One database, many companies: nobody may see or change another company's rows.</summary>
public class CompanyIsolationTests : IAsyncLifetime
{
    private TestApp _app = default!;
    public async Task InitializeAsync() => _app = await TestApp.CreateAsync();
    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task Globex_admin_sees_only_Globex_people()
    {
        await _app.SignInAsync("globex", "jane.doe@globex.com");
        var people = await _app.Send(new ListPeopleQuery());

        Assert.True(people.Succeeded);
        Assert.Equal(["Jane Doe", "Meena Iyer"], people.Value!.Select(p => p.Name).Order());
    }

    [Fact]
    public async Task Globex_admin_cannot_open_or_edit_an_Acme_person()
    {
        await _app.SignInAsync("acme", "priya.nair@acme.com");
        var acmePersonId = (await _app.Send(new ListPeopleQuery("Maria"))).Value!.Single().Id;

        await _app.SignInAsync("globex", "jane.doe@globex.com");
        Assert.Equal(FailureKind.NotFound, (await _app.Send(new GetPersonQuery(acmePersonId))).Kind);
        var edit = await _app.Send(new SavePersonCommand(acmePersonId, "Hacked", "Name", null, null));
        Assert.False(edit.Succeeded);

        await _app.SignInAsync("acme", "priya.nair@acme.com");
        Assert.Equal("Maria", (await _app.Send(new GetPersonQuery(acmePersonId))).Value!.FirstName);
    }

    [Fact]
    public async Task Internal_job_board_shows_only_own_company_jobs()
    {
        await _app.SignInAsync("globex", "jane.doe@globex.com");
        var jobs = await _app.Send(new ListInternalJobsQuery());

        Assert.True(jobs.Succeeded);
        Assert.DoesNotContain(jobs.Value!, j => j.Title.Contains("Quality Analyst"));
    }

    [Fact]
    public async Task New_rows_are_stamped_with_the_signed_in_company()
    {
        await _app.SignInAsync("globex", "jane.doe@globex.com");
        var id = (await _app.Send(new SavePersonCommand(null, "Asha", "Rao", "asha.rao@globex.com", null))).Value;

        var companyCode = await _app.QueryAsync(db => db.People.Where(p => p.Id == id)
            .Join(db.Companies, p => p.CompanyId, c => c.Id, (p, c) => c.Code).SingleAsync());
        Assert.Equal("globex", companyCode);
    }

    [Fact]
    public async Task Rows_are_never_deleted()
    {
        await _app.SignInAsync("acme", "priya.nair@acme.com");
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => _app.QueryAsync<int>(async db =>
        {
            db.People.Remove(await db.People.FirstAsync());
            return await db.SaveChangesAsync();
        }));
        Assert.Contains("delete", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
