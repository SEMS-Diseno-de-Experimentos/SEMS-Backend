using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sems.Api.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SegmentoEstablecimientos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "og_memberships",
                columns: table => new
                {
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_og_memberships", x => x.MembershipId);
                });

            migrationBuilder.CreateTable(
                name: "og_organizations",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TradeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TaxId = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    BusinessType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_og_organizations", x => x.OrganizationId);
                });

            migrationBuilder.CreateTable(
                name: "og_sites",
                columns: table => new
                {
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    District = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    FloorAreaM2 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    ContractedPowerKw = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    TariffCategory = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_og_sites", x => x.SiteId);
                });

            migrationBuilder.CreateTable(
                name: "og_zones",
                columns: table => new
                {
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ZoneType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OperatesOffHours = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_og_zones", x => x.ZoneId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_og_memberships_OrganizationId",
                table: "og_memberships",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_og_memberships_OrganizationId_UserId",
                table: "og_memberships",
                columns: new[] { "OrganizationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_og_memberships_UserId",
                table: "og_memberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_og_organizations_TaxId",
                table: "og_organizations",
                column: "TaxId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_og_sites_OrganizationId",
                table: "og_sites",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_og_sites_OrganizationId_SiteCode",
                table: "og_sites",
                columns: new[] { "OrganizationId", "SiteCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_og_zones_SiteId",
                table: "og_zones",
                column: "SiteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "og_memberships");

            migrationBuilder.DropTable(
                name: "og_organizations");

            migrationBuilder.DropTable(
                name: "og_sites");

            migrationBuilder.DropTable(
                name: "og_zones");
        }
    }
}
