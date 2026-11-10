import {
  Accordion, AccordionHeader, AccordionItem, AccordionPanel, Badge, Button, Caption1, Card, CardHeader, Field, Input,
  MessageBar, MessageBarBody, SpinButton, Subtitle1, Switch, Text, Textarea, Title2, makeStyles, mergeClasses, tokens,
} from '@fluentui/react-components'
import { PlayRegular } from '@fluentui/react-icons'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ApiError } from '../api/client'
import { useCreateRun, useTemplates } from '../api/hooks'
import { FileDropZone } from '../components/FileDropZone'
import { StrategyPicker } from '../components/StrategyPicker'
import type { StrategyChoice } from '../components/StrategyPicker'

const useStyles = makeStyles({
  page: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXL, maxWidth: '960px' },
  section: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
  templates: { display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))', gap: tokens.spacingHorizontalM },
  // Fluent's selected card is only a shade darker; the outline makes the choice obvious.
  selected: { outline: `${tokens.strokeWidthThick} solid ${tokens.colorBrandStroke1}` },
  // In the card's corner the badge would otherwise wrap onto one line per word.
  badge: { whiteSpace: 'nowrap', flexShrink: 0 },
  lab: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL, paddingBottom: tokens.spacingVerticalM },
  labRow: { display: 'flex', gap: tokens.spacingHorizontalXXL, alignItems: 'flex-end' },
})

const packTypes = '.csv,.xlsx,.xls,.json,.ndjson,.jsonl,.yaml,.yml,.xml,.xsd,.sql,.pdf,.md,.txt,.zip'
const maxReviewLoopsLimit = 5

/** Upload an export pack, choose a design type, and optionally the lab settings, then start a run. */
export function NewRunPage() {
  const styles = useStyles()
  const navigate = useNavigate()
  const templates = useTemplates()
  const createRun = useCreateRun()

  const [files, setFiles] = useState<File[]>([])
  const [templateId, setTemplateId] = useState('')
  const [target, setTarget] = useState('')
  const [brief, setBrief] = useState('')
  const [strategy, setStrategy] = useState<StrategyChoice>({})
  const [lessons, setLessons] = useState(false)
  const [maxReviewLoops, setMaxReviewLoops] = useState(2)

  const template = templates.data?.find((t) => t.id === templateId)
  const needsTarget = template?.requiresTarget === true && !target.trim()
  const canStart = files.length > 0 && template !== undefined && !needsTarget && !createRun.isPending

  const start = () =>
    createRun.mutate(
      {
        files, templateId, brief, lessons, maxReviewLoops, ...strategy,
        target: template?.requiresTarget ? target : undefined,
      },
      { onSuccess: ({ runId }) => navigate(`/runs/${runId}`) },
    )

  return (
    <div className={styles.page}>
      <Title2>New run</Title2>

      <section className={styles.section}>
        <Subtitle1>1. The export pack</Subtitle1>
        <FileDropZone
          files={files}
          onChange={setFiles}
          accept={packTypes}
          hint="1 to 30 files of up to 10 MB each, or one .zip of up to 25 MB"
        />
      </section>

      <section className={styles.section}>
        <Subtitle1>2. The design type</Subtitle1>
        <div className={styles.templates}>
          {templates.data?.map((t) => (
            <Card
              key={t.id}
              className={mergeClasses(t.id === templateId && styles.selected)}
              selected={t.id === templateId}
              onSelectionChange={() => setTemplateId(t.id)}
            >
              <CardHeader
                header={<Text weight="semibold">{t.name}</Text>}
                description={<Caption1>{t.description}</Caption1>}
                action={t.requiresTarget ? <Badge appearance="outline" className={styles.badge}>needs a target</Badge> : undefined}
              />
            </Card>
          ))}
        </div>
        {template?.requiresTarget && (
          <Field label="Target platform" required hint="Where the data and processes move to, e.g. Dataverse or Azure SQL">
            <Input value={target} onChange={(_, data) => setTarget(data.value)} />
          </Field>
        )}
      </section>

      <section className={styles.section}>
        <Subtitle1>3. The goal (optional)</Subtitle1>
        <Field hint="In your own words: what should the design help you decide?">
          <Textarea value={brief} onChange={(_, data) => setBrief(data.value)} resize="vertical" />
        </Field>
      </section>

      <Accordion collapsible>
        <AccordionItem value="lab">
          <AccordionHeader>Lab settings</AccordionHeader>
          <AccordionPanel>
            <div className={styles.lab}>
              <Caption1>Leave a slot on Default to use the API's Strategy:Default.</Caption1>
              <StrategyPicker value={strategy} onChange={setStrategy} emptyLabel="Default" />
              <div className={styles.labRow}>
                <Switch label="Use lessons" checked={lessons} onChange={(_, data) => setLessons(data.checked)} />
                <Field label="Max review loops" hint={`0 to ${maxReviewLoopsLimit}`}>
                  <SpinButton
                    value={maxReviewLoops}
                    min={0}
                    max={maxReviewLoopsLimit}
                    onChange={(_, data) => {
                      const next = data.value ?? Number.parseInt(data.displayValue ?? '', 10)
                      if (Number.isFinite(next)) setMaxReviewLoops(Math.min(maxReviewLoopsLimit, Math.max(0, next)))
                    }}
                  />
                </Field>
              </div>
            </div>
          </AccordionPanel>
        </AccordionItem>
      </Accordion>

      {createRun.error && (
        <MessageBar intent="error">
          <MessageBarBody>
            {createRun.error instanceof ApiError ? createRun.error.messages.join(' ') : createRun.error.message}
          </MessageBarBody>
        </MessageBar>
      )}

      <div>
        <Button appearance="primary" size="large" icon={<PlayRegular />} disabled={!canStart} onClick={start}>
          {createRun.isPending ? 'Uploading…' : 'Start run'}
        </Button>
      </div>
    </div>
  )
}
