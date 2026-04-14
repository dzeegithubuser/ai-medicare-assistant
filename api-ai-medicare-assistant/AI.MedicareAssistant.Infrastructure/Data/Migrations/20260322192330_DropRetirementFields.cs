using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.MedicareAssistant.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropRetirementFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RetirementState",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "RetirementZipCode",
                table: "profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RetirementState",
                table: "profiles",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RetirementZipCode",
                table: "profiles",
                type: "varchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
