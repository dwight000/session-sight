using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SessionSight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewReasons",
                table: "Extractions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ReviewStatus",
                table: "Extractions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotFlagged");

            migrationBuilder.CreateTable(
                name: "SupervisorReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExtractionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReviewerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupervisorReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupervisorReviews_Extractions_ExtractionId",
                        column: x => x.ExtractionId,
                        principalTable: "Extractions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupervisorReviews_ExtractionId",
                table: "SupervisorReviews",
                column: "ExtractionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupervisorReviews");

            migrationBuilder.DropColumn(
                name: "ReviewReasons",
                table: "Extractions");

            migrationBuilder.DropColumn(
                name: "ReviewStatus",
                table: "Extractions");
        }
    }
}
