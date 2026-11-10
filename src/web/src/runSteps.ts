import type { RunStatus } from './api/types'

/** The pipeline's steps in order (FakeRunPipeline and Boban's pipeline report the same statuses). */
export const runSteps: RunStatus[] = ['Screening', 'Profiling', 'Detecting', 'Drafting', 'Reviewing', 'Rendering', 'Completed']

/** How far a run has got, from 0 to 1. A failed run shows a full bar in the error colour. */
export function progressOf(status: RunStatus): number {
  if (status === 'Completed' || status === 'Failed') return 1
  if (status === 'Queued') return 0
  return runSteps.indexOf(status) / (runSteps.length - 1)
}
