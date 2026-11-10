import { Body1, Caption1, ProgressBar, Spinner, Subtitle2, makeStyles, tokens } from '@fluentui/react-components'
import { CheckmarkCircleRegular, CircleRegular } from '@fluentui/react-icons'
import type { RunDetails } from '../../api/types'
import { SimpleTable } from '../../components/SimpleTable'
import { formatBytes } from '../../format'
import { progressOf, runSteps } from '../../runSteps'

const useStyles = makeStyles({
  tab: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL, maxWidth: '720px' },
  steps: { listStyleType: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXS },
  step: { display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS },
  done: { color: tokens.colorPaletteGreenForeground1 },
  pending: { color: tokens.colorNeutralForeground4 },
})

/** Where the run is: a bar, the pipeline's latest message, and the steps done so far. */
export function ProgressTab({ run }: { run: RunDetails }) {
  const styles = useStyles()
  const current = runSteps.indexOf(run.status)
  const failed = run.status === 'Failed'

  return (
    <div className={styles.tab}>
      <ProgressBar
        value={progressOf(run.status)}
        color={failed ? 'error' : run.status === 'Completed' ? 'success' : 'brand'}
        thickness="large"
      />
      <Body1>
        {run.progressMessage}
        {run.iteration > 0 && ` (review round ${run.iteration})`}
      </Body1>

      {!failed && (
        <ul className={styles.steps}>
          {runSteps.map((step, index) => (
            <li key={step} className={styles.step}>
              {index < current || run.status === 'Completed' ? (
                <CheckmarkCircleRegular className={styles.done} />
              ) : index === current ? (
                <Spinner size="extra-tiny" />
              ) : (
                <CircleRegular className={styles.pending} />
              )}
              <Caption1>{step}</Caption1>
            </li>
          ))}
        </ul>
      )}

      <Subtitle2>Uploaded files</Subtitle2>
      <SimpleTable
        columns={['File', 'Kind', 'Size']}
        rows={run.files.map((file) => [file.path, file.kind, formatBytes(file.sizeBytes)])}
      />
    </div>
  )
}
