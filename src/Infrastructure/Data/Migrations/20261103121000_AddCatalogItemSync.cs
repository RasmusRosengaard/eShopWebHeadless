using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.eShopWeb.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    // Genereret med 5 AlterColumn på Orders/OrderItems/Catalog.Description fra ældre afvigelser mellem model og snapshot.
    // De er fjernet med vilje, så migrationen kun tilføjer kolonnerne til sync med CatalogService.
    public partial class AddCatalogItemSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Catalog",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Catalog",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Catalog");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Catalog");
        }
    }
}
