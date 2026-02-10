using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SessionSight.Infrastructure.Data;

#nullable disable

namespace SessionSight.Infrastructure.Migrations;

/// <inheritdoc />
[DbContext(typeof(SessionSightDbContext))]
[Migration("20260210014000_UseRunLevelRiskDiagnosticsColumns")]
public partial class UseRunLevelRiskDiagnosticsColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DiagnosticsJson",
            table: "Extractions");

        migrationBuilder.AddColumn<int>(
            name: "CriteriaValidationAttemptsUsed",
            table: "Extractions",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<bool>(
            name: "HomicidalGuardrailApplied",
            table: "Extractions",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "HomicidalGuardrailReason",
            table: "Extractions",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RiskDecisionsJson",
            table: "Extractions",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "SelfHarmGuardrailApplied",
            table: "Extractions",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "SelfHarmGuardrailReason",
            table: "Extractions",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CriteriaValidationAttemptsUsed",
            table: "Extractions");

        migrationBuilder.DropColumn(
            name: "HomicidalGuardrailApplied",
            table: "Extractions");

        migrationBuilder.DropColumn(
            name: "HomicidalGuardrailReason",
            table: "Extractions");

        migrationBuilder.DropColumn(
            name: "RiskDecisionsJson",
            table: "Extractions");

        migrationBuilder.DropColumn(
            name: "SelfHarmGuardrailApplied",
            table: "Extractions");

        migrationBuilder.DropColumn(
            name: "SelfHarmGuardrailReason",
            table: "Extractions");

        migrationBuilder.AddColumn<string>(
            name: "DiagnosticsJson",
            table: "Extractions",
            type: "nvarchar(max)",
            nullable: true);
    }
}
