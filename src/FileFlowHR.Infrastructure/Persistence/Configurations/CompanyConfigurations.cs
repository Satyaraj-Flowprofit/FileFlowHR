using FileFlowHR.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileFlowHR.Infrastructure.Persistence.Configurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> b)
    {
        b.ToTable("Company");
        b.Property(x => x.Id).HasColumnName("CompanyId");
        b.Property(x => x.Code).HasColumnName("CompanyCode").HasMaxLength(30).IsUnicode(false);
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.LegalName).HasMaxLength(200);
        b.Property(x => x.CountryCode).HasMaxLength(2).IsFixedLength().IsUnicode(false);
    }
}

internal sealed class CompanyLoginProviderConfiguration : IEntityTypeConfiguration<CompanyLoginProvider>
{
    public void Configure(EntityTypeBuilder<CompanyLoginProvider> b)
    {
        b.ToTable("CompanyLoginProvider");
        b.Property(x => x.Id).HasColumnName("CompanyLoginProviderId");
        b.Property(x => x.DisplayName).HasMaxLength(100);
        b.Property(x => x.Authority).HasMaxLength(400).IsUnicode(false);
        b.Property(x => x.ClientId).HasMaxLength(200).IsUnicode(false);
        b.Property(x => x.SecretName).HasMaxLength(100).IsUnicode(false);
        b.Property(x => x.EmailDomain).HasMaxLength(253).IsUnicode(false);
        b.Ignore(x => x.SchemeName);
    }
}
