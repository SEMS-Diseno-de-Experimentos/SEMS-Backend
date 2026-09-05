using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sems.Api.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreacionInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "al_alerts",
                columns: table => new
                {
                    AlertId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ThresholdId = table.Column<Guid>(type: "uuid", nullable: true),
                    InactivityRuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    AlertType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Message = table.Column<string>(type: "text", nullable: true),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TriggeredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_al_alerts", x => x.AlertId);
                });

            migrationBuilder.CreateTable(
                name: "al_inactivity_rules",
                columns: table => new
                {
                    InactivityRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    RuleName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    MaxInactiveMinutes = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_al_inactivity_rules", x => x.InactivityRuleId);
                });

            migrationBuilder.CreateTable(
                name: "al_notification_logs",
                columns: table => new
                {
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertId = table.Column<Guid>(type: "uuid", nullable: true),
                    Channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Recipient = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_al_notification_logs", x => x.NotificationId);
                });

            migrationBuilder.CreateTable(
                name: "al_notification_preferences",
                columns: table => new
                {
                    PreferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    MinSeverity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    QuietHoursStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    QuietHoursEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_al_notification_preferences", x => x.PreferenceId);
                });

            migrationBuilder.CreateTable(
                name: "al_thresholds",
                columns: table => new
                {
                    ThresholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ThresholdName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Metric = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Operator = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ThresholdValue = table.Column<double>(type: "double precision", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_al_thresholds", x => x.ThresholdId);
                });

            migrationBuilder.CreateTable(
                name: "an_anomalies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    AnomalyType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActualKwh = table.Column<double>(type: "double precision", nullable: false),
                    ExpectedKwh = table.Column<double>(type: "double precision", nullable: false),
                    DeviationPercentage = table.Column<double>(type: "double precision", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_an_anomalies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "an_bill_predictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PredictionYear = table.Column<int>(type: "integer", nullable: false),
                    PredictionMonth = table.Column<int>(type: "integer", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstimatedKwh = table.Column<double>(type: "double precision", nullable: false),
                    EstimatedAmount = table.Column<double>(type: "double precision", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TariffUsed = table.Column<double>(type: "double precision", nullable: false),
                    ErrorMarginPercentage = table.Column<double>(type: "double precision", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_an_bill_predictions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "an_consumption_rankings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PeriodType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RankingsJson = table.Column<string>(type: "text", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_an_consumption_rankings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "an_device_identifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PredictedDeviceType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AnalyzedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_an_device_identifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "an_recommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    RecommendationType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    EstimatedSavingKwh = table.Column<double>(type: "double precision", nullable: false),
                    EstimatedSavingAmount = table.Column<double>(type: "double precision", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_an_recommendations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "dm_device_bindings",
                columns: table => new
                {
                    BindingId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeId = table.Column<Guid>(type: "uuid", nullable: true),
                    BindingStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UnlinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dm_device_bindings", x => x.BindingId);
                });

            migrationBuilder.CreateTable(
                name: "dm_device_configurations",
                columns: table => new
                {
                    ConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ConfigValue = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dm_device_configurations", x => x.ConfigurationId);
                });

            migrationBuilder.CreateTable(
                name: "dm_device_events",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dm_device_events", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "dm_devices",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalDeviceCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DeviceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ConnectionProtocol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dm_devices", x => x.DeviceId);
                });

            migrationBuilder.CreateTable(
                name: "em_consumption_alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    MeterId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    AlertType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ThresholdValue = table.Column<double>(type: "double precision", nullable: false),
                    ActualValue = table.Column<double>(type: "double precision", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_em_consumption_alerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "em_device_consumptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    MeterId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    TotalKwh = table.Column<double>(type: "double precision", nullable: false),
                    CostEstimateSoles = table.Column<double>(type: "double precision", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeakPowerWatts = table.Column<double>(type: "double precision", nullable: false),
                    AveragePowerWatts = table.Column<double>(type: "double precision", nullable: false),
                    ReadingCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_em_device_consumptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "em_energy_meters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    MeterSerial = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Location = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FirmwareVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MaxPowerWatts = table.Column<double>(type: "double precision", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_em_energy_meters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "em_energy_readings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    MeterId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PowerWatts = table.Column<double>(type: "double precision", nullable: false),
                    Voltage = table.Column<double>(type: "double precision", nullable: false),
                    Current = table.Column<double>(type: "double precision", nullable: false),
                    Frequency = table.Column<double>(type: "double precision", nullable: false),
                    EnergyKwh = table.Column<double>(type: "double precision", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadingType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Phase = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_em_energy_readings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "iam_refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Revoked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_iam_refresh_tokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "iam_user_auth_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_iam_user_auth_tokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "iam_users",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailAddress = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_iam_users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "pm_invoices",
                columns: table => new
                {
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalAmount = table.Column<double>(type: "double precision", nullable: false),
                    PdfUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pm_invoices", x => x.InvoiceId);
                });

            migrationBuilder.CreateTable(
                name: "pm_payment_methods",
                columns: table => new
                {
                    PaymentMethodId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Brand = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Last4 = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    ExpMonth = table.Column<int>(type: "integer", nullable: false),
                    ExpYear = table.Column<int>(type: "integer", nullable: false),
                    StripePaymentMethodId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pm_payment_methods", x => x.PaymentMethodId);
                });

            migrationBuilder.CreateTable(
                name: "pm_payments",
                columns: table => new
                {
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentMethodId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<double>(type: "double precision", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    StripePaymentIntentId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pm_payments", x => x.PaymentId);
                });

            migrationBuilder.CreateTable(
                name: "pm_webhook_events",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ProviderEventId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    EventType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    Processed = table.Column<bool>(type: "boolean", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pm_webhook_events", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "sb_subscription_plans",
                columns: table => new
                {
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Price = table.Column<double>(type: "double precision", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BillingPeriod = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sb_subscription_plans", x => x.PlanId);
                });

            migrationBuilder.CreateTable(
                name: "sb_subscriptions",
                columns: table => new
                {
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StripeSubscriptionId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sb_subscriptions", x => x.SubscriptionId);
                });

            migrationBuilder.CreateTable(
                name: "sb_plan_features",
                columns: table => new
                {
                    FeatureId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    FeatureName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FeatureValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sb_plan_features", x => x.FeatureId);
                    table.ForeignKey(
                        name: "FK_sb_plan_features_sb_subscription_plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "sb_subscription_plans",
                        principalColumn: "PlanId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_al_alerts_DeviceId",
                table: "al_alerts",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_al_alerts_UserId",
                table: "al_alerts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_al_inactivity_rules_UserId",
                table: "al_inactivity_rules",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_al_notification_logs_AlertId",
                table: "al_notification_logs",
                column: "AlertId");

            migrationBuilder.CreateIndex(
                name: "IX_al_notification_preferences_UserId_Channel",
                table: "al_notification_preferences",
                columns: new[] { "UserId", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_al_thresholds_DeviceId",
                table: "al_thresholds",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_al_thresholds_UserId",
                table: "al_thresholds",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_an_anomalies_UserId",
                table: "an_anomalies",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_an_bill_predictions_UserId",
                table: "an_bill_predictions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_an_consumption_rankings_UserId",
                table: "an_consumption_rankings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_an_device_identifications_UserId",
                table: "an_device_identifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_an_recommendations_UserId",
                table: "an_recommendations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_dm_device_bindings_DeviceId",
                table: "dm_device_bindings",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_dm_device_bindings_UserId",
                table: "dm_device_bindings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_dm_device_configurations_DeviceId_ConfigKey",
                table: "dm_device_configurations",
                columns: new[] { "DeviceId", "ConfigKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dm_device_events_DeviceId",
                table: "dm_device_events",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_dm_devices_ExternalDeviceCode",
                table: "dm_devices",
                column: "ExternalDeviceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dm_devices_UserId",
                table: "dm_devices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_em_consumption_alerts_UserId",
                table: "em_consumption_alerts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_em_device_consumptions_DeviceId",
                table: "em_device_consumptions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_em_device_consumptions_UserId",
                table: "em_device_consumptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_em_energy_meters_MeterSerial",
                table: "em_energy_meters",
                column: "MeterSerial",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_em_energy_meters_UserId",
                table: "em_energy_meters",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_em_energy_readings_DeviceId_Timestamp",
                table: "em_energy_readings",
                columns: new[] { "DeviceId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_em_energy_readings_MeterId_Timestamp",
                table: "em_energy_readings",
                columns: new[] { "MeterId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_em_energy_readings_UserId_Timestamp",
                table: "em_energy_readings",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_iam_refresh_tokens_TokenHash",
                table: "iam_refresh_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_iam_refresh_tokens_UserId",
                table: "iam_refresh_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_iam_user_auth_tokens_TokenHash",
                table: "iam_user_auth_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_iam_user_auth_tokens_UserId",
                table: "iam_user_auth_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_iam_users_EmailAddress",
                table: "iam_users",
                column: "EmailAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pm_invoices_PaymentId",
                table: "pm_invoices",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_pm_payment_methods_UserId",
                table: "pm_payment_methods",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_pm_payments_StripePaymentIntentId",
                table: "pm_payments",
                column: "StripePaymentIntentId");

            migrationBuilder.CreateIndex(
                name: "IX_pm_payments_SubscriptionId",
                table: "pm_payments",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_pm_payments_UserId",
                table: "pm_payments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_pm_webhook_events_ProviderEventId",
                table: "pm_webhook_events",
                column: "ProviderEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sb_plan_features_PlanId",
                table: "sb_plan_features",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_sb_subscription_plans_Name",
                table: "sb_subscription_plans",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sb_subscriptions_StripeSubscriptionId",
                table: "sb_subscriptions",
                column: "StripeSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_sb_subscriptions_UserId",
                table: "sb_subscriptions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "al_alerts");

            migrationBuilder.DropTable(
                name: "al_inactivity_rules");

            migrationBuilder.DropTable(
                name: "al_notification_logs");

            migrationBuilder.DropTable(
                name: "al_notification_preferences");

            migrationBuilder.DropTable(
                name: "al_thresholds");

            migrationBuilder.DropTable(
                name: "an_anomalies");

            migrationBuilder.DropTable(
                name: "an_bill_predictions");

            migrationBuilder.DropTable(
                name: "an_consumption_rankings");

            migrationBuilder.DropTable(
                name: "an_device_identifications");

            migrationBuilder.DropTable(
                name: "an_recommendations");

            migrationBuilder.DropTable(
                name: "dm_device_bindings");

            migrationBuilder.DropTable(
                name: "dm_device_configurations");

            migrationBuilder.DropTable(
                name: "dm_device_events");

            migrationBuilder.DropTable(
                name: "dm_devices");

            migrationBuilder.DropTable(
                name: "em_consumption_alerts");

            migrationBuilder.DropTable(
                name: "em_device_consumptions");

            migrationBuilder.DropTable(
                name: "em_energy_meters");

            migrationBuilder.DropTable(
                name: "em_energy_readings");

            migrationBuilder.DropTable(
                name: "iam_refresh_tokens");

            migrationBuilder.DropTable(
                name: "iam_user_auth_tokens");

            migrationBuilder.DropTable(
                name: "iam_users");

            migrationBuilder.DropTable(
                name: "pm_invoices");

            migrationBuilder.DropTable(
                name: "pm_payment_methods");

            migrationBuilder.DropTable(
                name: "pm_payments");

            migrationBuilder.DropTable(
                name: "pm_webhook_events");

            migrationBuilder.DropTable(
                name: "sb_plan_features");

            migrationBuilder.DropTable(
                name: "sb_subscriptions");

            migrationBuilder.DropTable(
                name: "sb_subscription_plans");
        }
    }
}
