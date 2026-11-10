import {
  Button, Caption1, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle, DialogTrigger, Divider,
  MessageBar, MessageBarBody, makeStyles, tokens,
} from '@fluentui/react-components'
import { ArrowClockwiseRegular } from '@fluentui/react-icons'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { useRerun } from '../../api/hooks'
import type { RunDetails } from '../../api/types'
import { StrategyPicker } from '../../components/StrategyPicker'
import type { StrategyChoice } from '../../components/StrategyPicker'
import { formatCost, formatNumber } from '../../format'

const useStyles = makeStyles({
  footer: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokens.spacingHorizontalXL },
  dialog: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL },
})

/** Usage and the trace id of the run, and "Rerun with…" another strategy on the same files. */
export function RunFooter({ run }: { run: RunDetails }) {
  const styles = useStyles()
  const { usage } = run
  return (
    <>
      <Divider />
      <div className={styles.footer}>
        <Caption1>
          {formatNumber(usage.inputTokens)} tokens in · {formatNumber(usage.outputTokens)} out · {usage.modelCalls} model
          calls · {usage.toolCalls} tool calls · {formatCost(usage.estimatedCostUsd)}
        </Caption1>
        {run.traceId && <Caption1>trace {run.traceId}</Caption1>}
        <RerunDialog runId={run.id} />
      </div>
    </>
  )
}

function RerunDialog({ runId }: { runId: string }) {
  const styles = useStyles()
  const navigate = useNavigate()
  const rerun = useRerun(runId)
  const [open, setOpen] = useState(false)
  const [strategy, setStrategy] = useState<StrategyChoice>({})

  const start = () =>
    rerun.mutate(strategy, {
      onSuccess: ({ runId: newRunId }) => {
        setOpen(false)
        navigate(`/runs/${newRunId}`)
      },
    })

  return (
    <Dialog open={open} onOpenChange={(_, data) => setOpen(data.open)}>
      <DialogTrigger disableButtonEnhancement>
        <Button icon={<ArrowClockwiseRegular />}>Rerun with…</Button>
      </DialogTrigger>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Rerun on the same files</DialogTitle>
          <DialogContent className={styles.dialog}>
            <Caption1>A slot left on "Same as this run" keeps this run's choice.</Caption1>
            <StrategyPicker value={strategy} onChange={setStrategy} emptyLabel="Same as this run" />
            {rerun.error && (
              <MessageBar intent="error">
                <MessageBarBody>
                  {rerun.error instanceof ApiError ? rerun.error.messages.join(' ') : rerun.error.message}
                </MessageBarBody>
              </MessageBar>
            )}
          </DialogContent>
          <DialogActions>
            <DialogTrigger disableButtonEnhancement>
              <Button appearance="secondary">Cancel</Button>
            </DialogTrigger>
            <Button appearance="primary" disabled={rerun.isPending} onClick={start}>
              Start rerun
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  )
}
