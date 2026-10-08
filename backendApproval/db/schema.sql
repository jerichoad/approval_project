CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    CREATE TABLE policy_versions (
        code character varying(10) NOT NULL,
        description text NOT NULL,
        effective_from timestamp with time zone NOT NULL,
        is_active boolean NOT NULL DEFAULT FALSE,
        CONSTRAINT pk_policy_versions PRIMARY KEY (code)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    CREATE TABLE users (
        id uuid NOT NULL,
        email character varying(256) NOT NULL,
        display_name character varying(100) NOT NULL,
        manager_id uuid,
        is_auditor boolean NOT NULL DEFAULT FALSE,
        CONSTRAINT pk_users PRIMARY KEY (id),
        CONSTRAINT ck_users_email_lowercase CHECK (email = lower(email)),
        CONSTRAINT ck_users_not_own_manager CHECK (manager_id IS NULL OR manager_id <> id),
        CONSTRAINT fk_users_manager FOREIGN KEY (manager_id) REFERENCES users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    CREATE TABLE applications (
        id uuid NOT NULL,
        code character varying(50) NOT NULL,
        name character varying(100) NOT NULL,
        system_owner_id uuid NOT NULL,
        CONSTRAINT pk_applications PRIMARY KEY (id),
        CONSTRAINT fk_applications_system_owner FOREIGN KEY (system_owner_id) REFERENCES users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    CREATE TABLE access_requests (
        id uuid NOT NULL,
        client_request_id character varying(64) NOT NULL,
        requester_id uuid NOT NULL,
        application_id uuid NOT NULL,
        environment character varying(20) NOT NULL,
        access_level character varying(10) NOT NULL,
        justification character varying(1000) NOT NULL,
        status character varying(40) NOT NULL,
        policy_version character varying(10) NOT NULL,
        is_high_risk boolean NOT NULL,
        manager_approver_id uuid NOT NULL,
        system_owner_approver_id uuid,
        rejection_reason character varying(1000),
        version integer NOT NULL DEFAULT 1,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        decided_at timestamp with time zone,
        CONSTRAINT pk_access_requests PRIMARY KEY (id),
        CONSTRAINT ck_access_requests_access_level CHECK (access_level IN ('Read', 'Admin')),
        CONSTRAINT ck_access_requests_client_request_id_not_blank CHECK (length(btrim(client_request_id)) > 0),
        CONSTRAINT ck_access_requests_decided_at CHECK ((status IN ('Approved', 'Rejected')) = (decided_at IS NOT NULL)),
        CONSTRAINT ck_access_requests_environment CHECK (environment IN ('NonProduction', 'Production')),
        CONSTRAINT ck_access_requests_justification_not_blank CHECK (length(btrim(justification)) > 0),
        CONSTRAINT ck_access_requests_manager_not_requester CHECK (manager_approver_id <> requester_id),
        CONSTRAINT ck_access_requests_owner_not_requester CHECK (system_owner_approver_id IS NULL OR system_owner_approver_id <> requester_id),
        CONSTRAINT ck_access_requests_rejection_reason CHECK ((status = 'Rejected' AND rejection_reason IS NOT NULL AND length(btrim(rejection_reason)) > 0) OR (status <> 'Rejected' AND rejection_reason IS NULL)),
        CONSTRAINT ck_access_requests_status CHECK (status IN ('PendingManagerApproval', 'PendingSystemOwnerApproval', 'Approved', 'Rejected')),
        CONSTRAINT ck_access_requests_v1_high_risk_definition CHECK (policy_version <> 'v1' OR is_high_risk = (environment = 'Production' OR access_level = 'Admin')),
        CONSTRAINT ck_access_requests_v1_low_risk_no_owner_stage CHECK (policy_version <> 'v1' OR NOT (status = 'PendingSystemOwnerApproval' AND NOT is_high_risk)),
        CONSTRAINT ck_access_requests_v1_owner_assignment CHECK (policy_version <> 'v1' OR (is_high_risk = (system_owner_approver_id IS NOT NULL))),
        CONSTRAINT ck_access_requests_version_positive CHECK (version >= 1),
        CONSTRAINT fk_access_requests_application FOREIGN KEY (application_id) REFERENCES applications (id) ON DELETE RESTRICT,
        CONSTRAINT fk_access_requests_manager FOREIGN KEY (manager_approver_id) REFERENCES users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_access_requests_policy_version FOREIGN KEY (policy_version) REFERENCES policy_versions (code) ON DELETE RESTRICT,
        CONSTRAINT fk_access_requests_requester FOREIGN KEY (requester_id) REFERENCES users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_access_requests_system_owner FOREIGN KEY (system_owner_approver_id) REFERENCES users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    CREATE TABLE audit_events (
        id bigint GENERATED ALWAYS AS IDENTITY,
        access_request_id uuid NOT NULL,
        event_type character varying(40) NOT NULL,
        actor_id uuid NOT NULL,
        from_status character varying(40),
        to_status character varying(40) NOT NULL,
        reason character varying(1000),
        policy_version character varying(10) NOT NULL,
        request_version integer NOT NULL,
        correlation_id character varying(64),
        occurred_at timestamp with time zone NOT NULL,
        CONSTRAINT pk_audit_events PRIMARY KEY (id),
        CONSTRAINT ck_audit_events_event_type CHECK (event_type IN ('RequestCreated', 'ManagerApproved', 'ManagerRejected', 'SystemOwnerApproved', 'SystemOwnerRejected')),
        CONSTRAINT ck_audit_events_reject_has_reason CHECK (event_type NOT IN ('ManagerRejected', 'SystemOwnerRejected') OR (reason IS NOT NULL AND length(btrim(reason)) > 0)),
        CONSTRAINT fk_audit_events_access_request FOREIGN KEY (access_request_id) REFERENCES access_requests (id) ON DELETE RESTRICT,
        CONSTRAINT fk_audit_events_actor FOREIGN KEY (actor_id) REFERENCES users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_audit_events_policy_version FOREIGN KEY (policy_version) REFERENCES policy_versions (code) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    INSERT INTO policy_versions (code, description, effective_from, is_active)
    VALUES ('v1', 'High-risk = Environment Production OR AccessLevel Admin. Semua request butuh Manager approval; high-risk lanjut ke System Owner approval.', TIMESTAMPTZ '2026-01-01T00:00:00+00:00', TRUE);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    INSERT INTO users (id, display_name, email, manager_id)
    VALUES ('00000000-0000-0000-0000-000000000002', 'Bob', 'bob@example.local', NULL);
    INSERT INTO users (id, display_name, email, manager_id)
    VALUES ('00000000-0000-0000-0000-000000000003', 'Carol', 'carol@example.local', NULL);
    INSERT INTO users (id, display_name, email, manager_id)
    VALUES ('00000000-0000-0000-0000-000000000004', 'Dana', 'dana@example.local', NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    INSERT INTO users (id, display_name, email, is_auditor, manager_id)
    VALUES ('00000000-0000-0000-0000-000000000005', 'Erin', 'erin@example.local', TRUE, NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    INSERT INTO applications (id, code, name, system_owner_id)
    VALUES ('10000000-0000-0000-0000-000000000001', 'CRM', 'CRM', '00000000-0000-0000-0000-000000000003');
    INSERT INTO applications (id, code, name, system_owner_id)
    VALUES ('10000000-0000-0000-0000-000000000002', 'FINANCE_PORTAL', 'Finance Portal', '00000000-0000-0000-0000-000000000004');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    INSERT INTO users (id, display_name, email, manager_id)
    VALUES ('00000000-0000-0000-0000-000000000001', 'Alice', 'alice@example.local', '00000000-0000-0000-0000-000000000002');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    CREATE INDEX ix_access_requests_application_id ON access_requests (application_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    CREATE INDEX ix_access_requests_manager_status ON access_requests (manager_approver_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    CREATE INDEX ix_access_requests_policy_version ON access_requests (policy_version);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    CREATE INDEX ix_access_requests_requester_created ON access_requests (requester_id, created_at DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    CREATE INDEX ix_access_requests_system_owner_status ON access_requests (system_owner_approver_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    CREATE UNIQUE INDEX ux_access_requests_requester_client_request_id ON access_requests (requester_id, client_request_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    CREATE INDEX ix_applications_system_owner_id ON applications (system_owner_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    CREATE UNIQUE INDEX ux_applications_code ON applications (code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    CREATE INDEX ix_audit_events_actor_id ON audit_events (actor_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    CREATE INDEX ix_audit_events_policy_version ON audit_events (policy_version);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    CREATE INDEX ix_audit_events_request_occurred ON audit_events (access_request_id, occurred_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    CREATE UNIQUE INDEX ux_audit_events_request_version ON audit_events (access_request_id, request_version);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    CREATE UNIQUE INDEX ux_policy_versions_single_active ON policy_versions (is_active) WHERE is_active;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    CREATE INDEX ix_users_manager_id ON users (manager_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    CREATE UNIQUE INDEX ux_users_email ON users (email);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015514_Initial') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261008015514_Initial', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015527_AddDatabaseGuards') THEN

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

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015527_AddDatabaseGuards') THEN

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

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015527_AddDatabaseGuards') THEN

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

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261008015527_AddDatabaseGuards') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261008015527_AddDatabaseGuards', '8.0.11');
    END IF;
END $EF$;
COMMIT;

