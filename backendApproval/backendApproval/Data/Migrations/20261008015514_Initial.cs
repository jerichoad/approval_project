using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace backendApproval.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "policy_versions",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_policy_versions", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    manager_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_auditor = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_users_email_lowercase", "email = lower(email)");
                    table.CheckConstraint("ck_users_not_own_manager", "manager_id IS NULL OR manager_id <> id");
                    table.ForeignKey(
                        name: "fk_users_manager",
                        column: x => x.manager_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    system_owner_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_applications", x => x.id);
                    table.ForeignKey(
                        name: "fk_applications_system_owner",
                        column: x => x.system_owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "access_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_request_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    requester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    access_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    justification = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    policy_version = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_high_risk = table.Column<bool>(type: "boolean", nullable: false),
                    manager_approver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    system_owner_approver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_access_requests", x => x.id);
                    table.CheckConstraint("ck_access_requests_access_level", "access_level IN ('Read', 'Admin')");
                    table.CheckConstraint("ck_access_requests_client_request_id_not_blank", "length(btrim(client_request_id)) > 0");
                    table.CheckConstraint("ck_access_requests_decided_at", "(status IN ('Approved', 'Rejected')) = (decided_at IS NOT NULL)");
                    table.CheckConstraint("ck_access_requests_environment", "environment IN ('NonProduction', 'Production')");
                    table.CheckConstraint("ck_access_requests_justification_not_blank", "length(btrim(justification)) > 0");
                    table.CheckConstraint("ck_access_requests_manager_not_requester", "manager_approver_id <> requester_id");
                    table.CheckConstraint("ck_access_requests_owner_not_requester", "system_owner_approver_id IS NULL OR system_owner_approver_id <> requester_id");
                    table.CheckConstraint("ck_access_requests_rejection_reason", "(status = 'Rejected' AND rejection_reason IS NOT NULL AND length(btrim(rejection_reason)) > 0) OR (status <> 'Rejected' AND rejection_reason IS NULL)");
                    table.CheckConstraint("ck_access_requests_status", "status IN ('PendingManagerApproval', 'PendingSystemOwnerApproval', 'Approved', 'Rejected')");
                    table.CheckConstraint("ck_access_requests_v1_high_risk_definition", "policy_version <> 'v1' OR is_high_risk = (environment = 'Production' OR access_level = 'Admin')");
                    table.CheckConstraint("ck_access_requests_v1_low_risk_no_owner_stage", "policy_version <> 'v1' OR NOT (status = 'PendingSystemOwnerApproval' AND NOT is_high_risk)");
                    table.CheckConstraint("ck_access_requests_v1_owner_assignment", "policy_version <> 'v1' OR (is_high_risk = (system_owner_approver_id IS NOT NULL))");
                    table.CheckConstraint("ck_access_requests_version_positive", "version >= 1");
                    table.ForeignKey(
                        name: "fk_access_requests_application",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_access_requests_manager",
                        column: x => x.manager_approver_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_access_requests_policy_version",
                        column: x => x.policy_version,
                        principalTable: "policy_versions",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_access_requests_requester",
                        column: x => x.requester_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_access_requests_system_owner",
                        column: x => x.system_owner_approver_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    access_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    to_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    policy_version = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    request_version = table.Column<int>(type: "integer", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_events", x => x.id);
                    table.CheckConstraint("ck_audit_events_event_type", "event_type IN ('RequestCreated', 'ManagerApproved', 'ManagerRejected', 'SystemOwnerApproved', 'SystemOwnerRejected')");
                    table.CheckConstraint("ck_audit_events_reject_has_reason", "event_type NOT IN ('ManagerRejected', 'SystemOwnerRejected') OR (reason IS NOT NULL AND length(btrim(reason)) > 0)");
                    table.ForeignKey(
                        name: "fk_audit_events_access_request",
                        column: x => x.access_request_id,
                        principalTable: "access_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_audit_events_actor",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_audit_events_policy_version",
                        column: x => x.policy_version,
                        principalTable: "policy_versions",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "policy_versions",
                columns: new[] { "code", "description", "effective_from", "is_active" },
                values: new object[] { "v1", "High-risk = Environment Production OR AccessLevel Admin. Semua request butuh Manager approval; high-risk lanjut ke System Owner approval.", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "display_name", "email", "manager_id" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000002"), "Bob", "bob@example.local", null },
                    { new Guid("00000000-0000-0000-0000-000000000003"), "Carol", "carol@example.local", null },
                    { new Guid("00000000-0000-0000-0000-000000000004"), "Dana", "dana@example.local", null }
                });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "display_name", "email", "is_auditor", "manager_id" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), "Erin", "erin@example.local", true, null });

            migrationBuilder.InsertData(
                table: "applications",
                columns: new[] { "id", "code", "name", "system_owner_id" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "CRM", "CRM", new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "FINANCE_PORTAL", "Finance Portal", new Guid("00000000-0000-0000-0000-000000000004") }
                });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "display_name", "email", "manager_id" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), "Alice", "alice@example.local", new Guid("00000000-0000-0000-0000-000000000002") });

            migrationBuilder.CreateIndex(
                name: "ix_access_requests_application_id",
                table: "access_requests",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_access_requests_manager_status",
                table: "access_requests",
                columns: new[] { "manager_approver_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_access_requests_policy_version",
                table: "access_requests",
                column: "policy_version");

            migrationBuilder.CreateIndex(
                name: "ix_access_requests_requester_created",
                table: "access_requests",
                columns: new[] { "requester_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_access_requests_system_owner_status",
                table: "access_requests",
                columns: new[] { "system_owner_approver_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_access_requests_requester_client_request_id",
                table: "access_requests",
                columns: new[] { "requester_id", "client_request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_applications_system_owner_id",
                table: "applications",
                column: "system_owner_id");

            migrationBuilder.CreateIndex(
                name: "ux_applications_code",
                table: "applications",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_actor_id",
                table: "audit_events",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_policy_version",
                table: "audit_events",
                column: "policy_version");

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_request_occurred",
                table: "audit_events",
                columns: new[] { "access_request_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ux_audit_events_request_version",
                table: "audit_events",
                columns: new[] { "access_request_id", "request_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_policy_versions_single_active",
                table: "policy_versions",
                column: "is_active",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_users_manager_id",
                table: "users",
                column: "manager_id");

            migrationBuilder.CreateIndex(
                name: "ux_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "access_requests");

            migrationBuilder.DropTable(
                name: "applications");

            migrationBuilder.DropTable(
                name: "policy_versions");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
