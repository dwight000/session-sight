using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SessionSight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSummaryToExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SummaryJson",
                table: "Extractions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SummaryJson",
                table: "Extractions");
        }
    }
}
