using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SessionSight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CleanupRiskDiagnosticsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RiskDecisionsJson",
                table: "Extractions",
                newName: "RiskFieldDecisionsJson");

            migrationBuilder.RenameColumn(
                name: "CriteriaValidationAttemptsUsed",
                table: "Extractions",
                newName: "CriteriaValidationAttempts");

            migrationBuilder.AlterColumn<string>(
                name: "SelfHarmGuardrailReason",
                table: "Extractions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "HomicidalGuardrailReason",
                table: "Extractions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiscrepancyCount",
                table: "Extractions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "GuardrailApplied",
                table: "Extractions",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscrepancyCount",
                table: "Extractions");

            migrationBuilder.DropColumn(
                name: "GuardrailApplied",
                table: "Extractions");

            migrationBuilder.RenameColumn(
                name: "RiskFieldDecisionsJson",
                table: "Extractions",
                newName: "RiskDecisionsJson");

            migrationBuilder.RenameColumn(
                name: "CriteriaValidationAttempts",
                table: "Extractions",
                newName: "CriteriaValidationAttemptsUsed");

            migrationBuilder.AlterColumn<string>(
                name: "SelfHarmGuardrailReason",
                table: "Extractions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "HomicidalGuardrailReason",
                table: "Extractions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }
    }
}
