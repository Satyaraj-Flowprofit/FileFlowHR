using FileFlowHR.Domain.Organisation;
using FileFlowHR.Domain.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileFlowHR.Infrastructure.Persistence.Configurations;

internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> b)
    {
        b.ToTable("Person");
        b.Property(x => x.Id).HasColumnName("PersonId");
        b.Property(x => x.PersonNumber).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.FirstName).HasMaxLength(100);
        b.Property(x => x.LastName).HasMaxLength(100);
        b.Property(x => x.Email).HasMaxLength(320);
        b.Property(x => x.Phone).HasMaxLength(30).IsUnicode(false);
        b.HasIndex(x => new { x.CompanyId, x.PersonNumber }).IsUnique();
        b.HasIndex(x => new { x.CompanyId, x.Email });
        b.Ignore(x => x.FullName);
    }
}

internal sealed class EmploymentConfiguration : IEntityTypeConfiguration<Employment>
{
    public void Configure(EntityTypeBuilder<Employment> b)
    {
        b.ToTable("Employment");
        b.Property(x => x.Id).HasColumnName("EmploymentId");
        b.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.PersonId).IsUnique().HasDatabaseName("UX_EmploymentOpen").HasFilter("[TerminationDate] IS NULL");
    }
}

internal sealed class PositionAssignmentConfiguration : IEntityTypeConfiguration<PositionAssignment>
{
    public void Configure(EntityTypeBuilder<PositionAssignment> b)
    {
        b.ToTable("PositionAssignment", t =>
            t.HasCheckConstraint("CK_InterimHasEnd", "[AssignmentType] = 'primary' OR [EffectiveTo] IS NOT NULL"));
        b.Property(x => x.Id).HasColumnName("PositionAssignmentId");
        b.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Position>().WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
        // One current primary holder per position, and one current primary position per person.
        b.HasIndex(x => x.PositionId).IsUnique().HasDatabaseName("UX_AssignmentOpenPrimaryPosition")
            .HasFilter("[EffectiveTo] IS NULL AND [AssignmentType] = 'primary'");
        b.HasIndex(x => x.PersonId).IsUnique().HasDatabaseName("UX_AssignmentOpenPrimaryPerson")
            .HasFilter("[EffectiveTo] IS NULL AND [AssignmentType] = 'primary'");
        b.Ignore(x => x.IsOpen);
    }
}
