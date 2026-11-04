using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sems.Api.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DatosDeLaAlertaDeDemanda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ContractedKw",
                table: "al_alerts",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MeasuredKw",
                table: "al_alerts",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SiteId",
                table: "al_alerts",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContractedKw",
                table: "al_alerts");

            migrationBuilder.DropColumn(
                name: "MeasuredKw",
                table: "al_alerts");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "al_alerts");
        }
    }
}
