# Username accounts and administration

Run `dotnet watch --project CseReviewer run --launch-profile http` and open http://localhost:5213/. Signed-out visitors are redirected to the login page.

## Registration and login

Accounts use a unique username and password only. No email, Google provider, verification email, or Supabase Auth API key is required. Supabase PostgreSQL remains the database.

Usernames are case-insensitive, 3–30 letters, numbers or underscores. New passwords must be 12–128 characters. The server uses ASP.NET Core PasswordHasher to store salted password hashes in AccountProfiles.PasswordHash. It never stores plaintext passwords. New accounts always receive the User role, regardless of client-supplied fields.

## Roles

- SuperAdmin: assigns roles and resets other users' passwords, adds/imports questions, and studies.
- Admin: adds/imports questions and studies; cannot assign roles or access another user's sets.
- User: studies with private sets and saved progress. Import controls are hidden and server endpoints deny access.

Initial Super Admin: `sa` / `123`. The first login requires changing the password. Startup never resets an existing password. Complete this setup before publishing the website.

Open Administration to assign Admin or User roles. Roles are loaded on every request. To help someone who forgot their password or has an old email-based account, set a temporary password in that account's row. The user must replace it at next login. Resetting preserves their account ID, roles, sets, and progress. Existing email-account passwords cannot be copied from Supabase Auth; they must be reset here. Old email values may remain for existing profiles but are not used for login.

## Database

Run `supabase-accounts-setup.sql` in Supabase SQL Editor if the tables are not present, then restart the application. No additional schema changes are required for the switch to username-only accounts.

- AccountProfiles: account IDs, usernames, roles, password hashes, forced password-change flag.
- Sets: each user's question snapshots and saved progress, scoped by account ID.
- BankQuestions: shared question bank.
- DataProtectionKeys: persistent server cookie-encryption keys.

Sessions last eight hours. New accounts are stored in public.AccountProfiles, not auth.users. Previous Supabase Auth records are left untouched. Application tables have RLS enabled without client policies; the .NET server accesses them using its backend database connection. The browser accesses the .NET API, not database tables directly.

For hosting configure `ConnectionStrings__Study` with your backend PostgreSQL connection. Supabase publishable-key and Google settings are no longer used by login.

## Verification

```powershell
 dotnet build CseReviewer -p:OutputPath=bin/AuthCheck/
 dotnet run --project CseReviewer.AuthChecks -p:OutputPath=bin/AuthCheck/
```

Checks run against a uniquely named temporary SQL LocalDB database and server on port 5096. They check redirects, password setup, registration, duplicate usernames, login, role restrictions, private progress, and logout. The temporary server and database are cleaned up afterward. They do not modify the live Supabase database.
