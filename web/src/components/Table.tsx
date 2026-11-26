import type { ReactNode } from 'react'
import { defineMessages, useMessages } from '../i18n/messages'
import './Table.css'

const messages = defineMessages({ tr: { empty: 'Gösterilecek kayıt yok.' }, en: { empty: 'No records to show.' } })

export interface Column<T> {
  key: string
  header: string
  render: (row: T) => ReactNode
}

interface TableProps<T> {
  columns: Column<T>[]
  rows: T[]
  rowKey: (row: T) => string
  empty?: string
}

export function Table<T>({ columns, rows, rowKey, empty }: TableProps<T>) {
  const m = useMessages(messages)
  if (rows.length === 0) return <p className="table-empty">{empty ?? m.empty}</p>

  return (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            {columns.map((column) => (
              <th key={column.key} scope="col">
                {column.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={rowKey(row)}>
              {columns.map((column) => (
                <td key={column.key}>{column.render(row)}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
