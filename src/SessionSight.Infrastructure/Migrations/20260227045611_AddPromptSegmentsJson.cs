using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SessionSight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptSegmentsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PromptSegmentsJson",
                table: "ExtractionLlmTraces",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PromptSegmentsJson",
                table: "ExtractionLlmTraces");
        }
    }
}
