# Husay — Philippine CSE Reviewer

ASP.NET Core 9 + Tailwind CSS 4 + Entity Framework Core + Microsoft SQL Server. A local-first reviewer with topic-based AI generation via Ollama, saved question sets, instant feedback, and worked numerical solutions.

## Run

Requirements: .NET 9 SDK, Node.js/npm (only to rebuild CSS), and SQL Server LocalDB or SQL Server Express/Developer.

```powershell
npm ci
npm run css
dotnet run --project CseReviewer --urls http://localhost:5080
```

Open http://localhost:5080. Sample mode works without AI and provides five introductory questions per subject. Numerical samples use a percentage template; other subjects have five fixed samples. Custom focus and difficulty apply only to AI mode.

## SQL Server / SSMS

### If startup says “address already in use”

Only one instance can use a given port. Stop the previous reviewer using Ctrl+C in its terminal or Stop Debugging in Visual Studio before starting again. The default launch profile uses `http://localhost:5213`; the explicit command above uses port 5080. An old running instance can also lock build output files. Stop it before rebuilding. From the solution folder, start the website with `dotnet run --project CseReviewer` (the other project is a console test runner).

SSMS is the management application; SQL Server stores the data. The default connection creates a `CseReviewer` database on `(localdb)\MSSQLLocalDB` at first startup. In SSMS connect to that server using Windows Authentication and inspect `CseReviewer > Tables > dbo.Sets`.

For SQL Server Express, change `ConnectionStrings:Study` in `CseReviewer/appsettings.json`, for example `Server=.\SQLEXPRESS;Database=CseReviewer;Trusted_Connection=True;TrustServerCertificate=True`. Never commit database passwords. Use environment variable `ConnectionStrings__Study` or .NET user secrets for credentials. `EnsureCreated` is used for this initial project; introduce EF migrations before evolving a populated schema.

## AI setup

Install Ollama from https://ollama.com and run:

```powershell
ollama pull llama3.2
ollama serve
```

If Ollama is already running, a second `serve` is unnecessary. Select **AI · Custom questions** in the app. Configure `AI:Model` and `AI:Endpoint` in appsettings or `AI__Model` / `AI__Endpoint` environment variables. The .NET backend calls Ollama, validates the batch, stores it in SQL Server, and returns question text without answer keys. Failed generation displays an error, never silently substitutes samples. The request can take up to five minutes; keep the page open. This is request-driven asynchronous generation, not a durable background queue.

## Behavior and scope

- Professional: numerical, verbal, analytical, general information.
- Subprofessional: numerical, verbal, clerical, general information.
- Choose 5–20 AI questions, difficulty, and a free-text focus.
- Submit an answer to reveal the correct option, explanation, and numerical steps. Review all explanations at the end or retry the set.
- Save and rename sets for later practice. A signed, HttpOnly cookie identifies this browser's study library. This is not account login or cross-device sync. Clearing cookies loses access to that profile; database records remain. Quiz progress and scores are session-only, while saved question sets persist.
- AI outputs receive structural validation, not independent mathematical/factual proof. Review doubtful answers. Original practice material is not an official CSC question bank.
- This local application is bound to localhost. Internet deployment requires real account authentication, quotas/rate limits, HTTPS, operational monitoring, and a deployment database strategy.

## API contract

All POST requests require `Content-Type: application/json`. First `POST /api/session` with `{}` to establish a signed study cookie. Other set endpoints require that cookie and enforce ownership.

| Method | Path | Body / result |
|---|---|---|
| GET | `/api/config` | Categories and configured AI model |
| POST | `/api/sets` | `{category,level,difficulty,count,focus,mode}` → `{id}`; mode `sample` or `ai` |
| GET | `/api/sets` | Saved sets for current browser profile |
| GET | `/api/sets/{id}` | Set metadata and questions, excluding answer keys |
| POST | `/api/sets/{id}/answer` | `{index,choice}` (zero-based) → `{correct,answer,explanation,steps}` |
| POST | `/api/sets/{id}/save` | `{title}` → 200; saves/renames |

Validation errors return 400, unauthenticated calls 401, inaccessible sets 404, and non-JSON POSTs 415. AI failures include an `error` message and machine-readable `code`: unavailable server/model returns 503, invalid output returns 502, and generation timeout returns 504. Unsaved generated sets also remain in the database but do not appear in the library.

### Troubleshooting AI generation

A valid `mode: "ai"` request still needs the separate Ollama service. The website port (such as 5080 or 5213) is different from Ollama's default port, 11434. Install Ollama, run `ollama pull llama3.2`, then start `ollama serve` if it is not already running. Check `http://localhost:11434/api/tags` for installed models. The configured model name must match an installed model. Start with 5 questions before trying a 20-question set. Restart the .NET app after rebuilding changes.

## Verification

Run `dotnet run --project CseReviewer.Checks` for AI adapter checks with a simulated provider, including malformed output, missing steps, duplicate choices, and unavailable models. These do not claim live AI verification.

With the app running, execute `powershell -File scripts/smoke.ps1`. It tests database-backed generation, private ownership, answer-key hiding, explanations, save/reload, and invalid requests. Test records are labeled clearly. If local PowerShell policy prevents script files, run the reviewed contents with `& ([scriptblock]::Create((Get-Content scripts/smoke.ps1 -Raw)))` without changing system policy.

## References

- CSC scope: https://www.csc.gov.ph/more-than-400k-to-take-26-march-career-service-exam
- Tailwind build: https://tailwindcss.com/docs/installation/tailwind-cli
- Ollama JSON generation: https://github.com/ollama/ollama/blob/main/docs/api.md
