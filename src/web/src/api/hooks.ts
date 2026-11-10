// TanStack Query hooks: the only way components read or change server data. Queries are cached by their key, so two
// components asking for the same run share one request, and a mutation can refresh the lists it affects.

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useRef, useState } from 'react'
import {
  ApiError, ask, createRun, getModel, getRun, getRuns, getStrategies, getTemplates, ingestKnowledge, rerun,
  searchKnowledge, sendFeedback,
} from './client'
import type { FeedbackInput, KnowledgeSearch, RerunInput, RunFilters } from './client'
import type { ChatCitation, RunStatus } from './types'

export const isTerminal = (status: RunStatus | undefined) => status === 'Completed' || status === 'Failed'

const pollInterval = 1500

/** Templates and strategies only change when the API restarts. */
export const useTemplates = () => useQuery({ queryKey: ['templates'], queryFn: getTemplates, staleTime: Infinity })
export const useStrategies = () => useQuery({ queryKey: ['strategies'], queryFn: getStrategies, staleTime: Infinity })

/** The run list; it keeps refreshing while any run in it is still going. */
export const useRuns = (filters: RunFilters) =>
  useQuery({
    queryKey: ['runs', filters],
    queryFn: () => getRuns(filters),
    refetchInterval: (query) => (query.state.data?.some((run) => !isTerminal(run.status)) ? pollInterval : false),
  })

/** One run, polled until it completes or fails (guide 1 §5.3). GET /api/runs/{id}/events could replace the polling. */
export const useRun = (id: string) =>
  useQuery({
    queryKey: ['run', id],
    queryFn: () => getRun(id),
    refetchInterval: (query) => (isTerminal(query.state.data?.status) ? false : pollInterval),
  })

/** The system model; only exists once the run has completed. */
export const useModel = (id: string, enabled: boolean) =>
  useQuery({ queryKey: ['model', id], queryFn: () => getModel(id), enabled, staleTime: Infinity })

export function useCreateRun() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: createRun,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['runs'] }),
  })
}

export function useRerun(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: RerunInput) => rerun(id, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['runs'] }),
  })
}

export const useFeedback = (id: string) => useMutation({ mutationFn: (input: FeedbackInput) => sendFeedback(id, input) })

export const useIngestKnowledge = () => useMutation({ mutationFn: ingestKnowledge })

/** Runs a search when given one; null means "nothing to search yet". */
export const useKnowledgeSearch = (search: KnowledgeSearch | null) =>
  useQuery({
    queryKey: ['knowledge-search', search],
    queryFn: () => searchKnowledge(search!),
    enabled: search !== null,
  })

// ── Chat ──

export interface ChatEntry {
  role: 'user' | 'assistant'
  text: string
  citations: ChatCitation[]
  /** True while the answer is still streaming in. */
  streaming: boolean
}

/**
 * A conversation with POST /api/ask: runId scopes it to one run, no runId asks the knowledge base. Streaming isn't a
 * cached query, so this is plain React state. The conversation id from the first answer goes with every follow-up.
 */
export function useChat(runId?: string) {
  const [entries, setEntries] = useState<ChatEntry[]>([])
  const [conversationId, setConversationId] = useState<string>()
  const [error, setError] = useState<string>()
  const [busy, setBusy] = useState(false)
  const controller = useRef<AbortController | null>(null)

  // Leaving the page closes the stream, which also stops the model call on the server.
  useEffect(() => () => controller.current?.abort(), [])

  const send = useCallback(
    async (question: string) => {
      const abort = new AbortController()
      controller.current = abort
      setError(undefined)
      setBusy(true)
      setEntries((list) => [
        ...list,
        { role: 'user', text: question, citations: [], streaming: false },
        { role: 'assistant', text: '', citations: [], streaming: true },
      ])

      try {
        const answer = await ask({ question, runId, conversationId }, abort.signal)
        if (answer.conversationId) setConversationId(answer.conversationId)
        for await (const delta of answer.deltas) {
          setEntries((list) =>
            updateLast(list, (last) => ({
              ...last,
              text: last.text + (delta.text ?? ''),
              citations: delta.citations ?? last.citations,
              streaming: !delta.done,
            })),
          )
        }
      } catch (e) {
        if (!abort.signal.aborted) setError(e instanceof ApiError ? e.messages.join(' ') : String(e))
        // Drop the empty answer bubble a failed request leaves behind.
        setEntries((list) => (list.at(-1)?.text ? list : list.slice(0, -1)))
      } finally {
        setEntries((list) => updateLast(list, (last) => ({ ...last, streaming: false })))
        setBusy(false)
      }
    },
    [runId, conversationId],
  )

  const stop = useCallback(() => controller.current?.abort(), [])

  const reset = useCallback(() => {
    controller.current?.abort()
    setEntries([])
    setConversationId(undefined)
    setError(undefined)
  }, [])

  return { entries, send, stop, reset, busy, error, conversationId }
}

function updateLast<T>(list: T[], change: (item: T) => T): T[] {
  return list.length === 0 ? list : [...list.slice(0, -1), change(list[list.length - 1])]
}
