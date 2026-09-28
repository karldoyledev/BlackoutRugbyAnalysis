# Wayfinder Map: Squad page card-by-card design

**Repo**: karldoyledev/BlackoutRugbyAnalysis — this file is the local draft of the GitHub issue labelled `wayfinder:map`. Each ticket is a child issue (`Part of #<map>`, labels `wayfinder:grilling` / `wayfinder:task`), blocking edges are GitHub native `blocked_by` dependencies.

**Tracker (live)**: Map = [#20](https://github.com/karldoyledev/BlackoutRugbyAnalysis/issues/20) · S1 #21 · S2 #23 · S3 #22 · S4 #27 · S5 #26 · S6 #25 · S7 #28 · S8 #24 · P1 #29. All 9 sub-issue links wired; edges restructured 2026-09-28 (see Notes). **Status**: frontier = P1 (card judging prototype) + S2 (load & snapshot model).

## Destination

A locked, per-card design for the /Squad page: for every visible card, its intent (the question it answers for the manager), its value verdict (earns its place / changes / goes), and its form (data source, visual, interactions) - recorded as an amendment to `docs/spec/dashboard-2-0-spec.md` that supersedes decision 12's "existing MemberStatsComparison moves to /Squad unchanged in behavior". Static prototypes exist for any card whose form was contested. The map is done when the spec amendment is written and reviewed; the build is a separate later effort.

## Notes

- **Unit of work = the card.** Cards on the visible /Squad surface (top to bottom): C1 control panel (infrastructure; redesigned only as a consequence of the load-model decision), C2 status/warning/error banners (out of scope; D3-governed), C3 "Squad changes since the previous load" snapshot-delta table, C4 "Member stats comparison" ranked-bars card, C5 "Member stats breakdown" fixture-by-subject bars card, C6 "Team stats per game" multi-series line card, C7 "Recent match details" table, C8 debug panel (out of scope; dev tool).
- **Card template**, resolved in order in every card ticket: intent (what question did you originally want it to answer) -> value (does it still earn its place) -> design (form, data source, interactions) -> prototype only if the form is contested.
- Resolve tickets with **grilling**; at most one per session.
- **Prototype-first override (2026-09-28, user decision)**: the user was unsure of the page's primary job and chose to prototype the cards and talk through them individually BEFORE deriving it. S1 is re-scoped as the derived synthesis (blocked by S3-S7, amends its original body on the ticket); P1 (#29) builds the judging artifact up front; card tickets grill against it. The card template's intent -> value -> design order stands; the prototype now precedes the conversation instead of graduating after it.
- Language: **Player**, never "Member" (Context.md glossary; the legacy page was MemberStatsComparison).
- Theme: D10 "command centre" constraints apply to any prototype.
- Data rules in force: Match Cache strict cache-first patterns (Dashboard 2.0 decisions 11/20); SnapshotStore lifecycle untouched unless a card decision explicitly changes it; the live roster read (2 calls) is the only present-state source (CSR/form/energy/salary/age); PlayerSeasons cached per Player+season; squad window = last 20 completed fixtures (SquadWindowLast).
- Prototype pattern when graduated: static single-file HTML on a branch, plus an optional throwaway Razor route for viewing in the running app (the D10 prototype pattern); sample data only; label `wayfinder:prototype`.
- Decisions 1-15 of the Dashboard 2.0 map remain in force; this map amends decision 12 only.
- Tracker: GitHub Issues (docs/agents/issue-tracker.md).

## Decisions so far

(none yet - the map is freshly charted)

## Not yet specified

- Prototype tickets: graduated early by the prototype-first override — P1 (#29) is the judging artifact, built before the card conversations (in `docs/prototypes/` because `.scratch/` is gitignored). A NEW prototype ticket only graduates if a card resolution demands a treatment P1 does not show.
- Spec-amendment structure: whether the /Squad section of `dashboard-2-0-spec.md` is rewritten wholesale or as a decision-by-decision addendum - settled when assembling it.
- Shape of the follow-on build effort (tickets, order) - sketched when the synthesis ticket closes, if wanted.

## Out of scope

- Implementing the redesign: no production code changes in this map; the build launches from the synthesis ticket's spec amendment.
- C2 banners and C8 debug panel: not reviewed.
- Issues #2 and #3 (stale pre-cache refactor tickets): closed as superseded at charting time.

## Tickets

### P1 — Prototype the Squad cards for judging (static HTML, sample data) — `wayfinder:prototype` — frontier (#29)

**Question**: Build the judging artifact: one static single-file HTML prototype presenting each visible /Squad card in its current form plus alternative treatments where the form is open, per-card switcher, all sample data, D10 command-centre theme. The card tickets grill against it.

**Notes**: pulled forward by the 2026-09-28 prototype-first override. Lives at `docs/prototypes/squad-card-prototype.html` on branch `prototype/squad-card-design` (`.scratch/` is gitignored). Tracker: #29.

### S1 — Lock the Squad page primary job — `wayfinder:grilling` — blocked by S3-S7 (#21, amended 2026-09-28)

**Question**: What is the /Squad page FOR - the one question a manager arrives at /Squad to answer - and how does it divide labor with Home, Match Analysis, and Player History? AMENDED: the job is DERIVED from the card resolutions (judged against the P1 prototype), not pre-set; story 38's "squad development" intent remains the recorded hypothesis to test against the evidence.

**Notes**: resolve with **grilling**. Blocked by S3, S4, S5, S6, S7; blocks S8. Tracker: #21.

### S2 — Lock the load and snapshot model — `wayfinder:grilling` — frontier (#23)

**Question**: Auto cache-first render on visit vs. manual load; what the capture/refresh action is; season scope control; death of the manual endpoint/Team fields; the zero-snapshot cold start; what C1 becomes.

**Notes**: resolve with **grilling**. Blocks S3-S7. Tracker: #23.

### S3 — Card C3 (Squad changes since the previous load) intent and design — `wayfinder:grilling` — blocked by S1, S2 (#22)

**Question**: Per the card template: intent, value (baseline = previous load - is that right), design (which deltas matter; the filter that silently hides salary/form-only changes), prototype if contested.

**Notes**: resolve with **grilling**. Blocked by S1, S2; blocks S8. Tracker: #22.

### S4 — Card C4 (ranked Player comparison) intent and design — `wayfinder:grilling` — blocked by S1, S2 (#27)

**Question**: Per the card template: intent ("who is my best X" vs. squad strength scan), value (23-bar chart vs. alternatives), design (curated stats vs. the ~40-stat dump; present-state vs. season aggregates in one selector), prototype if contested.

**Notes**: resolve with **grilling**. Blocked by S1, S2; blocks S8. Tracker: #27.

### S5 — Card C5 (fixture breakdown) intent and design — `wayfinder:grilling` — blocked by S1, S2 (#26)

**Question**: Per the card template: intent ("who did what in match N" vs. the team's shape), value (distinct job given Player History + Match Analysis), design (team/player selector split; the silently-chosen 7 metrics vs. Stat Groups), prototype if contested.

**Notes**: resolve with **grilling**. Blocked by S1, S2; blocks S8. Tracker: #26.

### S6 — Card C6 (team trend line) intent and design — `wayfinder:grilling` — blocked by S1, S2 (#25)

**Question**: Per the card template: intent (what trend matters), value (4-series line vs. noise), design (backwards time axis; unit mismatch flattening small series), prototype if contested.

**Notes**: resolve with **grilling**. Blocked by S1, S2; blocks S8. Tracker: #25.

### S7 — Card C7 (recent Fixtures table) intent and design — `wayfinder:grilling` — blocked by S1, S2 (#28)

**Question**: Per the card template: intent (form sheet vs. navigation hub), value (column overlap with Home/Match Analysis), design (the unimplemented D7 promise of row links to /Fixtures/{id} and /Players/{id}; minimal column set), prototype if contested.

**Notes**: resolve with **grilling**. Blocked by S1, S2; blocks S8. Tracker: #28.

### S8 — Assemble the Squad page design spec — `wayfinder:task` — blocked by S3-S7 (#24)

**Question**: No new decisions - assemble the spec amendment at `docs/spec/dashboard-2-0-spec.md` (per-card section superseding decision 12) from the S1-S7 resolutions plus any graduated prototype verdicts. This is the destination artifact; when it is written and reviewed, the map is done and the build effort can be launched.

**Notes**: `wayfinder:task`. Blocked by S3, S4, S5, S6, S7. Tracker: #24.

