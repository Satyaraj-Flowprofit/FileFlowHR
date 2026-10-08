using FileFlowHR.Domain.Companies;
using FileFlowHR.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileFlowHR.Infrastructure.Persistence.Configurations;

/// <summary>ASP.NET Core Identity tables, renamed to the agreed schema.</summary>
internal sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> b)
    {
        b.ToTable("UserAccount");
        b.Property(x => x.Id).HasColumnName("UserAccountId");
        b.Property(x => x.UserType).HasMaxLength(10).IsUnicode(false);
        b.Property(x => x.Status).HasMaxLength(10).IsUnicode(false);
        b.Property(x => x.PhoneNumber).HasMaxLength(30);
        b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.CompanyId, x.NormalizedEmail }).IsUnique();
        b.HasIndex(x => new { x.CompanyId, x.PersonId });
    }
}

internal sealed class UserExternalLoginConfiguration : IEntityTypeConfiguration<UserExternalLogin>
{
    public void Configure(EntityTypeBuilder<UserExternalLogin> b)
    {
        b.ToTable("UserExternalLogin");
        b.Property(x => x.UserId).HasColumnName("UserAccountId");
    }
}

internal sealed class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> b)
    {
        b.ToTable("UserToken");
        b.Property(x => x.UserId).HasColumnName("UserAccountId");
    }
}

internal sealed class UserClaimConfiguration : IEntityTypeConfiguration<UserClaim>
{
    public void Configure(EntityTypeBuilder<UserClaim> b)
    {
        b.ToTable("UserClaim");
        b.Property(x => x.Id).HasColumnName("UserClaimId");
        b.Property(x => x.UserId).HasColumnName("UserAccountId");
    }
}
