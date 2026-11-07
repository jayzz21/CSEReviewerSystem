# Multiplayer lobbies

Open **Multiplayer lobbies** from the reviewer sidebar, or visit `/?view=multiplayer`. Only the main content changes; the sidebar, header, account controls, and dark/light theme remain in place. Existing `/multiplayer.html?code=...` links redirect into this layout.

## Playing

1. Sign in and create a lobby. Choose Professional or Subprofessional, one category or all four subjects, 30–180 questions, game mode, and 1–5 minutes.
2. Copy the link or share the ten-character lobby code. Guests sign in with their own accounts. A shared link is preserved through the login page.
3. All players select Ready. The host can start with 2–20 players; joining is closed after start, but existing players can reconnect.
4. Play and view the final leaderboard and your answer review, including explanations and numerical solving steps.

**Timed test:** one shared 1–5 minute deadline for the entire test. Players answer sequentially at their own pace. Most correct answers wins; equal top scores draw. If everyone has zero points there is no winner. Scores and correct answers are hidden until the match ends. The match also ends early if everyone finishes.

**Speed rounds:** one shared 1–5 minute deadline per question. The first correct answer accepted by the server wins a point and closes the round. A wrong answer eliminates that player only for the current round. If everyone is wrong, or time expires, nobody wins the round. There are five seconds to review the correct answer and solving method before the next round starts automatically. Most round points wins the match; equal top scores draw. Network travel time affects which answer reaches the server first.

Questions and choices are shuffled once per match and shared identically with everyone. Questions come from the database, with duplicate prompts removed within each category. Full exams distribute the question count evenly among four appropriate subjects (any remainder is distributed one at a time). Difficulty is mixed. If any category lacks its quota of unique questions, creation returns a clear message rather than silently reducing the test size.

## Reconnection and hosting

Keep the lobby link/code to reopen a match. Questions, player answers, scores, and round outcomes are stored in the `Lobbies` table. Disconnecting does not pause the server deadline. The host may cancel a match, but guests cannot. Players can leave a waiting lobby; leaving as host closes it. Waiting lobbies expire after 24 hours. There is no automatic host transfer.

Clients poll the server every 1.5 seconds while a lobby is active. Timers display locally using server time; the server validates all submissions and computes winners. Database row locks serialize simultaneous answers, including across app processes. A sleeping/restarting host does not reset the stored deadlines: the next request advances expired rounds. This is intended for small friend groups, not large tournaments.

## Deploying this update

1. Run **`supabase-multiplayer-setup.sql`** in Supabase SQL Editor. It adds the `Lobbies` table and enables RLS without modifying existing accounts/questions. The app also performs the same additive setup at startup.
2. Commit/push all multiplayer source changes, the SQL file, and rebuilt `app.css` to the repository connected to Render.
3. Deploy the latest commit in Render. The lobby page is then at `https://csereviewersystem.onrender.com/multiplayer.html`.
4. Test with two separate accounts/devices. Tabs sharing the same browser profile use the same signed-in account and count as one player.

No new API key, Supabase Realtime subscription, paid hosting plan, or AI service is required. The existing backend database connection must have permissions to create/access the Lobbies table. Browser users access it through the .NET API only.

## Verification

`dotnet run --project CseReviewer.AuthChecks -p:OutputPath=bin/AuthCheck/` runs isolated SQL LocalDB integration checks for lobby validation, four-category coverage, membership, readiness, host permissions, hidden answer keys, duplicate/late answers, reconnect state, score calculation, simultaneous winners, wrong-answer elimination, and timer advancement. These checks do not modify the live Supabase database. Live two-device verification remains necessary after deployment; PostgreSQL production behavior is not covered by the LocalDB run.
