using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sems.Api.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExclusionDeDomingosEnHoraPunta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExcludesSundaysFromPeak",
                table: "og_sites",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExcludesSundaysFromPeak",
                table: "og_sites");
        }
    }
}
