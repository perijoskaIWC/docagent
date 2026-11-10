// Typed functions for every endpoint. Components never call fetch themselves: they use the hooks in hooks.ts, which
// call these. A new endpoint gets a function here and a request in src/DocAgent.Api/DocAgent.Api.http.

import { readSse } from './sse'
import type {
  ChatDelta, DesignTemplate, IngestedDocument, KnowledgeChunk, KnowledgeKind, ProblemDetails, RunDetails,
  RunSummary, SearchMode, Strategies, SystemModel,
} from './types'

/** A failed request, with the API's ProblemDetails. */
export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? `The request failed (HTTP ${status}).`)
    this.status = status
    this.problem = problem
  }

  /** Every message to show: the per-field problems of a 400, or the one message. */
  get messages(): string[] {
    return this.problem.errors ? Object.values(this.problem.errors).flat() : [this.message]
  }

  static async from(response: Response): Promise<ApiError> {
    const problem = (await response.json().catch(() => ({}))) as ProblemDetails
    return new ApiError(response.status, problem)
  }
}

const jsonHeaders = { 'Content-Type': 'application/json' }

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, init)
  if (!response.ok) throw await ApiError.from(response)
  return (response.status === 204 ? undefined : await response.json()) as T
}

function query(params: Record<string, string | number | undefined>): string {
  const search = new URLSearchParams()
  for (const [name, value] of Object.entries(params))
    if (value !== undefined && value !== '') search.set(name, String(value))
  const text = search.toString()
  return text ? `?${text}` : ''
}

// ── Catalog ──

export const getTemplates = () => request<DesignTemplate[]>('/api/templates')
export const getStrategies = () => request<Strategies>('/api/strategies')

// ── Runs ──

export interface RunFilters {
  templateId?: string
  author?: string
  retrieval?: string
  detector?: string
  experimentId?: string
}

/** Strategy fields left undefined use the API's Strategy:Default. */
export interface NewRun {
  files: File[]
  templateId: string
  target?: string
  brief?: string
  detector?: string
  author?: string
  retrieval?: string
  lessons?: boolean
  maxReviewLoops?: number
}

/** Fields left undefined keep the source run's value. */
export interface RerunInput {
  templateId?: string
  target?: string
  systemOverride?: string
  detector?: string
  author?: string
  retrieval?: string
  lessons?: boolean
  maxReviewLoops?: number
}

export interface FeedbackInput { rating: 1 | -1; sectionId?: string; comment?: string }

export const getRuns = (filters: RunFilters) => request<RunSummary[]>(`/api/runs${query({ ...filters })}`)
export const getRun = (id: string) => request<RunDetails>(`/api/runs/${id}`)
export const getModel = (id: string) => request<SystemModel>(`/api/runs/${id}/model`)
export const designMarkdownUrl = (id: string) => `/api/runs/${id}/design.md`

export function createRun(input: NewRun) {
  const form = new FormData()
  for (const file of input.files) form.append('files', file, file.name)
  const fields: Record<string, string | number | boolean | undefined> = {
    templateId: input.templateId, target: input.target, brief: input.brief, detector: input.detector,
    author: input.author, retrieval: input.retrieval, lessons: input.lessons, maxReviewLoops: input.maxReviewLoops,
  }
  for (const [name, value] of Object.entries(fields))
    if (value !== undefined && value !== '') form.append(name, String(value))
  // No Content-Type header: the browser sets multipart/form-data with the boundary itself.
  return request<{ runId: string }>('/api/runs', { method: 'POST', body: form })
}

export const rerun = (id: string, input: RerunInput) =>
  request<{ runId: string }>(`/api/runs/${id}/rerun`, { method: 'POST', headers: jsonHeaders, body: JSON.stringify(input) })

export const sendFeedback = (id: string, input: FeedbackInput) =>
  request<void>(`/api/runs/${id}/feedback`, { method: 'POST', headers: jsonHeaders, body: JSON.stringify(input) })

// ── Ask ──

export interface AskInput { question: string; runId?: string; conversationId?: string }

/**
 * POST /api/ask. The answer streams back as Server-Sent Events; the conversation id comes in a header, so a
 * follow-up can continue the conversation. Aborting the signal stops the stream and the model call.
 */
export async function ask(input: AskInput, signal: AbortSignal) {
  const response = await fetch('/api/ask', { method: 'POST', headers: jsonHeaders, body: JSON.stringify(input), signal })
  if (!response.ok) throw await ApiError.from(response)
  return { conversationId: response.headers.get('X-Conversation-Id'), deltas: readSse<ChatDelta>(response) }
}

// ── Knowledge ──

export interface KnowledgeUpload { files: File[]; kind: KnowledgeKind; license: string; sourceUri?: string }

export function ingestKnowledge(input: KnowledgeUpload) {
  const form = new FormData()
  for (const file of input.files) form.append('files', file, file.name)
  form.append('kind', input.kind)
  form.append('license', input.license)
  if (input.sourceUri) form.append('sourceUri', input.sourceUri)
  return request<IngestedDocument[]>('/api/knowledge', { method: 'POST', body: form })
}

export interface KnowledgeSearch { q: string; kind?: KnowledgeKind; mode?: SearchMode; top?: number }

export const searchKnowledge = (search: KnowledgeSearch) =>
  request<KnowledgeChunk[]>(`/api/knowledge/search${query({ ...search })}`)
