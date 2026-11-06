# Import HTML questions into local SQL Server

1. Run `dotnet watch --project CseReviewer run --launch-profile http` from the solution directory.
2. Open http://localhost:5213 in your normal study browser.
3. In **Add questions from an HTML file**, drop the file or use the file picker.
4. Review the counts and expand **Review excluded questions** if shown.
5. Click **Import valid questions & create a saved set**. Accepted questions are stored in `CseReviewer.dbo.BankQuestions`. An automatically saved, randomized set of up to 20 questions from that file appears immediately. This initial set can mix subjects and exam levels; it is labeled Mixed practice, not a mock Professional exam.
6. For later practice select **My imported database questions**, then subject, exam level, difficulty, and count. Matching questions and choices are randomized without AI. Every generated database set is saved in `dbo.Sets` as a snapshot so later imports do not alter it.

No cloud database or AI requests are used for import/database practice. Records belong to the existing browser profile, not a cross-device login. Use the same browser; clearing its cookies loses access to that profile.

## Supported formats

The exact supplied `CSE_2027_Reviewer.html` generator is supported. It produces 540 items, of which 241 are valid unique questions, 294 are repeats, and 5 have duplicate choices and are excluded. Legacy metadata defaults to Easy; shared subjects use Both levels. Numerical explanations are preserved as a solution entry rather than inventing additional solving steps. The import does not verify factual correctness or independently recalculate answers.

For all future files, copy `CseReviewer/wwwroot/import-template.html` (also downloadable in the app). Open it in a text editor and add objects to the JSON `questions` array. The parser reads only:

```html
<script id="cse-question-bank" type="application/json">
{"version":1,"title":"My question bank","questions":[...]}
</script>
```

Each question requires `category`, `level`, `difficulty`, `prompt`, `options` (exactly four distinct plain strings), `answer` (integer index 0–3), `explanation`, and `steps` (string array, nonempty for numerical questions). Valid categories are the five subjects shown in the app. Levels: Professional, Subprofessional, Both. Analytical must be Professional, Clerical must be Subprofessional. Difficulty: Easy, Moderate, Hard. Escape quotation marks and newlines using normal JSON rules; encode any literal closing script sequence as `<\/script>` inside JSON text.

Limit: 2 MB, 1–2,000 questions. An invalid document is rejected entirely. Individual malformed questions are listed in the preview and excluded when you choose to import valid entries. Repeated questions with the same category, level, difficulty, text, choices (regardless of order), and correct answer are skipped. Importing a repeated file creates another randomized saved set but does not duplicate its questions. Re-import does not overwrite existing explanations. Unknown HTML layouts are rejected with a template instruction.

Uploaded markup is never rendered. Arbitrary JavaScript is never executed. Only the exact known legacy script (SHA-256 allowlist) runs in Jint with no host APIs, limited statements, memory, and runtime. Its rendering code is not executed. Modified legacy generators must be converted to the JSON template.

## Storage and API

Startup adds `dbo.BankQuestions` if missing, without altering or deleting `dbo.Sets`. New database creation includes both tables. Imports use a transaction and database application lock; a unique owner/fingerprint index also prevents duplicate records. Answer keys remain server-side until a quiz answer is submitted.

- `POST /api/imports/preview`: `{fileName,html}`; validation summary only, no writes.
- `POST /api/imports`: same payload; adds valid records and atomically creates a saved set; returns `{id,added,skipped,duplicates,count}`.
- `GET /api/bank`: current profile's counts by category/level/difficulty.
- `POST /api/bank/sets`: `{category,level,difficulty,count}`; returns `{id}`. Insufficient matches return 400; no silent repetition or AI fallback.

All require the signed session cookie established by `POST /api/session`.

Run the integration check with your local app running:

```powershell
& ([scriptblock]::Create((Get-Content scripts/import-smoke.ps1 -Raw))) -Base http://localhost:5213 -ReviewerFile 'C:/Users/combe/Desktop/Notepad/CSE_2027_Reviewer.html'
```

The check uses isolated test browser profiles and inserts test records. It verifies import counts, re-import deduplication, automatic saves, answer-key hiding, shuffled answer alignment, owner isolation, unsupported script rejection and insufficient-bank errors.
