import { Badge, Body1, Caption1, Subtitle1, Subtitle2, makeStyles, tokens } from '@fluentui/react-components'
import type { BadgeProps } from '@fluentui/react-components'
import type { IssueSeverity, RunDetails } from '../../api/types'
import { SimpleTable } from '../../components/SimpleTable'
import { formatPercent } from '../../format'

const useStyles = makeStyles({
  tab: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXL },
  round: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalS },
  heading: { display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalM },
})

const severityColor: Record<IssueSeverity, BadgeProps['color']> = {
  Info: 'informative', Minor: 'warning', Major: 'severe', Critical: 'danger',
}

/**
 * One block per review round: the LLM reviewer's verdict and issues, and the deterministic checks.
 * Stevcho (issues) and Riste (checks) own this tab from week 3.
 */
export function ReviewsTab({ run }: { run: RunDetails }) {
  const styles = useStyles()
  if (run.reviews.length === 0) return <Caption1>This run has no reviews.</Caption1>

  return (
    <div className={styles.tab}>
      {run.reviews.map((review) => (
        <section key={review.iteration} className={styles.round}>
          <div className={styles.heading}>
            <Subtitle1>Round {review.iteration}</Subtitle1>
            <Badge appearance="tint" color={review.approved ? 'success' : 'warning'}>
              {review.approved ? 'approved' : 'changes requested'}
            </Badge>
            <Caption1>score {formatPercent(review.score)}</Caption1>
          </div>
          <Body1>{review.summary}</Body1>

          <Subtitle2>Issues</Subtitle2>
          <SimpleTable
            columns={['Section', 'Severity', 'Problem', 'Suggested fix']}
            rows={review.issues.map((issue) => [
              issue.sectionId,
              <Badge key="severity" appearance="tint" color={severityColor[issue.severity]}>{issue.severity}</Badge>,
              issue.problem,
              issue.suggestedFix,
            ])}
            empty="No issues."
          />

          <Subtitle2>Checks</Subtitle2>
          <SimpleTable
            columns={['Check', 'Passed', 'Score', 'Detail']}
            rows={review.checks.map((check) => [
              check.checkId,
              check.passed ? 'yes' : 'no',
              formatPercent(check.score),
              check.detail,
            ])}
            empty="No checks."
          />
        </section>
      ))}
    </div>
  )
}
