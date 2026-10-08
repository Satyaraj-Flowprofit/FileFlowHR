using FileFlowHR.Domain.Organisation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileFlowHR.Infrastructure.Persistence.Configurations;

internal sealed class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public void Configure(EntityTypeBuilder<Region> b)
    {
        b.ToTable("Region");
        b.Property(x => x.Id).HasColumnName("RegionId");
        b.Property(x => x.Name).HasColumnName("RegionName").HasMaxLength(100);
        b.Property(x => x.CountryCode).HasMaxLength(2).IsFixedLength().IsUnicode(false);
        b.Property(x => x.TimeZone).HasMaxLength(64).IsUnicode(false);
        b.Ignore(x => x.IsOpen);
    }
}

internal sealed class BusinessUnitConfiguration : IEntityTypeConfiguration<BusinessUnit>
{
    public void Configure(EntityTypeBuilder<BusinessUnit> b)
    {
        b.ToTable("BusinessUnit");
        b.Property(x => x.Id).HasColumnName("BusinessUnitId");
        b.Property(x => x.Name).HasColumnName("BusinessUnitName").HasMaxLength(150);
        b.HasOne<Region>().WithMany().HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<BusinessUnit>().WithMany().HasForeignKey(x => x.ParentBusinessUnitId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.IsOpen);
    }
}

internal sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> b)
    {
        b.ToTable("Location");
        b.Property(x => x.Id).HasColumnName("LocationId");
        b.Property(x => x.Name).HasColumnName("LocationName").HasMaxLength(150);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.CountryCode).HasMaxLength(2).IsFixedLength().IsUnicode(false);
        b.HasOne<Region>().WithMany().HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> b)
    {
        b.ToTable("Position");
        b.Property(x => x.Id).HasColumnName("PositionId");
        b.Property(x => x.Code).HasColumnName("PositionCode").HasMaxLength(30).IsUnicode(false);
        b.Property(x => x.Title).HasMaxLength(150);
        b.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        b.HasOne<BusinessUnit>().WithMany().HasForeignKey(x => x.BusinessUnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.IsOpen);
    }
}

internal sealed class PositionReportingLineConfiguration : IEntityTypeConfiguration<PositionReportingLine>
{
    public void Configure(EntityTypeBuilder<PositionReportingLine> b)
    {
        b.ToTable("PositionReportingLine", t => t.HasCheckConstraint("CK_ReportingLineNotSelf", "[PositionId] <> [ManagerPositionId]"));
        b.Property(x => x.Id).HasColumnName("PositionReportingLineId");
        b.HasOne<Position>().WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Position>().WithMany().HasForeignKey(x => x.ManagerPositionId).OnDelete(DeleteBehavior.Restrict);
        // One open solid line per position.
        b.HasIndex(x => x.PositionId).IsUnique().HasDatabaseName("UX_ReportingLineOpenSolid")
            .HasFilter("[EffectiveTo] IS NULL AND [LineType] = 'solid'");
        b.Ignore(x => x.IsOpen);
    }
}
