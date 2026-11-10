import {
  Button, Caption1, Field, Link, MessageBar, MessageBarBody, Select, Spinner, Table, TableBody, TableCell, TableHeader,
  TableHeaderCell, TableRow, Title2, makeStyles, tokens,
} from '@fluentui/react-components'
import { AddRegular } from '@fluentui/react-icons'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useRuns, useTemplates } from '../api/hooks'
import { StatusBadge } from '../components/StatusBadge'
import { StrategyPicker } from '../components/StrategyPicker'
import type { StrategyChoice } from '../components/StrategyPicker'
import { formatCost, formatDateTime, formatNumber, formatPercent } from '../format'

const useStyles = makeStyles({
  page: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL },
  heading: { display: 'flex', alignItems: 'center', justifyContent: 'space-between' },
  filters: { display: 'flex', flexWrap: 'wrap', gap: tokens.spacingHorizontalL, alignItems: 'flex-start' },
})

/** The latest 50 runs, filterable by template and strategy, so runs of one experiment can be compared. */
export function RunsPage() {
  const styles = useStyles()
  const navigate = useNavigate()
  const [templateId, setTemplateId] = useState('')
  const [strategy, setStrategy] = useState<StrategyChoice>({})
  const templates = useTemplates()
  const runs = useRuns({ templateId, ...strategy })

  return (
    <div className={styles.page}>
      <div className={styles.heading}>
        <Title2>Runs</Title2>
        <Button appearance="primary" icon={<AddRegular />} onClick={() => navigate('/runs/new')}>
          New run
        </Button>
      </div>

      <div className={styles.filters}>
        <Field label="Template" hint="Filter by design type">
          <Select value={templateId} onChange={(_, data) => setTemplateId(data.value)}>
            <option value="">Any</option>
            {templates.data?.map((template) => (
              <option key={template.id} value={template.id}>
                {template.name}
              </option>
            ))}
          </Select>
        </Field>
        <StrategyPicker value={strategy} onChange={setStrategy} emptyLabel="Any" />
      </div>

      {runs.isPending && <Spinner label="Loading runs" />}
      {runs.error && (
        <MessageBar intent="error">
          <MessageBarBody>{runs.error.message}</MessageBarBody>
        </MessageBar>
      )}
      {runs.data?.length === 0 && <Caption1>No runs match. Start one with New run.</Caption1>}
      {runs.data && runs.data.length > 0 && (
        <Table size="small">
          <TableHeader>
            <TableRow>
              {['Created', 'Pack', 'Template', 'Author', 'Retrieval', 'Detector', 'Detected system', 'Status', 'Tokens', 'Cost']
                .map((column) => <TableHeaderCell key={column}>{column}</TableHeaderCell>)}
            </TableRow>
          </TableHeader>
          <TableBody>
            {runs.data.map((run) => (
              <TableRow key={run.id}>
                <TableCell>{formatDateTime(run.createdAt)}</TableCell>
                <TableCell>
                  <Link onClick={() => navigate(`/runs/${run.id}`)}>{run.packName}</Link>
                  {run.sourceRunId && <Caption1> (rerun)</Caption1>}
                </TableCell>
                <TableCell>{run.templateId}</TableCell>
                <TableCell>{run.strategy.author}</TableCell>
                <TableCell>{run.strategy.retrieval}</TableCell>
                <TableCell>{run.strategy.detector}</TableCell>
                <TableCell>
                  {run.detectedSystemId
                    ? `${run.detectedSystemId} (${formatPercent(run.detectionConfidence ?? 0)})`
                    : '–'}
                </TableCell>
                <TableCell>
                  <StatusBadge status={run.status} />
                </TableCell>
                <TableCell>{formatNumber(run.inputTokens + run.outputTokens)}</TableCell>
                <TableCell>{formatCost(run.estimatedCostUsd)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  )
}
