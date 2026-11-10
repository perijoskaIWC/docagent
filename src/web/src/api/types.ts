// The API's JSON, typed. Property names are camelCase and enums arrive as their names ("Completed").
// Mirrors DocAgent.Core (the contracts) and src/DocAgent.Api/Runs/RunModels.cs: change them together.

export type RunStatus =
  | 'Queued' | 'Screening' | 'Profiling' | 'Detecting' | 'Drafting' | 'Reviewing' | 'Rendering' | 'Completed' | 'Failed'
export type FileKind =
  | 'Unknown' | 'Csv' | 'Excel' | 'Json' | 'Xml' | 'Xsd' | 'OpenApi' | 'Yaml' | 'Sql' | 'Pdf' | 'Markdown' | 'Text'
export type Certainty = 'Evidenced' | 'Assumption' | 'Unknown'
export type EvidenceKind = 'Model' | 'Knowledge'
export type IssueSeverity = 'Info' | 'Minor' | 'Major' | 'Critical'
export type KnowledgeKind = 'SystemProfile' | 'Pattern' | 'Standard' | 'Exemplar' | 'Glossary' | 'Lesson'
export type InterfaceKind = 'RestOperation' | 'FileFeed' | 'Webhook' | 'Message' | 'DatabaseView' | 'Other'
export type FindingLevel = 'Info' | 'Warning' | 'Error'

export const knowledgeKinds: KnowledgeKind[] = ['SystemProfile', 'Pattern', 'Standard', 'Exemplar', 'Glossary', 'Lesson']
export const searchModes = ['keyword', 'vector', 'hybrid', 'hybrid-semantic'] as const
export type SearchMode = (typeof searchModes)[number]

// ── Strategies and templates ──

export interface RunStrategy {
  detector: string
  author: string
  retrieval: string
  lessons: boolean
  maxReviewLoops: number
}

/** The three experiment slots a run can choose an implementation for. */
export type StrategySlot = 'detector' | 'author' | 'retrieval'

export interface StrategyOption {
  slot: string
  key: string
  description: string
  isFake: boolean
}

/** GET /api/strategies: options grouped by slot. */
export type Strategies = Record<string, StrategyOption[]>

export interface TableSpec { id: string; title: string; columns: string[] }

export interface SectionSpec {
  id: string
  title: string
  guidance: string
  required: boolean
  tables: TableSpec[]
  knowledgeKinds: KnowledgeKind[]
}

export interface DesignTemplate {
  id: string
  name: string
  description: string
  requiresTarget: boolean
  sections: SectionSpec[]
}

// ── Designs, detection, reviews ──

export interface EvidenceRef { kind: EvidenceKind; ref: string }
export interface DesignStatement { id: string; text: string; certainty: Certainty; evidence: EvidenceRef[] }
export interface DesignTable { tableId: string; rows: string[][]; evidence: EvidenceRef[] }
export interface OpenQuestion { id: string; question: string; whyItMatters: string }

export interface DesignPart {
  sectionId: string
  statements: DesignStatement[]
  tables: DesignTable[]
  openQuestions: OpenQuestion[]
}

export interface DetectionCandidate { systemId: string; displayName: string; confidence: number }

export interface SystemDetection {
  systemId: string
  displayName: string
  confidence: number
  evidence: string[]
  alternatives: DetectionCandidate[]
  strategy: string
}

export interface SolutionDesign {
  title: string
  templateId: string
  target: string | null
  system: SystemDetection
  parts: DesignPart[]
}

export interface ScreeningResult { allowed: boolean; flags: string[] }
export interface ReviewIssue { sectionId: string; severity: IssueSeverity; problem: string; suggestedFix: string }
export interface CheckResult { checkId: string; passed: boolean; score: number; detail: string }

export interface ReviewResult {
  iteration: number
  approved: boolean
  score: number
  issues: ReviewIssue[]
  checks: CheckResult[]
  summary: string
}

export interface RunUsage {
  inputTokens: number
  outputTokens: number
  modelCalls: number
  toolCalls: number
  estimatedCostUsd: number
}

// ── Runs ──

/** One line of GET /api/runs. */
export interface RunSummary {
  id: string
  createdAt: string
  status: RunStatus
  packName: string
  templateId: string
  strategy: RunStrategy
  detectedSystemId: string | null
  detectionConfidence: number | null
  inputTokens: number
  outputTokens: number
  estimatedCostUsd: number
  experimentId: string | null
  sourceRunId: string | null
}

export interface RunFileInfo { path: string; kind: FileKind; sizeBytes: number }

/** GET /api/runs/{id}. Screening, detection, design and markdown are null until the run completes. */
export interface RunDetails {
  id: string
  createdAt: string
  completedAt: string | null
  status: RunStatus
  progressMessage: string
  iteration: number
  errorMessage: string | null
  packName: string
  templateId: string
  target: string | null
  brief: string | null
  systemOverride: string | null
  strategy: RunStrategy
  files: RunFileInfo[]
  screening: ScreeningResult | null
  detection: SystemDetection | null
  design: SolutionDesign | null
  markdown: string | null
  reviews: ReviewResult[]
  usage: RunUsage
  traceId: string | null
  experimentId: string | null
  sourceRunId: string | null
}

// ── The system model (GET /api/runs/{id}/model) ──

export interface SourceRef { path: string; locator: string }

export interface FieldInfo {
  id: string
  name: string
  dataType: string | null
  isKey: boolean
  isRequired: boolean | null
  sampleValues: string[]
  source: SourceRef
}

export interface EntityInfo { id: string; name: string; fields: FieldInfo[]; rowCount: number | null; source: SourceRef }
export interface RelationshipInfo { id: string; fromFieldId: string; toFieldId: string; basis: string; source: SourceRef }

export interface InterfaceInfo {
  id: string
  name: string
  kind: InterfaceKind
  direction: string | null
  detail: string | null
  source: SourceRef
}

export interface ScheduleInfo { id: string; name: string; expression: string; source: SourceRef }
export interface Finding { id: string; level: FindingLevel; message: string; source: SourceRef | null }
export interface Signal { name: string; value: string; source: SourceRef }
export interface FileSummary { path: string; kind: FileKind; sizeBytes: number; note: string | null }

export interface SystemModel {
  packId: string
  files: FileSummary[]
  entities: EntityInfo[]
  relationships: RelationshipInfo[]
  interfaces: InterfaceInfo[]
  schedules: ScheduleInfo[]
  findings: Finding[]
  signals: Signal[]
}

// ── Chat and knowledge ──

export interface ChatCitation { kind: EvidenceKind; ref: string; label: string }

/** One streamed piece of an answer. Citations arrive with the final delta (done = true). */
export interface ChatDelta { text: string | null; citations: ChatCitation[] | null; done: boolean }

export interface KnowledgeChunk {
  chunkId: string
  documentId: string
  kind: KnowledgeKind
  title: string
  content: string
  sourceUri: string
  score: number
  tags: Record<string, string>
}

export interface IngestedDocument { id: string; kind: KnowledgeKind; title: string }

/** RFC 9457 ProblemDetails, as every API error returns it. A 400 lists its problems per field in errors. */
export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  errors?: Record<string, string[]>
}
