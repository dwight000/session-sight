using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SessionSight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddToolCallInputOutputAndLlmTraceColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InputJson",
                table: "ExtractionToolCalls",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutputJson",
                table: "ExtractionToolCalls",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InputJson",
                table: "ExtractionToolCalls");

            migrationBuilder.DropColumn(
                name: "OutputJson",
                table: "ExtractionToolCalls");
        }
    }
}
