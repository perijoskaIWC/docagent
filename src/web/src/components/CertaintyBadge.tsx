import { Badge } from '@fluentui/react-components'
import type { Certainty } from '../api/types'

/**
 * Marks statements that aren't backed by evidence: assumptions get a quiet badge (the text is also set in italics),
 * unknowns a warning, because someone has to fill them in. Evidenced statements show their evidence instead.
 */
export function CertaintyBadge({ certainty }: { certainty: Certainty }) {
  if (certainty === 'Assumption')
    return (
      <Badge appearance="tint" color="informative" size="small">
        assumption
      </Badge>
    )
  if (certainty === 'Unknown')
    return (
      <Badge appearance="filled" color="warning" size="small">
        unknown
      </Badge>
    )
  return null
}
