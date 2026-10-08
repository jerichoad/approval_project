using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backendApproval.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_access_requests_guard_update()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF OLD.status IN ('Approved', 'Rejected') THEN
        RAISE EXCEPTION 'access_request % is terminal (%)', OLD.id, OLD.status
            USING ERRCODE = 'check_violation';
    END IF;

    IF NOT (
           (OLD.status = 'PendingManagerApproval'
                AND NEW.status IN ('PendingSystemOwnerApproval', 'Approved', 'Rejected'))
        OR (OLD.status = 'PendingSystemOwnerApproval'
                AND NEW.status IN ('Approved', 'Rejected'))
    ) THEN
        RAISE EXCEPTION 'illegal transition % -> % on access_request %', OLD.status, NEW.status, OLD.id
            USING ERRCODE = 'check_violation';
    END IF;

    IF OLD.policy_version = 'v1'
       AND OLD.is_high_risk
       AND OLD.status = 'PendingManagerApproval'
       AND NEW.status = 'Approved' THEN
        RAISE EXCEPTION 'v1 high-risk request % must go through System Owner approval', OLD.id
            USING ERRCODE = 'check_violation';
    END IF;

    IF NEW.version <> OLD.version + 1 THEN
        RAISE EXCEPTION 'version must increment by exactly 1 (old %, new %)', OLD.version, NEW.version
            USING ERRCODE = 'check_violation';
    END IF;

    IF NEW.id                       IS DISTINCT FROM OLD.id
    OR NEW.requester_id             IS DISTINCT FROM OLD.requester_id
    OR NEW.client_request_id        IS DISTINCT FROM OLD.client_request_id
    OR NEW.application_id           IS DISTINCT FROM OLD.application_id
    OR NEW.environment              IS DISTINCT FROM OLD.environment
    OR NEW.access_level             IS DISTINCT FROM OLD.access_level
    OR NEW.justification            IS DISTINCT FROM OLD.justification
    OR NEW.policy_version           IS DISTINCT FROM OLD.policy_version
    OR NEW.is_high_risk             IS DISTINCT FROM OLD.is_high_risk
    OR NEW.manager_approver_id      IS DISTINCT FROM OLD.manager_approver_id
    OR NEW.system_owner_approver_id IS DISTINCT FROM OLD.system_owner_approver_id
    OR NEW.created_at               IS DISTINCT FROM OLD.created_at
    THEN
        RAISE EXCEPTION 'immutable column changed on access_request %', OLD.id
            USING ERRCODE = 'check_violation';
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_access_requests_guard_update
    BEFORE UPDATE ON access_requests
    FOR EACH ROW
    EXECUTE FUNCTION fn_access_requests_guard_update();
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_access_requests_guard_insert()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.status <> 'PendingManagerApproval' OR NEW.version <> 1 THEN
        RAISE EXCEPTION 'new access_request must start at PendingManagerApproval/version 1 (got %/%)',
            NEW.status, NEW.version
            USING ERRCODE = 'check_violation';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_access_requests_guard_insert
    BEFORE INSERT ON access_requests
    FOR EACH ROW
    EXECUTE FUNCTION fn_access_requests_guard_insert();
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_audit_events_append_only()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'audit_events is append-only (% blocked)', TG_OP
        USING ERRCODE = 'insufficient_privilege';
END;
$$;

CREATE TRIGGER trg_audit_events_no_update_delete
    BEFORE UPDATE OR DELETE ON audit_events
    FOR EACH ROW
    EXECUTE FUNCTION fn_audit_events_append_only();

CREATE TRIGGER trg_audit_events_no_truncate
    BEFORE TRUNCATE ON audit_events
    FOR EACH STATEMENT
    EXECUTE FUNCTION fn_audit_events_append_only();
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_audit_events_no_truncate ON audit_events;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_audit_events_no_update_delete ON audit_events;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_audit_events_append_only();");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_access_requests_guard_insert ON access_requests;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_access_requests_guard_insert();");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_access_requests_guard_update ON access_requests;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_access_requests_guard_update();");
        }
    }
}
