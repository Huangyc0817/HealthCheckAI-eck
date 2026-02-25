using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthCheckAI.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddIsLockedToPatientFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiagnosisE",
                table: "Reports",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DiagnosisF",
                table: "Reports",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AiSeverity",
                table: "PatientFiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiSummary",
                table: "PatientFiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "PatientFiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedText",
                table: "PatientFiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "PatientFiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublishedToPublic",
                table: "PatientFiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "PatientFiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UploadedAt",
                table: "PatientFiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReportFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginalName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExtractedText = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportFiles", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportFiles");

            migrationBuilder.DropColumn(
                name: "DiagnosisE",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "DiagnosisF",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "AiSeverity",
                table: "PatientFiles");

            migrationBuilder.DropColumn(
                name: "AiSummary",
                table: "PatientFiles");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "PatientFiles");

            migrationBuilder.DropColumn(
                name: "ExtractedText",
                table: "PatientFiles");

            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "PatientFiles");

            migrationBuilder.DropColumn(
                name: "IsPublishedToPublic",
                table: "PatientFiles");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "PatientFiles");

            migrationBuilder.DropColumn(
                name: "UploadedAt",
                table: "PatientFiles");
        }
    }
}
