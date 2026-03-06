using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthCheckAI.Migrations.AppDb
{
    public partial class AddFileNameToPatientFiles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatientFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    DiagnosisA = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DiagnosisB = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DiagnosisC = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DiagnosisD = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.Id);
                });

            // ✅ 原本錯誤的 CreateTable("Users") 改成：只新增 Name 欄位
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientFiles");

            migrationBuilder.DropTable(
                name: "Reports");

            // ✅ 對應 Up 的 AddColumn
            migrationBuilder.DropColumn(
                name: "Name",
                table: "Users");
        }
    }
}