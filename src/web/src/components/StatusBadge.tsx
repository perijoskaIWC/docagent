import { Badge } from '@fluentui/react-components'
import type { BadgeProps } from '@fluentui/react-components'
import type { RunStatus } from '../api/types'

function colorOf(status: RunStatus): BadgeProps['color'] {
  if (status === 'Completed') return 'success'
  if (status === 'Failed') return 'danger'
  if (status === 'Queued') return 'informative'
  return 'brand'   // one of the running steps
}

export function StatusBadge({ status }: { status: RunStatus }) {
  return (
    <Badge appearance="tint" color={colorOf(status)}>
      {status}
    </Badge>
  )
}
