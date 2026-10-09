BEGIN;

CREATE TABLE IF NOT EXISTS "SystemAccount" (
    "AccountID" SERIAL PRIMARY KEY,
    "FullName" VARCHAR(100) NOT NULL,
    "Email" VARCHAR(254) NOT NULL,
    "PasswordHash" TEXT NOT NULL,
    "Role" SMALLINT NOT NULL DEFAULT 0 CHECK ("Role" IN (0, 1)),
    "CreatedDate" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE UNIQUE INDEX IF NOT EXISTS "UX_SystemAccount_Email" ON "SystemAccount" (LOWER(BTRIM("Email")));
CREATE UNIQUE INDEX IF NOT EXISTS "UX_Tag_Name_Normalized" ON "Tag" (LOWER(BTRIM("TagName")));

-- Legacy tasks have no recorded creator; keep them unassigned.
ALTER TABLE "Task" ADD COLUMN IF NOT EXISTS "CreatedByID" INT NULL;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Task_CreatedBy') THEN
        ALTER TABLE "Task" ADD CONSTRAINT "FK_Task_CreatedBy" FOREIGN KEY ("CreatedByID") REFERENCES "SystemAccount" ("AccountID") ON DELETE RESTRICT;
    END IF;
END $$;
CREATE INDEX IF NOT EXISTS "IX_Task_CreatedByID" ON "Task" ("CreatedByID");

-- AS1 writes UTC; match EF's timestamptz mapping without changing the stored clock values.
DO $$ DECLARE target RECORD; BEGIN
    FOR target IN SELECT table_name, column_name FROM information_schema.columns
        WHERE table_schema = current_schema() AND data_type = 'timestamp without time zone'
        AND ((table_name = 'Task' AND column_name IN ('CreatedDate', 'ModifiedDate')) OR (table_name = 'Project' AND column_name = 'CreatedDate'))
    LOOP
        EXECUTE format('ALTER TABLE %I ALTER COLUMN %I TYPE timestamp with time zone USING %I AT TIME ZONE ''UTC''', target.table_name, target.column_name, target.column_name);
    END LOOP;
END $$;
COMMIT;
