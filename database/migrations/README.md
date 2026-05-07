# database/migrations

Numbered SQL migrations for the OnPoint AI Postgres schema.

## Naming convention

`NNNN_description.sql` where `NNNN` is a zero-padded sequence number.

- `0001_initial.sql` — initial schema (all core tables + RLS policies)
- `0002_<change>.sql` — next change
- `0003_<change>.sql` — etc.

## Rules (per CLAUDE.md §Migration rules)

1. **Forward-only.** Once a migration has been applied to a shared environment (dev / staging / prod), it is **never edited**. Write a new migration instead.
2. **Two-deploy pattern for destructive changes.** Stop reading from a column → deploy → drop the column in a *later* migration. Never drop and stop-reading in the same change.
3. **Every new tenant-scoped table must include its RLS policy** in the same migration that creates it.
4. **Every foreign key must have an index.** Every column used in a `WHERE` clause must have an index.
5. **All `business_id` columns** use the standard tenant policy:
   ```sql
   CREATE POLICY tenant_isolation ON {table}
     USING (is_platform_admin() OR business_id = current_business_id())
     WITH CHECK (is_platform_admin() OR business_id = current_business_id());
   ```

## Applying migrations in dev

```powershell
docker exec -i onpoint-db psql -U onpoint -d onpoint < database/migrations/0001_initial.sql
```

In CI / staging / prod: migrations run as a deploy step before the backend rolls out (see CLAUDE.md §Deployment Pipeline).
