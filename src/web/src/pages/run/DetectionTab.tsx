import {
  Badge, Body1, Button, Caption1, Field, Input, MessageBar, MessageBarBody, ProgressBar, Subtitle2, Title3, makeStyles,
  tokens,
} from '@fluentui/react-components'
import { ArrowClockwiseRegular } from '@fluentui/react-icons'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { useRerun } from '../../api/hooks'
import type { RunDetails } from '../../api/types'
import { SimpleTable } from '../../components/SimpleTable'
import { formatPercent } from '../../format'

const useStyles = makeStyles({
  tab: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL, maxWidth: '720px' },
  system: { display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalM },
  evidence: { margin: 0, paddingLeft: tokens.spacingHorizontalXL },
  override: { display: 'flex', gap: tokens.spacingHorizontalS, alignItems: 'flex-end' },
})

/**
 * Which system the detector recognised, how sure it is and why. If it's wrong, the user names the right system and
 * reruns: the pipeline then skips detection (strategy "override"). Trajche owns this tab from week 3.
 */
export function DetectionTab({ run }: { run: RunDetails }) {
  const styles = useStyles()
  const navigate = useNavigate()
  const rerun = useRerun(run.id)
  const [systemId, setSystemId] = useState('')
  const detection = run.detection!

  return (
    <div className={styles.tab}>
      <div className={styles.system}>
        <Title3>{detection.displayName}</Title3>
        <Caption1>{detection.systemId}</Caption1>
        <Badge appearance="tint">{detection.strategy}</Badge>
      </div>

      <Field label={`Confidence: ${formatPercent(detection.confidence)}`}>
        <ProgressBar value={detection.confidence} thickness="large" />
      </Field>

      <Subtitle2>Evidence</Subtitle2>
      {detection.evidence.length === 0 ? (
        <Caption1>No evidence given.</Caption1>
      ) : (
        <ul className={styles.evidence}>
          {detection.evidence.map((line) => <li key={line}><Body1>{line}</Body1></li>)}
        </ul>
      )}

      <Subtitle2>Alternatives</Subtitle2>
      <SimpleTable
        columns={['System', 'Id', 'Confidence']}
        rows={detection.alternatives.map((a) => [a.displayName, a.systemId, formatPercent(a.confidence)])}
        empty="No other candidates."
      />

      <Subtitle2>Not right? Pick the system</Subtitle2>
      <div className={styles.override}>
        <Field label="System id" hint="e.g. helio-service, quarry-wms, dataverse">
          <Input value={systemId} onChange={(_, data) => setSystemId(data.value)} />
        </Field>
        <Button
          icon={<ArrowClockwiseRegular />}
          disabled={!systemId.trim() || rerun.isPending}
          onClick={() =>
            rerun.mutate({ systemOverride: systemId.trim() }, { onSuccess: ({ runId }) => navigate(`/runs/${runId}`) })
          }
        >
          Rerun with this system
        </Button>
      </div>
      {rerun.error && (
        <MessageBar intent="error">
          <MessageBarBody>{rerun.error instanceof ApiError ? rerun.error.messages.join(' ') : rerun.error.message}</MessageBarBody>
        </MessageBar>
      )}
    </div>
  )
}
