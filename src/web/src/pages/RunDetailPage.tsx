import {
  Caption1, Link, MessageBar, MessageBarBody, MessageBarTitle, Spinner, Tab, TabList, Title2, makeStyles, tokens,
} from '@fluentui/react-components'
import type { SelectTabData } from '@fluentui/react-components'
import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { ApiError } from '../api/client'
import { useRun } from '../api/hooks'
import { ChatPanel } from '../components/ChatPanel'
import { StatusBadge } from '../components/StatusBadge'
import { formatDateTime } from '../format'
import { DesignTab } from './run/DesignTab'
import { DetectionTab } from './run/DetectionTab'
import { ModelTab } from './run/ModelTab'
import { ProgressTab } from './run/ProgressTab'
import { ReviewsTab } from './run/ReviewsTab'
import { RunFooter } from './run/RunFooter'

const useStyles = makeStyles({
  page: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL },
  title: { display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalM },
  panel: { minHeight: '240px' },
})

type TabValue = 'progress' | 'detection' | 'design' | 'model' | 'reviews' | 'ask'

export function RunDetailPage() {
  const { id = '' } = useParams()
  // A new key per run, so a rerun opens on its own fresh state (selected tab, forms).
  return <RunView key={id} id={id} />
}

function RunView({ id }: { id: string }) {
  const styles = useStyles()
  const navigate = useNavigate()
  const run = useRun(id)
  const [chosenTab, setChosenTab] = useState<TabValue>()

  if (run.isPending) return <Spinner label="Loading the run" />
  if (run.isError)
    return (
      <MessageBar intent="error">
        <MessageBarBody>
          {run.error instanceof ApiError && run.error.status === 404 ? 'There is no such run.' : run.error.message}
        </MessageBarBody>
      </MessageBar>
    )

  const data = run.data
  const completed = data.status === 'Completed'
  // Until someone picks a tab: the progress while the run is going, the design once it is done.
  const tab = chosenTab ?? (completed ? 'design' : 'progress')

  return (
    <div className={styles.page}>
      <div>
        <div className={styles.title}>
          <Title2>{data.design?.title ?? data.packName}</Title2>
          <StatusBadge status={data.status} />
        </div>
        <Caption1>
          {data.templateId} · {data.strategy.detector} / {data.strategy.author} / {data.strategy.retrieval} · started{' '}
          {formatDateTime(data.createdAt)}
          {data.sourceRunId && (
            <>
              {' · rerun of '}
              <Link onClick={() => navigate(`/runs/${data.sourceRunId}`)}>{data.sourceRunId.slice(0, 8)}</Link>
            </>
          )}
        </Caption1>
      </div>

      {data.status === 'Failed' && (
        <MessageBar intent="error">
          <MessageBarBody>
            <MessageBarTitle>The run failed</MessageBarTitle>
            {data.errorMessage}
          </MessageBarBody>
        </MessageBar>
      )}

      <TabList selectedValue={tab} onTabSelect={(_, selected: SelectTabData) => setChosenTab(selected.value as TabValue)}>
        <Tab value="progress">Progress</Tab>
        <Tab value="detection" disabled={!completed}>Detection</Tab>
        <Tab value="design" disabled={!completed}>Design</Tab>
        <Tab value="model" disabled={!completed}>What we found</Tab>
        <Tab value="reviews" disabled={!completed}>Reviews</Tab>
        <Tab value="ask">Ask</Tab>
      </TabList>

      <div className={styles.panel}>
        {tab === 'progress' && <ProgressTab run={data} />}
        {tab === 'detection' && completed && <DetectionTab run={data} />}
        {tab === 'design' && completed && <DesignTab run={data} />}
        {tab === 'model' && completed && <ModelTab runId={data.id} />}
        {tab === 'reviews' && completed && <ReviewsTab run={data} />}
        {tab === 'ask' && <ChatPanel runId={data.id} placeholder="Ask about this run, e.g. which entities did you find?" />}
      </div>

      <RunFooter run={data} />
    </div>
  )
}
