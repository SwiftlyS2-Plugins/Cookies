using Dapper;
using FluentMigrator;

namespace Cookies.Database.Migrations;

[Migration(1793692800)]
public class UniqueSteamId64 : Migration
{
    public override void Up()
    {
        // Keeps the lowest Id: the row earlier releases' unordered lookups returned on MySQL and SQLite.
        Execute.WithConnection((connection, transaction) =>
            connection.Execute(CookieSql.For(connection).DeleteDuplicates, transaction: transaction));

        Create.Index("UX_PlayerCookies_SteamId64").OnTable("PlayerCookies")
            .OnColumn("SteamId64").Ascending()
            .WithOptions().Unique();
    }

    public override void Down()
    {
        Delete.Index("UX_PlayerCookies_SteamId64").OnTable("PlayerCookies");
    }
}
