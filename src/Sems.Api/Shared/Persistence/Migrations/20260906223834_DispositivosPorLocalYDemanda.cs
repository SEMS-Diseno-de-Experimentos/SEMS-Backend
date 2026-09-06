using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sems.Api.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DispositivosPorLocalYDemanda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "HomeId",
                table: "dm_device_bindings",
                newName: "SiteId");

            migrationBuilder.AddColumn<Guid>(
                name: "SiteId",
                table: "dm_devices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ZoneId",
                table: "dm_devices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EnergyCost",
                table: "an_bill_predictions",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "EstimatedKwhOffPeak",
                table: "an_bill_predictions",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "EstimatedKwhPeak",
                table: "an_bill_predictions",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "EstimatedMaxDemandKw",
                table: "an_bill_predictions",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PowerCost",
                table: "an_bill_predictions",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "SiteId",
                table: "an_bill_predictions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "al_demand_rules",
                columns: table => new
                {
                    DemandRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ContractedPowerKw = table.Column<double>(type: "double precision", nullable: false),
                    WarningPercent = table.Column<double>(type: "double precision", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_al_demand_rules", x => x.DemandRuleId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dm_devices_SiteId",
                table: "dm_devices",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_dm_devices_ZoneId",
                table: "dm_devices",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_al_demand_rules_SiteId",
                table: "al_demand_rules",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_al_demand_rules_UserId",
                table: "al_demand_rules",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "al_demand_rules");

            migrationBuilder.DropIndex(
                name: "IX_dm_devices_SiteId",
                table: "dm_devices");

            migrationBuilder.DropIndex(
                name: "IX_dm_devices_ZoneId",
                table: "dm_devices");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "dm_devices");

            migrationBuilder.DropColumn(
                name: "ZoneId",
                table: "dm_devices");

            migrationBuilder.DropColumn(
                name: "EnergyCost",
                table: "an_bill_predictions");

            migrationBuilder.DropColumn(
                name: "EstimatedKwhOffPeak",
                table: "an_bill_predictions");

            migrationBuilder.DropColumn(
                name: "EstimatedKwhPeak",
                table: "an_bill_predictions");

            migrationBuilder.DropColumn(
                name: "EstimatedMaxDemandKw",
                table: "an_bill_predictions");

            migrationBuilder.DropColumn(
                name: "PowerCost",
                table: "an_bill_predictions");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "an_bill_predictions");

            migrationBuilder.RenameColumn(
                name: "SiteId",
                table: "dm_device_bindings",
                newName: "HomeId");
        }
    }
}
