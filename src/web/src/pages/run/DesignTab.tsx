import {
  Body1, Button, Caption1, Input, Subtitle1, Subtitle2, Tab, TabList, makeStyles, mergeClasses, tokens,
} from '@fluentui/react-components'
import { ArrowDownloadRegular, ThumbDislikeRegular, ThumbLikeRegular } from '@fluentui/react-icons'
import { useState } from 'react'
import Markdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { designMarkdownUrl } from '../../api/client'
import { useFeedback, useTemplates } from '../../api/hooks'
import type { DesignPart, DesignStatement, RunDetails, SectionSpec } from '../../api/types'
import { CertaintyBadge } from '../../components/CertaintyBadge'
import { SimpleTable } from '../../components/SimpleTable'

const useStyles = makeStyles({
  tab: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL, maxWidth: '900px' },
  toolbar: { display: 'flex', alignItems: 'center', justifyContent: 'space-between' },
  section: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    paddingBottom: tokens.spacingVerticalL,
    borderBottom: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
  },
  statement: { display: 'flex', alignItems: 'baseline', gap: tokens.spacingHorizontalS, margin: 0 },
  assumption: { fontStyle: 'italic' },
  questions: { margin: 0, paddingLeft: tokens.spacingHorizontalXL },
  feedback: { display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS },
  markdown: {
    '& table': { borderCollapse: 'collapse' },
    '& th, & td': { border: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke1}`, padding: tokens.spacingHorizontalS },
  },
})

/**
 * The design, either section by section from the structured parts (styled by certainty, with feedback per section) or
 * as the rendered Markdown that the download contains.
 */
export function DesignTab({ run }: { run: RunDetails }) {
  const styles = useStyles()
  const templates = useTemplates()
  const [view, setView] = useState<'sections' | 'markdown'>('sections')
  const design = run.design!

  // Sections in template order. Without the template, fall back to the parts in the order they came.
  const sections: Pick<SectionSpec, 'id' | 'title' | 'tables'>[] =
    templates.data?.find((t) => t.id === run.templateId)?.sections ??
    design.parts.map((part) => ({ id: part.sectionId, title: part.sectionId, tables: [] }))

  return (
    <div className={styles.tab}>
      <div className={styles.toolbar}>
        <TabList size="small" selectedValue={view} onTabSelect={(_, data) => setView(data.value as typeof view)}>
          <Tab value="sections">Sections</Tab>
          <Tab value="markdown">Markdown</Tab>
        </TabList>
        <Button as="a" href={designMarkdownUrl(run.id)} download icon={<ArrowDownloadRegular />}>
          Download .md
        </Button>
      </div>

      {view === 'markdown' ? (
        <div className={styles.markdown}>
          <Markdown remarkPlugins={[remarkGfm]}>{run.markdown ?? ''}</Markdown>
        </div>
      ) : (
        sections.map((section) => (
          <Section
            key={section.id}
            runId={run.id}
            section={section}
            part={design.parts.find((part) => part.sectionId === section.id)}
          />
        ))
      )}
    </div>
  )
}

interface SectionProps {
  runId: string
  section: Pick<SectionSpec, 'id' | 'title' | 'tables'>
  part: DesignPart | undefined
}

function Section({ runId, section, part }: SectionProps) {
  const styles = useStyles()
  return (
    <section className={styles.section}>
      <Subtitle1>{section.title}</Subtitle1>
      {!part ? (
        <Body1>
          <strong>[TBD]</strong> This section was not drafted.
        </Body1>
      ) : (
        <>
          {part.statements.map((statement) => <Statement key={statement.id} statement={statement} />)}
          {part.tables.map((table) => (
            <SimpleTable
              key={table.tableId}
              columns={section.tables.find((spec) => spec.id === table.tableId)?.columns ?? table.rows[0]?.map((_, i) => `Column ${i + 1}`) ?? []}
              rows={table.rows}
            />
          ))}
          {part.openQuestions.length > 0 && (
            <>
              <Subtitle2>Open questions</Subtitle2>
              <ul className={styles.questions}>
                {part.openQuestions.map((q) => (
                  <li key={q.id}>
                    <Body1>{q.question}</Body1> <Caption1>{q.whyItMatters}</Caption1>
                  </li>
                ))}
              </ul>
            </>
          )}
        </>
      )}
      <SectionFeedback runId={runId} sectionId={section.id} />
    </section>
  )
}

function Statement({ statement }: { statement: DesignStatement }) {
  const styles = useStyles()
  return (
    <p className={styles.statement}>
      <Body1 className={mergeClasses(statement.certainty === 'Assumption' && styles.assumption)}>{statement.text}</Body1>
      <CertaintyBadge certainty={statement.certainty} />
      {statement.evidence.length > 0 && <Caption1>evidence: {statement.evidence.map((e) => e.ref).join(', ')}</Caption1>}
    </p>
  )
}

/** Thumbs up or down for one section. The buttons lock after the first vote, so a double click can't count twice. */
function SectionFeedback({ runId, sectionId }: { runId: string; sectionId: string }) {
  const styles = useStyles()
  const feedback = useFeedback(runId)
  const [comment, setComment] = useState('')

  if (feedback.isSuccess) return <Caption1>Thanks, noted.</Caption1>

  const vote = (rating: 1 | -1) => feedback.mutate({ rating, sectionId, comment: comment.trim() || undefined })
  return (
    <div className={styles.feedback}>
      <Input size="small" placeholder="Comment (optional)" value={comment} onChange={(_, data) => setComment(data.value)} />
      <Button size="small" appearance="subtle" icon={<ThumbLikeRegular />} aria-label="Good section"
        disabled={feedback.isPending} onClick={() => vote(1)} />
      <Button size="small" appearance="subtle" icon={<ThumbDislikeRegular />} aria-label="Bad section"
        disabled={feedback.isPending} onClick={() => vote(-1)} />
      {feedback.error && <Caption1>{feedback.error.message}</Caption1>}
    </div>
  )
}
