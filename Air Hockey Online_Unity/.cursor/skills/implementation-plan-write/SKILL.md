---
name: implementation-plan-write
description: >-
  Produces structured feature implementation plan markdown: context from the repo,
  requirements flow, design notes, checkbox task lists, acceptance criteria, and open
  questions. Use when the user asks for an implementation plan, feature plan document,
  technical spec before coding, or checkbox task breakdown for a new feature.
---

# Write an implementation plan document

## When to use

Apply this skill when authoring a **plan-only** markdown doc (before or alongside code). The doc should be actionable for humans and agents: enough file pointers and checkboxes that implementation can be traced and later marked complete.

## Workflow

1. **Discover**: Read relevant code, existing architecture notes (`**/Doc/*.md`), and packet/sync docs. Grep or search for entities, commands, and touch points named in the request.
2. **Place the file**: Put the doc where the team keeps specs (e.g. `Assets/_MH/SharedLibrary/Doc/` or `Server Sln/Shared/Scripts/Doc/`). Prefer one canonical location per feature; link across repos with relative paths.
3. **Write sections** in order below.
4. **After implementation**: Update the same doc by changing `- [ ]` to `- [x]` for finished tasks; leave tests, optional polish, and unresolved questions unchecked.

## Document template

Use this structure (adapt headings; keep **checkbox tasks** as `- [ ]`).

```markdown
# Implementation plan: [Feature name]

Short intro: what problem this solves and what “done” means.

## Context (current codebase)

Bullet list of **existing** types/files/packets and how they relate today.
Use full repo-relative paths. Call out gaps (“not wired yet”, “follow-up in ARCH doc”).

## Product flow (requirement)

Numbered list: user-visible or system-visible flow end-to-end (who acts first, what is authoritative).

## Design notes

- Decisions, constraints, and guards (idempotency, debounce, phase/state machines).
- Protocol: new packet vs extending an existing one; versioning warning if wire format changes.
- What **not** to do (e.g. reuse bounce code for triggers).
- Config/tuning fields if behavior should be data-driven.

## Tasks

Group tasks by layer or subsystem. Every task line **must** be a checkbox:

### [Area name]

- [ ] Concrete, verifiable item (often maps to one PR slice or one file cluster).
- [ ] …

### …

## Acceptance criteria

- [ ] Testable outcomes (behavior, authority, UX). Mirror the requirement flow where helpful.

## Open questions

- [ ] Items that are intentionally undecided or out of scope for the first slice.
```

## Task checklist rules

- **One checkbox = one ownership-sized unit** (not “implement the whole feature”).
- Prefer **file or subsystem** hints in the task text when obvious (`Match.cs`, `s2c_*`, `GameRunner`).
- Include **parity** tasks when the repo duplicates shared code (e.g. Unity `SharedLibrary` vs `Server Sln/Shared`).
- Add **docs** tasks: architecture markdown, protocol section updates.
- Optional / nice-to-have: label in the task text (“Optional: …”) so it can stay `[ ]` without blocking “done”.

## Quality bar

- **Context** must be grounded in what was read; avoid generic filler.
- **Design notes** should record trade-offs the implementer would otherwise re-decide.
- **Acceptance criteria** should be checkable without reading the whole design.
- Fix **relative links** to other docs (count `../` from the plan file’s folder).

## Example (condensed)

See `Assets/_MH/SharedLibrary/Doc/PlayerGotScoreImplementationPlan.md` in this project for a full example: goal scoring, `MatchPhase`, `s2c_goal_scored`, guest prediction caveats, and checkbox maintenance after implementation.
