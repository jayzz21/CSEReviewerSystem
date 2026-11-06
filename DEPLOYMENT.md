# Free hosting with Render and Supabase

Render hosts the .NET app in Docker and gives it an HTTPS `onrender.com` URL. Supabase remains the PostgreSQL database. Choose the Free plan for both services. No Google or email provider setup is needed for username/password login.

## Before uploading

1. Run `supabase-accounts-setup.sql` in your Supabase SQL Editor if you have not already done so.
2. Start the app locally against that same database, sign in as `sa`, and replace the initial password. The deployed app will reuse that account and password.
3. Keep database passwords and API keys in .NET user secrets locally and Render environment variables in hosting. Do not commit credentials. Check existing Git history too if credentials were committed previously; replacing a file does not remove them from history.
4. Upload the project to a private GitHub repository. Include `Dockerfile`, `.dockerignore`, `render.yaml`, `deploy/appsettings.json`, `package-lock.json`, and the application source.

## Deploy

Docker Desktop is not required on your PC for Render deployment. Render uses the included Dockerfile to build the image on its own servers.

To check the files and Linux publish locally:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/check-deployment.ps1
```

This creates ignored build output under `artifacts/deploy-check`, without starting a server or connecting to your database.

1. Sign in to Render and select **New → Web Service**, then connect your repository.
2. Select **Docker**, with Dockerfile path `./Dockerfile` and repository root as the build context.
3. Select the **Free** instance. Do not create a Render database: use your existing Supabase project.
4. Add the secret environment variable `ConnectionStrings__Study` using this format (replace placeholders):

   ```text
   Host=YOUR_SESSION_POOLER_HOST;Port=5432;Database=postgres;Username=postgres.YOUR_PROJECT_REF;Password=YOUR_DATABASE_PASSWORD;SSL Mode=Require
   ```

   Copy the host and username from Supabase's **Connect → Session pooler**. Use the database password, not the publishable API key. If the password contains connection-string delimiters, use Npgsql connection-string quoting.

5. Add `ASPNETCORE_ENVIRONMENT=Production` and `ASPNETCORE_HTTP_PORTS=8080`.
6. Click **Deploy Web Service**. After the build and database startup succeed, open the URL shown by Render, such as `https://husay-reviewer.onrender.com` (subject to name availability).

Alternatively create a **Blueprint** from the included `render.yaml` and supply the database connection when prompted.

The Docker build compiles Tailwind and publishes .NET. Local appsettings files are excluded from the image; configuration comes from Render. Data and login encryption keys live in Supabase, so they survive app restarts. The current release offers database questions; AI generation deliberately returns `ai_not_available`.

## Free-plan limits

- Render free web services sleep after 15 minutes without traffic; the next visit can take time to wake them.
- Supabase Free includes 500 MB of database storage and may pause after a week of low activity.
- The provided `onrender.com` address is free. A separately registered domain such as `yourreviewer.com` generally costs money.

Official references: https://render.com/docs/free, https://render.com/docs/web-services, https://supabase.com/docs/guides/platform/free-project-pausing

The files prepare deployment; creating the hosting service and obtaining the live URL still require your Render/GitHub accounts. The Docker image must be built by Render or a local Docker installation before deployment is verified.
