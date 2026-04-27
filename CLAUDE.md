# CLAUDE.md

This document describes how Claude sessions should work on this repository. It is a workflow guide, not user-facing documentation. The conventions here exist because they have produced good outcomes on prior work; treat them as defaults that can be adjusted when the situation calls for it.

## Working cadence

One prompt at a time. Claude drafts a prompt for Claude Code (CC), the user runs it in CC, the user reports results, Claude responds and drafts the next prompt. No speculative chaining of prompts based on assumed outcomes. The reason is simple: CC's actual output frequently differs from what was anticipated, and a chain built on that assumption is wrong from the second link forward.

CC prompts go in their own fenced code block, one prompt per Claude response. Other content in the same response (commentary, analysis, justification) sits outside the fenced block.

## Investigation before implementation

When a change has any structural or behavioral risk, the first CC prompt for that change is an investigation prompt: read these files, report verbatim content with line numbers, confirm the current state. Only after the investigation result is in does Claude draft the implementation prompt. The implementation prompt then has accurate context, including the exact strings that need to be matched and replaced.

After substantive edits, CC verifies verbatim: report post-edit content with line counts, grep for the anti-patterns that were supposed to be removed and confirm zero matches, run the relevant test slice. The verification step closes the loop on whether the change actually landed as intended.

## Decision style

Claude makes decisive recommendations rather than asking multi-part questions when the technical material supports a clear answer. The user pushes back if something looks wrong. Claude pushes back if the user proposes something that violates the project's standards or the codebase's invariants.

Multi-question Q&A loops ("would you like A or B? if A, would you like A1 or A2?") are an anti-pattern. They offload decisions Claude is in a position to make and slow the work down.

## Writing standards

No em dashes in any drafted content: code, comments, documentation, ADRs, commit messages, release notes, anywhere. Em dashes are a recognizable AI writing pattern and the project standard is to avoid them. Use colons, periods, parentheses, semicolons, or restructured prose.

Conventional commits format for commit messages: `feat:`, `fix:`, `docs:`, `chore:`, `test:`, `refactor:`. The subject line is imperative mood and under 72 characters. Body, when present, explains the why, not the what.

## Git practices

Stage explicitly. Use `git add <file1> <file2>` with named files. Do not use `git add -A`, `git add .`, or `git commit -a`. The reason is that the user works in Visual Studio's Git Changes window with deliberate staging, and CC's behavior should match that pattern so the repo state is always exactly what was intended.

Commits are batched by logical change. A feature that touches five files is one commit, not five. A documentation pass that updates README, CHANGELOG, and three ADRs is one commit. The boundary is logical coherence, not file count.

## Code standards

These are restated here for completeness; they also live in user memories.

Target framework: .NET 8, C# 12, Native AOT compatible. No runtime reflection in hot paths.

All new public types require XML doc comments. No nested classes in `.cs` files; each class gets its own file.

Every feature gets positive and negative test coverage. Diagnostics in particular need both "must fire on this input" and "must not fire on this input" tests; the project has a structural gate test that enforces this.

`ImplicitUsings` is disabled across the solution. All CC prompts that emit code must include explicit `using` directives.

## Quality bar

No known gaps at ship time. If a gap is identified, it is fixed before the release that would otherwise have shipped with it. Deferring known issues to a later release is not part of this project's release model. This applies to documentation gaps as much as code gaps; missing READMEs in satellite packages count as a gap.

## Architectural bias

Interface-first when extending behavior. Clean architecture, CQRS, DI throughout. Testability is a first-class design constraint, not a quality afterthought: when a seam (interface, abstraction, injection point) would make tests cleaner, prefer the seam even when the simpler implementation would also work. The cost of the seam is small; the cost of writing tests against an unseamed implementation is recurring.

## When to deviate

These conventions exist to produce good outcomes, not to be rigid. If a specific situation calls for a different approach (a one-line typo fix that does not need an investigation prompt, a small refactor where investigation and implementation can fit in one prompt without risk), name the deviation explicitly and proceed. The point is to make deviations conscious rather than accidental.
