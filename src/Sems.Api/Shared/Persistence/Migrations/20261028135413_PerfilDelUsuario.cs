using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sems.Api.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PerfilDelUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "iam_users",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Segment",
                table: "iam_users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FullName",
                table: "iam_users");

            migrationBuilder.DropColumn(
                name: "Segment",
                table: "iam_users");
        }
    }
}
