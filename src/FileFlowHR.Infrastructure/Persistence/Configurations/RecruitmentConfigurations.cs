using FileFlowHR.Domain.Organisation;
using FileFlowHR.Domain.People;
using FileFlowHR.Domain.Recruitment;
using FileFlowHR.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileFlowHR.Infrastructure.Persistence.Configurations;

internal sealed class JobPostingConfiguration : IEntityTypeConfiguration<JobPosting>
{
    public void Configure(EntityTypeBuilder<JobPosting> b)
    {
        b.ToTable("JobPosting");
        b.Property(x => x.Id).HasColumnName("JobPostingId");
        b.Property(x => x.Title).HasColumnName("PostingTitle").HasMaxLength(150);
        b.Property(x => x.Description);
        b.HasOne<Position>().WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.IsVisibleToPublic);
        b.Ignore(x => x.IsVisibleToStaff);
    }
}

internal sealed class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> b)
    {
        b.ToTable("JobApplication");
        b.Property(x => x.Id).HasColumnName("JobApplicationId");
        b.Property(x => x.ResumePath).HasMaxLength(500).IsUnicode(false);
        b.HasOne<JobPosting>().WithMany().HasForeignKey(x => x.JobPostingId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.JobPostingId, x.PersonId }).IsUnique();
        b.Ignore(x => x.IsClosed);
    }
}

internal sealed class RoleGrantConfiguration : IEntityTypeConfiguration<RoleGrant>
{
    public void Configure(EntityTypeBuilder<RoleGrant> b)
    {
        b.ToTable("RoleGrant", t => t.HasCheckConstraint("CK_RoleGrantNotSelf", "[GrantedBy] IS NULL OR [GrantedBy] <> [PersonId]"));
        b.Property(x => x.Id).HasColumnName("RoleGrantId");
        b.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Person>().WithMany().HasForeignKey(x => x.GrantedBy).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.IsOpen);
    }
}
