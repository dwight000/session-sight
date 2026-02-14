using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SessionSight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedDefaultTherapist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Therapists",
                type: "datetime2",
                nullable: true);

            migrationBuilder.InsertData(
                table: "Therapists",
                columns: new[] { "Id", "CreatedAt", "Credentials", "IsActive", "LicenseNumber", "Name", "UpdatedAt" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, null, "Default Therapist", null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Therapists",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Therapists");
        }
    }
}
