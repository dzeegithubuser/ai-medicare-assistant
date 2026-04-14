using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.MedicareAssistant.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressLatLngCountyCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CountyCode",
                table: "addresses",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "addresses",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "addresses",
                type: "double",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CountyCode",
                table: "addresses");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "addresses");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "addresses");
        }
    }
}
