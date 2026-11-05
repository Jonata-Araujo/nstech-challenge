using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Domain.Common;
using OrderService.Domain.Products;

namespace OrderService.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", t =>
        {
            t.HasCheckConstraint("ck_products_available_quantity", "available_quantity >= 0");
            t.HasCheckConstraint("ck_products_unit_price", "unit_price > 0");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.UnitPrice).HasPrecision(18, 2);
        builder.Property(p => p.Currency)
            .HasConversion(c => c.Code, code => Currency.FromCode(code))
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();
        builder.Property(p => p.AvailableQuantity);
        builder.Property<uint>(PersistenceConstants.RowVersion).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");

        builder.HasData(SeedData.Products);
    }
}
