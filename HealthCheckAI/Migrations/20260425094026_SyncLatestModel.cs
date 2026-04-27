using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthCheckAI.Migrations
{
    /// <inheritdoc />
    public partial class SyncLatestModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "PatientFiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "PatientFiles",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
