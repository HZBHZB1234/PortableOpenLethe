using Microsoft.EntityFrameworkCore;
using OpenLethe.Data;

public class AccountModelTests
{
    private static AppDbContext BuildContext()
    {
        // No connection needed - we only inspect model metadata, never query.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void Account_MapsToAccountsTable_WithUniqueUsername()
    {
        using var ctx = BuildContext();
        var et = ctx.Model.FindEntityType(typeof(Account))!;

        Assert.Equal("accounts", et.GetTableName());
        Assert.Contains(et.GetIndexes(), i =>
            i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == nameof(Account.Username));
    }

    [Fact]
    public void GameDataColumns_AreText()
    {
        using var ctx = BuildContext();
        var et = ctx.Model.FindEntityType(typeof(Account))!;

        Assert.Equal("TEXT", et.FindProperty(nameof(Account.MdSaveInfo))!.GetColumnType());
        Assert.Equal("TEXT", et.FindProperty(nameof(Account.Egos))!.GetColumnType());
    }
}

