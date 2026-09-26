# AGENTS.md

Instructions for AI coding agents working in this repo. The README explains what Landweigh is; this file only covers how to work here.

## Commands

- Build: `dotnet build`
- Test: `dotnet test` (must pass before every commit)

## Architecture rules

- Domain logic lives only in `src/Landweigh.Core`. `src/Landweigh.Cli` only reads input and calls Core. Core must never reference Cli.
- Every call to an LLM goes through one interface in Core (planned: `IProductExtractor`). Every page fetch goes through `IListingFetcher`. No direct model or HTTP calls from anywhere else.
- Never branch on product category. Category-specific data goes into the flat `attributes` map (string keys, string values).
- A price is always a list of price tiers on a variant, never a single number.

## Tests

- NUnit, in `tests/Landweigh.Tests`.
- Tests never call a real model or a real website. Use fakes and the files in `fixtures/`.
- Never edit `fixtures/*/expected.json` to make a test pass. They are hand-verified answer keys; labelling rules are in `fixtures/LABELLING.md`.

## Secrets

- Never write API keys, tokens or connection strings into any file in this repo.
- Locally the Gemini key is a user secret on `src/Landweigh.Cli`, named `Gemini:ApiKey`.

## Git

- `master` is protected. Work on a branch and open a pull request; CI must be green before merging.
- Conventional Commits, lowercase and imperative: `feat:`, `fix:`, `test:`, `refactor:`, `docs:`, `ci:`, `chore:`.
- One task per branch, small enough to review in one sitting.

## Ask before

- Adding a NuGet package.
- Changing the shape of `expected.json` or the rules in `fixtures/LABELLING.md`.
