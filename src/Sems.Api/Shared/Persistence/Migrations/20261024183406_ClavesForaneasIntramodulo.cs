using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sems.Api.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClavesForaneasIntramodulo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_sb_subscriptions_PlanId",
                table: "sb_subscriptions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_pm_payments_PaymentMethodId",
                table: "pm_payments",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_og_memberships_SiteId",
                table: "og_memberships",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_al_alerts_InactivityRuleId",
                table: "al_alerts",
                column: "InactivityRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_al_alerts_ThresholdId",
                table: "al_alerts",
                column: "ThresholdId");

            migrationBuilder.AddForeignKey(
                name: "FK_al_alerts_al_inactivity_rules_InactivityRuleId",
                table: "al_alerts",
                column: "InactivityRuleId",
                principalTable: "al_inactivity_rules",
                principalColumn: "InactivityRuleId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_al_alerts_al_thresholds_ThresholdId",
                table: "al_alerts",
                column: "ThresholdId",
                principalTable: "al_thresholds",
                principalColumn: "ThresholdId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_al_notification_logs_al_alerts_AlertId",
                table: "al_notification_logs",
                column: "AlertId",
                principalTable: "al_alerts",
                principalColumn: "AlertId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_dm_device_bindings_dm_devices_DeviceId",
                table: "dm_device_bindings",
                column: "DeviceId",
                principalTable: "dm_devices",
                principalColumn: "DeviceId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_dm_device_configurations_dm_devices_DeviceId",
                table: "dm_device_configurations",
                column: "DeviceId",
                principalTable: "dm_devices",
                principalColumn: "DeviceId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_dm_device_events_dm_devices_DeviceId",
                table: "dm_device_events",
                column: "DeviceId",
                principalTable: "dm_devices",
                principalColumn: "DeviceId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_iam_refresh_tokens_iam_users_UserId",
                table: "iam_refresh_tokens",
                column: "UserId",
                principalTable: "iam_users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_iam_user_auth_tokens_iam_users_UserId",
                table: "iam_user_auth_tokens",
                column: "UserId",
                principalTable: "iam_users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_og_memberships_og_organizations_OrganizationId",
                table: "og_memberships",
                column: "OrganizationId",
                principalTable: "og_organizations",
                principalColumn: "OrganizationId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_og_memberships_og_sites_SiteId",
                table: "og_memberships",
                column: "SiteId",
                principalTable: "og_sites",
                principalColumn: "SiteId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_og_sites_og_organizations_OrganizationId",
                table: "og_sites",
                column: "OrganizationId",
                principalTable: "og_organizations",
                principalColumn: "OrganizationId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_og_zones_og_sites_SiteId",
                table: "og_zones",
                column: "SiteId",
                principalTable: "og_sites",
                principalColumn: "SiteId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_pm_invoices_pm_payments_PaymentId",
                table: "pm_invoices",
                column: "PaymentId",
                principalTable: "pm_payments",
                principalColumn: "PaymentId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_pm_payments_pm_payment_methods_PaymentMethodId",
                table: "pm_payments",
                column: "PaymentMethodId",
                principalTable: "pm_payment_methods",
                principalColumn: "PaymentMethodId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_sb_subscriptions_sb_subscription_plans_PlanId",
                table: "sb_subscriptions",
                column: "PlanId",
                principalTable: "sb_subscription_plans",
                principalColumn: "PlanId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_al_alerts_al_inactivity_rules_InactivityRuleId",
                table: "al_alerts");

            migrationBuilder.DropForeignKey(
                name: "FK_al_alerts_al_thresholds_ThresholdId",
                table: "al_alerts");

            migrationBuilder.DropForeignKey(
                name: "FK_al_notification_logs_al_alerts_AlertId",
                table: "al_notification_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_dm_device_bindings_dm_devices_DeviceId",
                table: "dm_device_bindings");

            migrationBuilder.DropForeignKey(
                name: "FK_dm_device_configurations_dm_devices_DeviceId",
                table: "dm_device_configurations");

            migrationBuilder.DropForeignKey(
                name: "FK_dm_device_events_dm_devices_DeviceId",
                table: "dm_device_events");

            migrationBuilder.DropForeignKey(
                name: "FK_iam_refresh_tokens_iam_users_UserId",
                table: "iam_refresh_tokens");

            migrationBuilder.DropForeignKey(
                name: "FK_iam_user_auth_tokens_iam_users_UserId",
                table: "iam_user_auth_tokens");

            migrationBuilder.DropForeignKey(
                name: "FK_og_memberships_og_organizations_OrganizationId",
                table: "og_memberships");

            migrationBuilder.DropForeignKey(
                name: "FK_og_memberships_og_sites_SiteId",
                table: "og_memberships");

            migrationBuilder.DropForeignKey(
                name: "FK_og_sites_og_organizations_OrganizationId",
                table: "og_sites");

            migrationBuilder.DropForeignKey(
                name: "FK_og_zones_og_sites_SiteId",
                table: "og_zones");

            migrationBuilder.DropForeignKey(
                name: "FK_pm_invoices_pm_payments_PaymentId",
                table: "pm_invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_pm_payments_pm_payment_methods_PaymentMethodId",
                table: "pm_payments");

            migrationBuilder.DropForeignKey(
                name: "FK_sb_subscriptions_sb_subscription_plans_PlanId",
                table: "sb_subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_sb_subscriptions_PlanId",
                table: "sb_subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_pm_payments_PaymentMethodId",
                table: "pm_payments");

            migrationBuilder.DropIndex(
                name: "IX_og_memberships_SiteId",
                table: "og_memberships");

            migrationBuilder.DropIndex(
                name: "IX_al_alerts_InactivityRuleId",
                table: "al_alerts");

            migrationBuilder.DropIndex(
                name: "IX_al_alerts_ThresholdId",
                table: "al_alerts");
        }
    }
}
