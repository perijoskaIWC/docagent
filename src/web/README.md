# DocAgent web

React + Vite + TypeScript, Fluent UI v9, React Router, TanStack Query.

## Run it

Start the AppHost (`dotnet run --project src/DocAgent.AppHost`) and open **web** from the Aspire dashboard. Aspire
starts the Vite dev server and tells it where the API is; Vite forwards every `/api` call there (`vite.config.ts`).

Checks before a PR: `npm run build` (type check + build) and `npm run lint`.

## Where things go

```
src/
├── api/types.ts      the API's JSON as TypeScript types (mirrors DocAgent.Core and the API's RunModels.cs)
├── api/client.ts     one typed function per endpoint
├── api/hooks.ts      TanStack Query hooks; useChat for the streaming Ask
├── api/sse.ts        reads a fetch() body as Server-Sent Events (POST /api/ask can't use EventSource)
├── components/       shared pieces: Layout, ChatPanel, StrategyPicker, FileDropZone, badges, SimpleTable
├── pages/            one file per route
└── pages/run/        one file per tab of the run page, so each owner works in their own file
```

## Review checklist (guide 1, "From week 3")

- Server data only through the hooks in `api/hooks.ts`: no `fetch` in components.
- Every endpoint has a typed function in `api/client.ts` and a request in `src/DocAgent.Api/DocAgent.Api.http`.
- Fluent UI components and `makeStyles`; no other CSS framework.
- No contract change without Darko.
