# Dashboard 2.0 — Decision-Complete Specification

**Status**: assembled 2026-09-15 from the [Wayfinder Map: Dashboard 2.0 Spec](https://github.com/karldoyledev/BlackoutRugbyAnalysis/issues/4) — decisions 1–29, tickets [#5](https://github.com/karldoyledev/BlackoutRugbyAnalysis/issues/5)–[#19](https://github.com/karldoyledev/BlackoutRugbyAnalysis/issues/19), research findings in `docs/research/`. Every open question the map surfaced is settled here; the builder should never have to re-decide. Vocabulary follows `Context.md` (the glossary of record).

## Problem Statement

The existing dashboard grew endpoint-first: an API tester, a time-keyed squad comparison, and fixtures data that is read live and never retained. The game's API can silently stop serving historical data (the undocumented `data_removed` flag proves removal happens), there is no concept of a signed-in club, and post-match analysis — the original reason this repo exists — is scattered across raw XML dumps.

## Solution

A re-architected Razor Pages web app organized around a signed-in **User** with a live-validated **Club Link** and a local, append-only **Match Cache**. Every viewed completed **Fixture** persists permanently (raw XML + parsed SQLite rows), so analysis pages render from the cache and survive upstream data removal. Four analysis surfaces replace the old pages: **Home** (last-8 form), **Match Analysis** (tactics panel, team compare row, 7 Stat-Group tabs, deterministic Recommendations), **Player History** (per-Player trends with sparklines), and the existing Squad comparison, all under one dark "command centre" theme. The API Playground stays for raw endpoint exploration.

## User Stories

1. As an Anonymous visitor, I want to sign up with just email + password, so that I have a local dashboard account without needing my game credentials first.
2. As a new User, I want a "Link your club" step after sign-up that live-validates my Member ID + Member Key before saving, so that I know the connection works.
3. As a new User, I want to skip club linking, so that I can explore the dashboard before fetching my Member Key.
4. As an Unlinked user, I want Home to show a "Link your club" prompt card, so that I know exactly how to finish setup.
5. As a User, I want to log in with only email + password, so that my Member Key is used and stored only inside the app.
6. As a User, I want "remember me" to keep me signed in for 14 days, so that I am not re-authenticating every visit.
7. As a User whose in-game password change broke my Member Key, I want a clear hard-error panel on API-backed pages with one button to Settings, so that I can re-link immediately.
8. As a User, I want to re-link my club from Settings, so that a rotated Member Key never locks me out of my own data.
9. As a User, I want to unlink my club from Settings, so that I can bind a different Member — keeping my account and my entire Match Cache.
10. As an Operator, I want a relaxed password policy with lockout and no email confirmation, so that a single-operator local tool stays simple.
11. As a Linked user, I want Home to list my last 8 Fixtures newest-first across all competitions, so that recent form is one glance away.
12. As a Linked user, I want each row to show a W/L badge and the score from my club's perspective, so that results read instantly regardless of home/away.
13. As a Linked user, I want each row to show the opponent's name and a CSR chip, so that I can gauge opposition strength.
14. As a Linked user, I want a "bot" chip on rows against game-managed Teams, so that I know why that CSR number looks tiny.
15. As a Linked user, I want attendance as a total with the 5-tier breakdown on hover, so that the row stays compact but the detail is there.
16. As a Linked user, I want rows from a previous season to carry a season label, so that the cross-season window is never misleading.
17. As a Linked user, I want every Home row to link into Match Analysis, so that form turns into analysis in one click.
18. As a Linked user, I want a fully-cached Home window to cost exactly one API call, so that the app respects the game's rate limits.
19. As a Linked user, I want missing opponent/summary data to render as "—" and retry on my next visit, so that partial data never breaks the page.
20. As a Linked user, I want unplayed Fixtures excluded from Home, so that it shows only results.
21. As a Linked user, I want the Match masthead to show both Team names, the full-time score tinted from my club's perspective, and the match metadata, so that context sits above everything.
22. As a Linked user, I want a tactics panel with the XV, Bench, captain and kicker badges and the intensity chip, so that I see the selection actually used — read-only, because the API cannot read strategy values.
23. As a Linked user, I want a team-vs-opponent compare row of 12 fixed stat pairs, so that the shape of the match is visible at a glance.
24. As a Linked user, I want per-player stats in one sortable table under 7 Stat-Group tabs, so that I can drill into exactly the battle I care about.
25. As a Linked user, I want the player table to default to Attack and slot order 1–23, so that it matches the team sheet above it.
26. As a Linked user, I want a Match notes card with scorers, injuries and subs, so that match events survive even if upstream data is later removed.
27. As a Linked user, I want a Recommendations button that produces severity-ranked, evidence-backed Tactic Slider advice, so that post-match analysis turns into concrete adjustments.
28. As a Linked user, I want the Recommendations card to say "No tactical flags — every comparison within thresholds" when nothing trips, so that silence is meaningful.
29. As a Linked user, I want deep-linked unplayed Fixtures to show a "not played" notice, so that the page never pretends to analyse a match that hasn't happened.
30. As a Linked user, I want foreign Fixtures to render masthead, compare row and notes but suppress our club's team sheet and player tabs, so that the page degrades honestly.
31. As a Linked user, I want Player History trends built only from my cached Fixtures, so that my data can never be lost to upstream removal.
32. As a Linked user, I want a per-field sparkline in each trends column header, so that a season's shape is visible before I read numbers.
33. As a Linked user, I want a season block with totals by Stat Group and reliable caps, so that cumulative performance sits next to the per-match trends.
34. As a Linked user, I want a slim /Players index listing my squad from the latest Squad Snapshot with zero API calls, so that I can reach any Player directly.
35. As a Linked user with no Squad Snapshot yet, I want a "Capture squad" action on /Players, so that the index can bootstrap itself with one call.
36. As a Linked user, I want transferred-out Players to remain fully trendable from the cache, so that history outlives the squad list.
37. As a Linked user, I want foreign or unknown Player ids to show an empty-trends page with zero API calls, so that bad links degrade cheaply.
38. As a Linked user, I want the Squad page at /Squad comparing Players over time via Squad Snapshots, so that squad development stays analysable.
39. As a User, I want the API Playground at /Playground, so that I can still probe raw endpoints as the API evolves.
40. As a User, I want Settings to show link status, re-link, unlink, the per-table Match Cache reset, and sign-out, so that every recovery hatch lives in one place.
41. As a User, I want one consistent dark command-centre theme across every page, so that the dashboard feels like one instrument.

## Implementation Decisions

### Foundation

- **Stack** (decisions 3, 4): ASP.NET Core 8 Razor Pages. One `DashboardDbContext` (EF Core + SQLite, file at `Data/dashboard.db`, migrations in the web project) hosts both the Identity sets and the Match Cache parsed rows. Raw cache XML lives at `Data/MatchCache/raw/`, one file per API call, named `<endpoint>-<fixtureids>-<capturedAt>.xml`; batch responses are archived whole — the batch is transport, the Fixture is the logical unit.
- **API client corrections** (decisions 16, 17; R1/R2/R5): `GetLineupsAsync` sends `lu` (not `li`); `GetMatchSummaryAsync` sends `ms` (not `msum`) with `fixtureids` batching; add `GetTeamAsync`/`GetTeamsAsync` (`r=t`, `teamid`/`teamids` batching) and a `GetMemberAsync` probe (`r=m`) for Club Link validation. Developer `d`/`dk` credentials are required on every read (R2 F2). Parser notes: dedupe the duplicated `<lineup>` element (R1 F4) and the duplicated `id` attribute+element on team reads (R5). On `No data requested` errors: transient global API state — pause and retry before diagnosing request shape (R5 F5).
- **Secrets & config** (decision 13; D3 §8): user-secrets hold `Developer:DeveloperKey`, `Developer:DeveloperIV`, and the demo Member credentials; `appsettings.json` keeps only `DashboardDefaults:BaseEndpoint`, `Developer:DeveloperId`, Logging, AllowedHosts. `DashboardDefaultsOptions` slims to `BaseEndpoint` only. Committed keys are mitigated by rotation (in-game password change / dev-key reset) — documented manual step.

### Accounts & Club Link (D3)

- Identity (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`) + `Microsoft.EntityFrameworkCore.Sqlite`. `DashboardUser : IdentityUser` carries `MemberId`, `EncryptedMemberKey` (ASP.NET Core Data Protection, decision 5), `TeamId`, `LinkedAt`. Unique index on `MemberId` — one Member per User.
- Password policy: 8+ alphanumeric; lockout on (5 failures → 5 minutes); no email confirmation; password reset is a manual operator action. Cookie: 14-day sliding persistent with remember-me, ~12 h otherwise.
- Two states only: **Unlinked → Linked**. A broken Member Key is a runtime condition on a Linked user (decision 2's hard-error panel + Settings hatch), never a persisted third state.
- Link-club validated on submit (never per-keystroke — rate limits): one `r=m` probe + developer credentials; success binds `MemberId` + `TeamId` from the response and encrypts the key at rest. Error copy pinned per failure class: wrong key / unknown Member ID / blank fields (client-side, no API call) / Member already linked / developer credentials missing / API unreachable.
- Settings = link status (Member ID, Team, linked-at) + re-link form + unlink (wipes member fields + key, keeps the account **and** the Match Cache) + the manual per-table Match Cache reset + sign-out (POST handler, antiforgery — no logout route).

### Routes, gates & navigation (D2, as amended by D7)

- Routes: `/` Home · `/Fixtures/{id:int}` Match Analysis · `/Players/{id:int}` Player History · `/Players` index · `/Squad` · `/Playground` · `/Account/{SignUp,Login,LinkClub,Settings}`. `/Error` stays (non-nav). No logout route. Collection URLs other than `/Players` 404; unknown ids → 404.
- Gates: default-deny `AuthorizeFolder("/")`; anonymous only Login + SignUp (authenticated users hitting them → `/`). Auth-only: `/`, `/Playground`, `/Account/*`. Link-required (enforced in code, not route config): `/Fixtures/{id:int}`, `/Players/{id:int}`, `/Players`, `/Squad` — Unlinked users → `/`; Linked users on `/Account/LinkClub` → Settings. No id-ownership checks in v1 — foreign ids degrade gracefully.
- Landing: anonymous `/` → Login; login honors local `returnUrl` else Home; sign-up → LinkClub; link save → Home.
- Nav is gate-driven: anonymous = brand + Log in/Sign up; Unlinked = Home · API Playground · Settings; Linked = Home · Squad · Player History · API Playground · Settings. Brand unchanged ("Blackout Rugby Team Hub"); sign-out is a Settings POST only.
- Legacy disposition (decision 12): Index and Privacy deleted; ApiTester → `/Playground`; MemberStatsComparison → `/Squad`. Clean cut, no redirect shims.

### Match Cache (D1)

- **Both layers** per cached Fixture: immutable raw XML (the data-removal hedge — a parser bug re-parses, never re-fetches) + parsed SQLite DTOs.
- **Entry scope** per completed viewed Fixture: `f` metadata extract (incl. `data_removed`), `ms` Match Summary, our-squad `fs` (one `teamplayersstats` call), bare-`fs` team + half-time stats (both sides), `lu` lineup. Excluded: opponent player-level `fs`. Unplayed Fixtures are never cached.
- **Parsed rows**: `Fixture` (all `f` metadata incl. `data_removed`, fetched-at) · `PlayerFixture` ((FixtureId, TeamId, PlayerId) + jersey + slot 1–23 + all 51 `fs` fields verbatim) · `TeamFixtureStat` ((FixtureId, side, half) × 58 bare-`fs` fields) · `MatchSummary` (points home/guest, intensity, weather, 5 attendance tiers, scorers as joinable rows, injuries/subs JSON) · `PlayerSeason` (`ps`, per Player+season) · `TeamFact` ((TeamId, captured-at) + name, `average_top15_csr`, `ranking_points`, `bot`, ranks, `leagueid` — captured when seen, e.g. on Home window refresh).
- **Retention**: append-only, no eviction (~55 KB raw + parsed per Fixture); completed Fixtures are immutable and never re-fetched. Manual per-table reset lives in Settings only.
- `SnapshotStore` is untouched — time-keyed Squad Snapshot data for the Squad page, a different identity/consumer/lifecycle from the Match Cache.

### Home page (D5; amends decision 7 — user-approved)

- **Window**: literal `r=f&teamid=<teamid>&last=8` — the API's Last-8 Window, cross-season, all competitions. Season-labeled rows where the season differs. (Decision 7's "current season" wording is superseded: the live window verifiably spans seasons.)
- **Rows = completed Fixtures only**, newest first; unplayed members of the schedule window are dropped silently.
- **Row**: date (BRT) · competition + round (+ season when ≠ current) · H/A marker · opponent (name, CSR chip = `average_top15_csr`, bot chip when `t.bot=1`) · score + W/L badge (user's perspective, home/away-aware) · attendance (total; 5-tier breakdown on hover) · link to `/Fixtures/{id}`. No gate-receipts column — `r=fi` returns zero transactions for this club in every probed period (R2), so the column and the "fi per round" call step are dropped (amends decision 7, user-approved).
- **Scores come from the Match Summary batch** (`ms` → `<home><points>`/`<guest><points>`) — `r=f` carries no score fields (verified against the live `r4-f-last8-refresh.xml` artifact).
- **Data-call plan** (cache-first per decision 11): every Home visit 1 × `r=f&last=8`; for each completed Fixture in the window not yet cached, batch `r=ms&fixtureids=…` and `r=t&teamids=…` (missing ids only) — a fully-cached window costs **1 call total**. TeamFact rows captured when seen.
- **Edge states**: bot opponents keyed off the team read's `bot` field (never `f.botmatch`, whose semantics are unknown — R5); missing `ms`/`t` data renders "—" and retries naturally next visit; defensive "stats unavailable" only if the Fixture record itself is unusable; no `data_removed` special-casing (R4: the flag gates nothing today).

### Match Analysis page (D6 + D4)

- **Skeleton** (top → bottom): Masthead → Tactics panel → Team compare row (+ Recommendations button) → Recommendations card (on demand) → Match notes card → Stat-Group tabs over the player table.
- **Masthead**: both Team names + full-time score, tinted win/loss from our club's perspective; sub-line: competition · season/round · date (BRT) · weather (incl. night flag) · attendance (total + 5 tiers on hover) · stadium. HT score renders as "HT x–y" only when derivable from cached half-1 TeamFixtureStat blocks (derivability pinned at build; never a hard dependency).
- **Tactics panel** (read-only — R1 proved strategy fields unreadable, decision 9): XV (slots 1–15, jersey + name) and Bench (slots 16–23, 8 spots) as two compact columns; captain and kicker badges; "Intensity: N" chip from the cached Match Summary.
- **Team compare row** (D4): 12 fixed pairs from cached full-time TeamFixtureStat rows (both sides): Possession · Territory · Tries · Tackles · Rucks won · Mauls won · Scrums won/lost · Lineouts won/lost + stolen/conceded · Phases · Minutes in 22 · Penalties · Turnovers. Possession/territory as computed share; raw units pinned at build (seconds suspected).
- **Player table** (D4): always-visible columns Name · Jersey · XV/Bench · Minutes · Points · Energy-after · **CSR from the latest Squad Snapshot** (zero extra calls; blank when the player is absent from it — e.g. a new signing). Default tab Attack; default sort slot order 1–23; column-click re-sorts. 23 rows — our squad only.
- **Field→tab map** (40 fields in the locked 7 tabs): **Attack (5)** — tries, try_assists, metres_gained, linebreaks, beaten_defenders · **Defence (4)** — tackles, missed_tackles, turnovers, intercepts · **Kicking (14)** — kicks, good_kicks, bad_kicks, kicking_metres, kicks_out_on_the_full, up_and_unders, good_up_and_unders, bad_up_and_unders, conversions, missed_conversions, penalties, missed_penalties, dropgoals, missed_dropgoals · **Handling (3)** — knockons, handling_errors, forward_passes · **Discipline (4)** — penalties_conceded, yellow_cards, red_cards, fights · **Lineout (5)** — lineouts_secured, lineouts_conceded, lineouts_stolen, successful_lineout_throws, unsuccessful_lineout_throws · **Other (2)** — injuries, ball_time. Excluded: the 8 `fs` caps fields + `played` (unreliable — `ps` is the caps source, D4 §5); `energy_before` cached, never shown.
- **Match notes card**: scorers by type (player + count), injuries (player, minute, days injured, replaced-by), subs (player, minute, replaced-by) from the cached Match Summary.
- **Edge states**: unplayed (deep link only) → masthead from a live `f` read + "Not played yet — no analysis available", everything else suppressed, never cached; foreign Fixture → masthead + compare row + notes render, tactics panel suppressed, tabs replaced by "Full breakdown only for your club's Fixtures"; `data_removed` → defensive per-section empty-states, no flag UI.
- **First-view calls**: arriving from Home (`f` + `ms` + our-squad `fs` already cached) the page fetches only `lu` + bare-`fs` both sides — 2–3 calls; a cold deep link triggers the full Match Cache entry-scope fill.

### Recommendations (D8)

- Deterministic rules engine over the **full-time team compare row only** — no half-time, player-level, or Match-Summary inputs in v1 (decision 10: rules first, AI later).
- **8 rules**, Watch/Act thresholds in one code constants table (tweakable without touching rule structure; shares = your/(your+opp), differentials = your − theirs): possession share <45%/<35% → raise Driving, cut Expansive · territory share <45%/<35% → raise Kick for Touch + Kicking · lineouts lost−won ≥3/≥6 → raise Driving, cut Expansive · scrums lost on own ball ≥2/≥4 → cut Expansive · rucks theirs−yours ≥5/≥10 → raise Pick and Go + Driving, cut Expansive · turnovers conceded−won ≥3/≥6 → cut Creative + Expansive, raise Driving · penalties conceded−won ≥4/≥8 → ease off Discipline · tries conceded ≥3/≥5 → raise Defence. Mauls/phases/minutes-in-22 deliberately ruleless.
- **Advice vocabulary**: Tactic Slider name + direction only (Pick and Go, Driving, Expansive, Creative, Defence, Kicking, Kick for Touch, Up and Under, Drop Goals, Intensity, Discipline) — strategy values are unreadable (R1); `defence`'s text-vs-1–11 doc conflict pins at build.
- **Severity & ordering**: Watch (amber) / Act (red); Act items first, then Watch, compare-row order within each level.
- **Output card** (D6 placement): directly below the compare row, computed on click from the cache — no API calls, no refresh control. Item = severity chip + diagnosis headline + evidence line with real numbers + advice line ("Raise Driving · Cut Expansive"). Header notes the source ("from the full-time compare row"). Empty state: "No tactical flags — every comparison within thresholds." No item cap.
- **Gating**: the button renders only when the Fixture is completed, the user's Team is a side, and both full-time TeamFixtureStat rows are cached. Bot opponents: rules run identically (R5).

### Player History page + /Players index (D7; amends D2 — user-approved)

- **Strict cache-first**: per-Fixture trends render exclusively from Match Cache PlayerFixture rows for that PlayerId — **zero `fs` calls, ever**. `r=ps` fetched once per Player+season on first view (1 call) → cached PlayerSeason row. Trends span everything cached (append-only; older cached seasons appear, season-labeled; no season selector in v1).
- **Anatomy**: header (player name + CSR chip from the latest Squad Snapshot, blank when absent) → season block (totals by the same 7 Stat Groups + caps — the reliable caps surface — + `ps` averages where provided) → trends: the same 7 Stat-Group tabs, each a per-Fixture table (rows = cached Fixtures, newest first; context columns date · competition+round · opponent · result · slot/minutes; that tab's fields follow) with a **sparkline per field in the column header** (inline SVG over the table's own rows, one chronological series, no chart library). Default tab Attack; default sort newest first.
- **/Players index** (amends D2's "no collection pages" rule, user-approved): link-required; rows = latest Squad Snapshot's Name + CSR chip + link; **zero API calls** when a snapshot exists; empty-state "Capture squad" action performs the same squad read + capture the Squad page does (1 call). Inbound links: Match player-name cells (all 23 rows) + Squad rows.
- **Edge states**: foreign/unknown player id → generic header ("Player <id>"), empty trends, no season block, **zero API calls**; new signing → CSR chip + empty trends; transferred-out → renders fully from the cache, chip blanks once absent from the latest snapshot; `data_removed` fixtures → the same defensive per-section empty-state policy.

### Squad, Playground & theme

- **Squad** (decision 12): existing MemberStatsComparison moves to `/Squad` unchanged in behavior — Squad Snapshots over time; untouched `SnapshotStore`.
- **API Playground** (decision 12): existing ApiTester moves to `/Playground`, nav label "API Playground"; auth-only, not link-required (developer-credential reads work without member credentials).
- **Visual theme** (D10, user's call): Variant C "Command centre" — dark slate surfaces (page #0b1220, sidebar #101a2e, panels #121d33, hairlines #1f2c47; text #d7e0ee), accent cyan #4cc2ff, win #3ddc84 / loss #ff6b6b, BOT tag amber #d9b84a; fixed left sidebar rendering D2's gate-driven nav with club name + Team CSR + "Club link active" chip; widget panels with uppercase micro-titles; segmented control for the 7 Stat-Group tabs; pill tags for masthead metadata; split bars on percentage compare pairs; tabular numerals. Primary source: branch `prototype/dashboard-2-0-look`. Applies to all pages; content decisions are those above, nothing presumed.

## Testing Decisions

**What makes a good test here**: assert external behavior at stable seams — parsed outputs, rule outputs, cache fill decisions — never internal call ordering or Razor markup. The API itself is never called from tests.

**Seams** (proposed — confirm before build):

1. **Parser seam (existing)**: `BlackoutRugbyResponseAdapter` + the `TestXml` harness in `BlackoutRugbyDashboard.Tests` (prior art: `ParseFixturesTests`, `ParseTeamTests`, `ParsePlayerStatisticsTests`). Extend for the corrected `lu`/`ms` shapes, `r=t` team records (incl. `average_top15_csr`, `bot`, duplicated `id` dedupe), and the duplicated-`<lineup>` dedupe. Raw XML from `docs/research/artifacts/` makes excellent fixture data.
2. **Recommendation engine seam (new, pure)**: rules as a pure function from the 12 compare-pair values (plus the constants table) → ordered Recommendation items. Test all 8 rules' Watch/Act boundaries, severity ordering, and the empty state.
3. **Match Cache seam (new)**: cache-fill service against a seeded in-memory SQLite `DashboardDbContext` — verify append-only behavior, no re-fetch of completed Fixtures, batched missing-ids-only fills for the Home window, TeamFact capture-on-seen, and that unplayed Fixtures never enter the cache.
4. **Page-model seam (new, highest)**: an interface over `BlackoutRugbyApiClient` so page models and the cache-fill service depend on a fake in tests — the one new HTTP-boundary seam in the codebase.

## Out of Scope

- **Build execution** — this spec is the destination; the build is a separate effort launched after it is reviewed.
- **Per-area attack tactics UI** — the API has no per-area tactics read surface (R1-verified).
- **AI/LLM recommendation layer** and **future-opponent tactical preview** — explicitly "later" (decision 10).
- **Season dropdown / historical season browsing** on Home (decision 7).
- **Youth / National / U20 team views** — API supports the flags; nobody asked in v1.
- **Username + password game login** (scraping the game site) — undocumented, ToS-gray; superseded by the Club Link flow.
- **Multi-Member linking per User / shared accounts / roles** — single Member per User is the v1 model (unique index enforces it).
- **Account-deletion flow** — never surfaced as a need for a single-operator local tool.
- **Collection pages** other than `/Players` (D2, as amended by D7); **id-ownership checks** on fixture/player ids.
- **API tactic writes** — advice is text the user applies in-game; the API offers no readable strategy surface to round-trip against.
- **Thresholds UI, score prediction, what-if simulation, lineup/selection advice** (D8 non-goals).

## Further Notes

- **Deferred fog** (recorded on the map, not specced): "Load full season" backfill for Player History (one `fs` fill serves all 23 slots); whether `r=ps` resolves for foreign player ids (unprobed; matters only if the backfill graduates).
- **Doc corrections bundled into build** (live-falsified): `API_ENDPOINTS_COMPLETE.md` §6 `r=f`→`fi` (decision 15), §14/§15 lineup + match-summary codes, §26 owner-only team fields (R1/R2/R5 divergences #4–#6).
- **Manual operator steps**: rotate the committed Member Key (in-game password change) and the developer key (game UI) — git history keeps the old values.
- **Rate limits** govern every call plan here: 3 s between calls for the first 120 s of a burst, then 1 s for the next 120 s. All call budgets in this spec stay well inside that envelope.
- **Build order suggestion**: secrets scrub + Identity scaffold → API client fixes (`lu`/`ms`/`t`/`m`) + parser tests → Match Cache (raw + parsed, per D1) → auth + Club Link + gates → Home → Match Analysis + Recommendations → Player History + /Players → theme sweep (branch `prototype/dashboard-2-0-look` is the visual source) → legacy page deletion.
