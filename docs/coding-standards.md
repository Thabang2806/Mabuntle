# Swyftly Coding Standards

## Backend

- Use nullable reference types.
- Keep domain code free of infrastructure dependencies.
- Prefer vertical slices for feature work.
- Use EF Core directly for simple persistence.
- Use specific repositories only for complex aggregate workflows.
- Add focused tests for business rules and state transitions.

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

## Testing

- Backend: `dotnet test backend\Swyftly.sln`.
- Frontend: `cmd /c npm test` from `frontend\swyftly-web`.
- Build both backend and frontend before handing off a scaffold or feature branch.
