import {
  Badge, Body1, Button, Caption1, Card, CardHeader, Field, Input, MessageBar, MessageBarBody, Select, SpinButton,
  Spinner, Subtitle1, Text, Title2, makeStyles, tokens,
} from '@fluentui/react-components'
import { ArrowUploadRegular, SearchRegular } from '@fluentui/react-icons'
import { useState } from 'react'
import { ApiError } from '../api/client'
import type { KnowledgeSearch } from '../api/client'
import { useIngestKnowledge, useKnowledgeSearch } from '../api/hooks'
import { knowledgeKinds, searchModes } from '../api/types'
import type { KnowledgeKind, SearchMode } from '../api/types'
import { FileDropZone } from '../components/FileDropZone'

const useStyles = makeStyles({
  page: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXL, maxWidth: '960px' },
  card: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM, padding: tokens.spacingVerticalL },
  row: { display: 'flex', flexWrap: 'wrap', gap: tokens.spacingHorizontalL, alignItems: 'flex-end' },
  grow: { flexGrow: 1, minWidth: '240px' },
  results: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalS },
  content: { whiteSpace: 'pre-wrap' },
})

/** Add documents to the knowledge base, and see what retrieval returns for a query. */
export function KnowledgePage() {
  const styles = useStyles()
  return (
    <div className={styles.page}>
      <Title2>Knowledge</Title2>
      <UploadCard />
      <SearchCard />
    </div>
  )
}

function UploadCard() {
  const styles = useStyles()
  const ingest = useIngestKnowledge()
  const [files, setFiles] = useState<File[]>([])
  const [kind, setKind] = useState<KnowledgeKind>('Pattern')
  const [license, setLicense] = useState('')
  const [sourceUri, setSourceUri] = useState('')

  const upload = () =>
    ingest.mutate(
      { files, kind, license, sourceUri: sourceUri || undefined },
      { onSuccess: () => setFiles([]) },
    )

  return (
    <Card className={styles.card}>
      <Subtitle1>Add documents</Subtitle1>
      <Caption1>Synthetic, public or our own writing only, always with its licence (team overview §9).</Caption1>
      <FileDropZone files={files} onChange={setFiles} accept=".md,.txt,.pdf" hint=".md, .txt or .pdf, up to 10 MB each" />
      <div className={styles.row}>
        <Field label="Kind">
          <Select value={kind} onChange={(_, data) => setKind(data.value as KnowledgeKind)}>
            {knowledgeKinds.map((k) => <option key={k}>{k}</option>)}
          </Select>
        </Field>
        <Field label="Licence" required hint="e.g. CC-BY-4.0 or internal">
          <Input value={license} onChange={(_, data) => setLicense(data.value)} />
        </Field>
        <Field label="Source URL" hint="Where it comes from (optional)" className={styles.grow}>
          <Input value={sourceUri} onChange={(_, data) => setSourceUri(data.value)} />
        </Field>
      </div>
      <div>
        <Button
          appearance="primary"
          icon={<ArrowUploadRegular />}
          disabled={files.length === 0 || !license.trim() || ingest.isPending}
          onClick={upload}
        >
          Upload
        </Button>
      </div>
      {ingest.data && (
        <MessageBar intent="success">
          <MessageBarBody>Added: {ingest.data.map((d) => `${d.title} (${d.id})`).join(', ')}</MessageBarBody>
        </MessageBar>
      )}
      {ingest.error && (
        <MessageBar intent="error">
          <MessageBarBody>
            {ingest.error instanceof ApiError ? ingest.error.messages.join(' ') : ingest.error.message}
          </MessageBarBody>
        </MessageBar>
      )}
    </Card>
  )
}

function SearchCard() {
  const styles = useStyles()
  const [text, setText] = useState('')
  const [mode, setMode] = useState<SearchMode>('hybrid-semantic')
  const [kind, setKind] = useState<KnowledgeKind | ''>('')
  const [top, setTop] = useState(5)
  const [search, setSearch] = useState<KnowledgeSearch | null>(null)
  const results = useKnowledgeSearch(search)

  const run = () => setSearch({ q: text.trim(), mode, kind: kind || undefined, top })

  return (
    <Card className={styles.card}>
      <Subtitle1>Search (debug)</Subtitle1>
      <div className={styles.row}>
        <Field label="Query" className={styles.grow}>
          <Input value={text} onChange={(_, data) => setText(data.value)} onKeyDown={(e) => e.key === 'Enter' && text.trim() && run()} />
        </Field>
        <Field label="Mode">
          <Select value={mode} onChange={(_, data) => setMode(data.value as SearchMode)}>
            {searchModes.map((m) => <option key={m}>{m}</option>)}
          </Select>
        </Field>
        <Field label="Kind">
          <Select value={kind} onChange={(_, data) => setKind(data.value as KnowledgeKind | '')}>
            <option value="">Any</option>
            {knowledgeKinds.map((k) => <option key={k}>{k}</option>)}
          </Select>
        </Field>
        <Field label="Top">
          <SpinButton value={top} min={1} max={50} onChange={(_, data) => data.value && setTop(data.value)} />
        </Field>
        <Button appearance="primary" icon={<SearchRegular />} disabled={!text.trim()} onClick={run}>
          Search
        </Button>
      </div>

      {results.isFetching && <Spinner size="tiny" label="Searching" />}
      {results.error && (
        <MessageBar intent="error">
          <MessageBarBody>{results.error.message}</MessageBarBody>
        </MessageBar>
      )}
      {results.data?.length === 0 && <Caption1>Nothing found.</Caption1>}
      <div className={styles.results}>
        {results.data?.map((chunk) => (
          <Card key={chunk.chunkId} appearance="outline">
            <CardHeader
              header={<Text weight="semibold">{chunk.title}</Text>}
              description={<Caption1>{chunk.chunkId} · score {chunk.score.toFixed(2)} · {chunk.sourceUri}</Caption1>}
              action={<Badge appearance="tint">{chunk.kind}</Badge>}
            />
            <Body1 className={styles.content}>{chunk.content}</Body1>
          </Card>
        ))}
      </div>
    </Card>
  )
}
