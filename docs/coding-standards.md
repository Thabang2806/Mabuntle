# Swyftly Coding Standards

## Backend

- Use nullable reference types.
- Keep domain code free of infrastructure dependencies.
- Prefer vertical slices for feature work.
- Use EF Core directly for simple persistence.
- Use specific repositories only for complex aggregate workflows.
- Add focused tests for business rules and state transitions.
- Use `Result`/`Result<T>` for expected application failures.
- Map API failures to ProblemDetails consistently.
- Keep validation lightweight until real request models justify a package.
- Use `AuditableEntity` only for persisted entities that need timestamps.
- Use `ISoftDelete` only for entities that explicitly need soft-delete behavior.
- Use the shared audit logging service for sensitive admin actions instead of creating `AuditLog` records inline.

## Frontend

- Use Angular standalone components.
- Use Angular Material for standard controls.
- Use Tailwind for layout utilities and spacing.
- Keep public marketplace pages SSR friendly.
- Keep provider calls behind backend APIs.

## Security

- No secrets in source code.
- No `.env` commits.
- Validate seller ownership and admin authorization.
- Audit sensitive admin, payment, payout, and moderation actions.
- Keep uploaded buyer images transient unless a documented retention policy and storage control exist.
- Rate-limit anonymous write endpoints and money-moving endpoints.
- Treat payment, refund, webhook, and ledger workflows as idempotent by design.

## Testing

- Backend: `dotnet test backend\Swyftly.sln`.
- Frontend: `cmd /c npm test` from `frontend\swyftly-web`.
- Build both backend and frontend before handing off a scaffold or feature branch.
