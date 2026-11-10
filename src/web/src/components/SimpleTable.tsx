import { Caption1, Table, TableBody, TableCell, TableHeader, TableHeaderCell, TableRow } from '@fluentui/react-components'
import type { ReactNode } from 'react'

interface SimpleTableProps {
  columns: string[]
  rows: ReactNode[][]
  /** Shown instead of an empty table. */
  empty?: string
}

/** A read-only Fluent table from column names and cell contents. */
export function SimpleTable({ columns, rows, empty = 'None found.' }: SimpleTableProps) {
  if (rows.length === 0) return <Caption1>{empty}</Caption1>
  return (
    <Table size="small">
      <TableHeader>
        <TableRow>
          {columns.map((column) => (
            <TableHeaderCell key={column}>{column}</TableHeaderCell>
          ))}
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((cells, row) => (
          <TableRow key={row}>
            {cells.map((cell, column) => (
              <TableCell key={column}>{cell}</TableCell>
            ))}
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
