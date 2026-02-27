using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SessionSight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContentFilterBlocked : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ContentFilterBlocked",
                table: "Extractions",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentFilterBlocked",
                table: "Extractions");
        }
    }
}
