using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.MedicareAssistant.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrentPrescriptionDocumentIdToProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentPrescriptionDocumentId",
                table: "profiles",
                type: "varchar(24)",
                maxLength: 24,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentPrescriptionDocumentId",
                table: "profiles");
        }
    }
}
