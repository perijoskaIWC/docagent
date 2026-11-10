## What and why

<!-- What does this change, and why is it needed? Link the guide section or issue if there is one. -->

## How I tested it

- [ ] `dotnet build` and `dotnet test`
- [ ] `npm run build` and `npm run lint` in `src/web` (if the web app changed)
- [ ] Ran it with the AppHost and tried the change by hand

## Checklist

- Contract change (`src/DocAgent.Core`)? **yes / no**. If yes: agreed with Darko, and the fakes are updated in this PR.
- New strategy? **yes / no**. If yes: a keyed registration plus its `StrategyOption`.
- [ ] Every new or changed endpoint has a request in `src/DocAgent.Api/DocAgent.Api.http`
- [ ] No secrets, and only synthetic or public data

## Screenshots

<!-- For UI changes: before and after. -->
