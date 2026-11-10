import { Field, Select, makeStyles, tokens } from '@fluentui/react-components'
import { useStrategies } from '../api/hooks'
import type { StrategySlot } from '../api/types'

const useStyles = makeStyles({
  row: { display: 'flex', flexWrap: 'wrap', gap: tokens.spacingHorizontalL },
  field: { minWidth: '200px' },
})

/** The chosen key per slot; a missing slot means the empty option ("Default", "Any", "Same as this run"). */
export type StrategyChoice = Partial<Record<StrategySlot, string>>

const slots: { slot: StrategySlot; label: string; hint: string }[] = [
  { slot: 'detector', label: 'Detector', hint: 'E1: how the system is recognised' },
  { slot: 'author', label: 'Author', hint: 'E2: the agent architecture' },
  { slot: 'retrieval', label: 'Retrieval', hint: 'E3: when the agents get knowledge' },
]

interface StrategyPickerProps {
  value: StrategyChoice
  onChange: (value: StrategyChoice) => void
  /** The label of the empty option, which leaves the slot unset. */
  emptyLabel: string
}

/** One dropdown per experiment slot, filled from GET /api/strategies. Fake strategies are marked. */
export function StrategyPicker({ value, onChange, emptyLabel }: StrategyPickerProps) {
  const styles = useStyles()
  const strategies = useStrategies()

  return (
    <div className={styles.row}>
      {slots.map(({ slot, label, hint }) => (
        <Field key={slot} label={label} hint={hint} className={styles.field}>
          <Select
            value={value[slot] ?? ''}
            disabled={!strategies.data}
            onChange={(_, data) => onChange({ ...value, [slot]: data.value || undefined })}
          >
            <option value="">{emptyLabel}</option>
            {(strategies.data?.[slot] ?? []).map((option) => (
              <option key={option.key} value={option.key}>
                {option.key}
                {option.isFake ? ' (fake)' : ''}
              </option>
            ))}
          </Select>
        </Field>
      ))}
    </div>
  )
}
