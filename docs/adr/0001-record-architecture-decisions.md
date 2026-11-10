# 0001: Record architecture decisions

Status: accepted (2026-11-10)

## Context

Seven people build DocAgent in parallel, and it is a lab: approaches are compared, kept or dropped. Months from now,
someone will ask why a choice was made, and the answer must not depend on who remembers the meeting.

## Decision

Every decision that someone might later question gets a one-page architecture decision record (ADR) in `docs/adr/`,
numbered in order (`0002-stack.md`, `0003-...`), with three sections: Context, Decision, Consequences.

- An ADR is written when the decision is made, in the same pull request as the change when there is one.
- An accepted ADR is never rewritten. A changed decision gets a new ADR that says which one it supersedes, and the old
  one's status becomes "superseded by 00NN".
- Short and plain: one page, written for a teammate who wasn't there.

## Consequences

- New team members and reviewers can read why the system looks the way it does.
- Changing a decision is cheap, but leaves a trace: the old ADR stays, marked superseded.
- Small, reversible choices don't need an ADR; the pull request description is enough.
