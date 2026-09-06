using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using TarlaAsistani.Infrastructure.Persistence;

namespace TarlaAsistani.UnitTests.Infrastructure;

public sealed class FinancialModelTests
{
    [Fact]
    public void Financial_entities_should_have_the_required_schema_contract()
    {
        using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=localhost;Database=tarla_test;Username=postgres;Password=postgres")
                .Options);

        var expense = db.Model.FindEntityType("TarlaAsistani.Domain.Entities.Expense");
        var sale = db.Model.FindEntityType("TarlaAsistani.Domain.Entities.CropSale");

        expense.Should().NotBeNull();
        sale.Should().NotBeNull();

        var expenseEntity = expense!;
        var saleEntity = sale!;

        expenseEntity.GetTableName().Should().Be("expenses");
        saleEntity.GetTableName().Should().Be("crop_sales");

        expenseEntity.FindProperty("Amount")!.ClrType.Should().Be(typeof(decimal));
        expenseEntity.FindProperty("Amount")!.GetPrecision().Should().Be(18);
        expenseEntity.FindProperty("Amount")!.GetScale().Should().Be(2);
        expenseEntity.FindProperty("Note")!.GetMaxLength().Should().Be(500);

        saleEntity.FindProperty("HarvestQuantity")!.ClrType.Should().Be(typeof(decimal));
        saleEntity.FindProperty("HarvestQuantity")!.GetPrecision().Should().Be(18);
        saleEntity.FindProperty("HarvestQuantity")!.GetScale().Should().Be(2);
        saleEntity.FindProperty("UnitPrice")!.GetPrecision().Should().Be(18);
        saleEntity.FindProperty("UnitPrice")!.GetScale().Should().Be(4);
        saleEntity.FindProperty("TotalAmount")!.GetPrecision().Should().Be(18);
        saleEntity.FindProperty("TotalAmount")!.GetScale().Should().Be(2);
        saleEntity.FindProperty("Unit")!.GetMaxLength().Should().Be(30);
        saleEntity.FindProperty("BuyerOrMarketNote")!.GetMaxLength().Should().Be(500);

        expenseEntity.GetIndexes().Should().Contain(index =>
            index.IsUnique &&
            index.GetFilter() == "\"ActivityId\" IS NOT NULL");
        expenseEntity.GetIndexes().Should().Contain(index =>
            index.Properties.Any(property => property.Name == "FarmId") &&
            index.Properties.Any(property => property.Name == "CropPeriodId") &&
            index.Properties.Any(property => property.Name == "ArchivedAtUtc"));
        saleEntity.GetIndexes().Should().Contain(index =>
            index.Properties.Any(property => property.Name == "FarmId") &&
            index.Properties.Any(property => property.Name == "CropPeriodId") &&
            index.Properties.Any(property => property.Name == "ArchivedAtUtc"));

        expenseEntity.GetForeignKeys().Should().Contain(fk =>
            fk.PrincipalEntityType.ClrType.Name == "Farm" &&
            fk.DeleteBehavior == DeleteBehavior.Restrict);
        saleEntity.GetForeignKeys().Should().Contain(fk =>
            fk.PrincipalEntityType.ClrType.Name == "Farm" &&
            fk.DeleteBehavior == DeleteBehavior.Restrict);
    }
}
