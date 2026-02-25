using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthCheckAI.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddAiScoreToPatientFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "PatientFiles");

            migrationBuilder.AddColumn<int>(
                name: "AiScore",
                table: "PatientFiles",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiScore",
                table: "PatientFiles");

            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "PatientFiles",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
