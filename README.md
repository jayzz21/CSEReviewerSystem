# Husay — Philippine CSE Reviewer

**HTML import and offline database practice:** see [IMPORTING.md](IMPORTING.md). Drop your reviewer into the app to preview, import, and automatically save a randomized practice set. Future files can use the downloadable HTML/JSON template.

ASP.NET Core 9 + Blazor Server + MudBlazor 8 + Entity Framework Core + Microsoft SQL Server. A reviewer with topic-based AI generation via Groq (cloud) or Ollama (local), saved question sets, instant feedback, and worked numerical solutions.

## Run

Requirements: .NET 9 SDK and SQL Server LocalDB or SQL Server Express/Developer.

```powershell
dotnet run --project CseReviewer --urls http://localhost:5080
```

Open http://localhost:5080. Sample mode works without AI and provides five introductory questions per subject. Numerical samples use a percentage template; other subjects have five fixed samples. Custom focus and difficulty apply only to AI mode.

## SQL Server / SSMS

### If startup says “address already in use”

Only one instance can use a given port. Stop the previous reviewer using Ctrl+C in its terminal or Stop Debugging in Visual Studio before starting again. The default launch profile uses `http://localhost:5213`; the explicit command above uses port 5080. An old running instance can also lock build output files. Stop it before rebuilding. From the solution folder, start the website with `dotnet run --project CseReviewer` (the other project is a console test runner).

SSMS is the management application; SQL Server stores the data. The default connection creates a `CseReviewer` database on `(localdb)\MSSQLLocalDB` at first startup. In SSMS connect to that server using Windows Authentication and inspect `CseReviewer > Tables > dbo.Sets`.

For SQL Server Express, change `ConnectionStrings:Study` in `CseReviewer/appsettings.json`, for example `Server=.\SQLEXPRESS;Database=CseReviewer;Trusted_Connection=True;TrustServerCertificate=True`. Never commit database passwords. Use environment variable `ConnectionStrings__Study` or .NET user secrets for credentials. `EnsureCreated` is used for this initial project; introduce EF migrations before evolving a populated schema.

## AI setup

Groq is the default. No Ollama installation or OpenAI subscription is needed. Create a Groq API key at https://console.groq.com/keys, then configure it **locally**, not in chat or frontend files:

```powershell
dotnet user-secrets set "AI:Groq:ApiKey" "YOUR_GROQ_KEY" --project CseReviewer
dotnet run --project CseReviewer
```

Replace the placeholder on your own computer. User secrets are development settings stored outside the repository (not an encrypted vault). For deployment, use the hosting secret store or environment variable `GROQ_API_KEY` / `AI__Groq__ApiKey`. Never commit a real key. Restart the app after setting it.

Select **AI · Custom questions**. The default model is `openai/gpt-oss-20b`, hosted by Groq: it uses your Groq account, not OpenAI billing. To use another Groq text model with JSON Object Mode, set `AI:Groq:Model` to its current model ID from your Groq console. Availability and free-tier quotas can change. The backend sends topic/settings to Groq; it does not send your saved library. There is no automatic switch to another provider or paid plan.

The app handles missing keys, rejected credentials, rate limits, invalid output, and timeouts separately. Start with 5 questions; a 20-question set consumes more output tokens and may hit free-tier limits. Returned JSON is validated for count, choices, answer index, explanations, and numerical steps before saving. This does not independently verify factual or mathematical correctness.

### Optional local alternative: Ollama

Set `AI:Provider` to `Ollama` in appsettings. Install Ollama from https://ollama.com and run:

```powershell
ollama pull llama3.2
ollama serve
```

If Ollama is already running, a second `serve` is unnecessary. Configure `AI:Ollama:Model` and `AI:Ollama:Endpoint` in appsettings. These replace the original `AI:Model` and `AI:Endpoint` settings. Both providers keep answer keys on the server until submission. Failed generation never silently substitutes samples. Requests can take up to five minutes; keep the page open. This is request-driven asynchronous generation, not a durable background queue.

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

For Groq, `ai_not_configured` means the server needs a key; `ai_auth_failed` means credentials or access were rejected; `ai_rate_limited` (HTTP 429) means wait for the provider's quota to reset. A configured key is not proof of valid credentials or available quota. For Ollama only, the website port (5080 or 5213) differs from its port 11434. Check `http://localhost:11434/api/tags` and your configured local model. Restart .NET after configuration changes.

## Verification

Run `dotnet run --project CseReviewer.Checks` for AI adapter checks with a simulated provider, including malformed output, missing steps, duplicate choices, and unavailable models. These do not claim live AI verification.

With the app running, execute `powershell -File scripts/smoke.ps1`. It tests database-backed generation, private ownership, answer-key hiding, explanations, save/reload, and invalid requests. Test records are labeled clearly. If local PowerShell policy prevents script files, run the reviewed contents with `& ([scriptblock]::Create((Get-Content scripts/smoke.ps1 -Raw)))` without changing system policy.

## References

- CSC scope: https://www.csc.gov.ph/more-than-400k-to-take-26-march-career-service-exam
- MudBlazor: https://mudblazor.com/getting-started/installation
- Ollama JSON generation: https://github.com/ollama/ollama/blob/main/docs/api.md
- Groq JSON output: https://console.groq.com/docs/structured-outputs
- Groq limits: https://console.groq.com/docs/rate-limits
