using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sems.Api.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MedidoresPorLocalYSilencioDeDemanda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SiteId",
                table: "em_energy_meters",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ZoneId",
                table: "em_energy_meters",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAlertAt",
                table: "al_demand_rules",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastAlertLevel",
                table: "al_demand_rules",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_em_energy_meters_SiteId",
                table: "em_energy_meters",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_em_energy_meters_ZoneId",
                table: "em_energy_meters",
                column: "ZoneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_em_energy_meters_SiteId",
                table: "em_energy_meters");

            migrationBuilder.DropIndex(
                name: "IX_em_energy_meters_ZoneId",
                table: "em_energy_meters");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "em_energy_meters");

            migrationBuilder.DropColumn(
                name: "ZoneId",
                table: "em_energy_meters");

            migrationBuilder.DropColumn(
                name: "LastAlertAt",
                table: "al_demand_rules");

            migrationBuilder.DropColumn(
                name: "LastAlertLevel",
                table: "al_demand_rules");
        }
    }
}
