import {
  Badge, Caption1, MessageBar, MessageBarBody, Spinner, Subtitle1, Subtitle2, makeStyles, tokens,
} from '@fluentui/react-components'
import type { BadgeProps } from '@fluentui/react-components'
import { useModel } from '../../api/hooks'
import type { FindingLevel, SourceRef } from '../../api/types'
import { SimpleTable } from '../../components/SimpleTable'
import { formatBytes } from '../../format'

const useStyles = makeStyles({
  tab: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL },
  entity: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXS },
})

const source = (ref: SourceRef | null) => (ref ? `${ref.path} ${ref.locator}` : '–')

const levelColor: Record<FindingLevel, BadgeProps['color']> = { Info: 'informative', Warning: 'warning', Error: 'danger' }

/** The SystemModel the profiler built from the pack: what the design's evidence points at. Riste owns this tab from week 3. */
export function ModelTab({ runId }: { runId: string }) {
  const styles = useStyles()
  const model = useModel(runId, true)

  if (model.isPending) return <Spinner label="Loading the model" />
  if (model.isError)
    return (
      <MessageBar intent="error">
        <MessageBarBody>{model.error.message}</MessageBarBody>
      </MessageBar>
    )

  const m = model.data
  return (
    <div className={styles.tab}>
      <Subtitle1>Entities ({m.entities.length})</Subtitle1>
      {m.entities.length === 0 && <Caption1>None found.</Caption1>}
      {m.entities.map((entity) => (
        <div key={entity.id} className={styles.entity}>
          <Subtitle2>
            {entity.name} <Caption1>{entity.id} · {entity.rowCount ?? '?'} rows · {source(entity.source)}</Caption1>
          </Subtitle2>
          <SimpleTable
            columns={['Field', 'Type', 'Key', 'Required', 'Samples', 'Id']}
            rows={entity.fields.map((field) => [
              field.name,
              field.dataType ?? '?',
              field.isKey ? 'yes' : '',
              field.isRequired === null ? '?' : field.isRequired ? 'yes' : 'no',
              field.sampleValues.join(', '),
              field.id,
            ])}
          />
        </div>
      ))}

      <Subtitle1>Relationships ({m.relationships.length})</Subtitle1>
      <SimpleTable
        columns={['From', 'To', 'Basis', 'Source']}
        rows={m.relationships.map((r) => [r.fromFieldId, r.toFieldId, r.basis, source(r.source)])}
      />

      <Subtitle1>Interfaces ({m.interfaces.length})</Subtitle1>
      <SimpleTable
        columns={['Name', 'Kind', 'Direction', 'Detail', 'Source']}
        rows={m.interfaces.map((i) => [i.name, i.kind, i.direction ?? '', i.detail ?? '', source(i.source)])}
      />

      <Subtitle1>Schedules ({m.schedules.length})</Subtitle1>
      <SimpleTable
        columns={['Name', 'When', 'Source']}
        rows={m.schedules.map((s) => [s.name, s.expression, source(s.source)])}
      />

      <Subtitle1>Findings ({m.findings.length})</Subtitle1>
      <SimpleTable
        columns={['Level', 'Message', 'Source']}
        rows={m.findings.map((f) => [
          <Badge key="level" appearance="tint" color={levelColor[f.level]}>{f.level}</Badge>,
          f.message,
          source(f.source),
        ])}
      />

      <Subtitle1>Files ({m.files.length})</Subtitle1>
      <SimpleTable
        columns={['File', 'Kind', 'Size', 'Note']}
        rows={m.files.map((f) => [f.path, f.kind, formatBytes(f.sizeBytes), f.note ?? ''])}
      />

      <Subtitle1>Detection signals ({m.signals.length})</Subtitle1>
      <SimpleTable columns={['Signal', 'Value', 'Source']} rows={m.signals.map((s) => [s.name, s.value, source(s.source)])} />
    </div>
  )
}
