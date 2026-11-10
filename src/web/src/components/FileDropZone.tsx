import { Body1, Button, Caption1, makeStyles, mergeClasses, shorthands, tokens } from '@fluentui/react-components'
import { ArrowUploadRegular, DismissRegular, DocumentRegular } from '@fluentui/react-icons'
import { useRef, useState } from 'react'
import type { DragEvent, KeyboardEvent } from 'react'
import { formatBytes } from '../format'

const useStyles = makeStyles({
  zone: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    gap: tokens.spacingVerticalS,
    padding: tokens.spacingVerticalXXL,
    cursor: 'pointer',
    ...shorthands.border(tokens.strokeWidthThick, 'dashed', tokens.colorNeutralStroke1),
    ...shorthands.borderRadius(tokens.borderRadiusLarge),
    ':hover': { backgroundColor: tokens.colorNeutralBackground1Hover },
  },
  dragging: {
    ...shorthands.borderColor(tokens.colorBrandStroke1),
    backgroundColor: tokens.colorBrandBackground2,
  },
  icon: { fontSize: '32px', color: tokens.colorBrandForeground1 },
  files: { listStyleType: 'none', padding: 0, margin: `${tokens.spacingVerticalS} 0 0` },
  file: { display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS },
})

interface FileDropZoneProps {
  files: File[]
  onChange: (files: File[]) => void
  /** The file input's accept list, e.g. ".md,.txt,.pdf". */
  accept: string
  hint: string
}

/** Drop files here or click to pick them. Picking a file with the same name again replaces it. */
export function FileDropZone({ files, onChange, accept, hint }: FileDropZoneProps) {
  const styles = useStyles()
  const input = useRef<HTMLInputElement>(null)
  const [dragging, setDragging] = useState(false)

  const add = (picked: FileList | null) => {
    if (!picked) return
    const byName = new Map(files.map((file) => [file.name, file]))
    for (const file of Array.from(picked)) byName.set(file.name, file)
    onChange([...byName.values()])
  }

  const onDrop = (event: DragEvent) => {
    event.preventDefault()
    setDragging(false)
    add(event.dataTransfer.files)
  }

  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault()
      input.current?.click()
    }
  }

  return (
    <div>
      <div
        role="button"
        tabIndex={0}
        className={mergeClasses(styles.zone, dragging && styles.dragging)}
        onClick={() => input.current?.click()}
        onKeyDown={onKeyDown}
        onDragOver={(event) => {
          event.preventDefault()
          setDragging(true)
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={onDrop}
      >
        <ArrowUploadRegular className={styles.icon} />
        <Body1>Drop files here, or click to choose</Body1>
        <Caption1>{hint}</Caption1>
        <input
          ref={input}
          type="file"
          multiple
          accept={accept}
          hidden
          onChange={(event) => {
            add(event.target.files)
            event.target.value = ''   // so choosing the same file again still fires onChange
          }}
        />
      </div>
      {files.length > 0 && (
        <ul className={styles.files}>
          {files.map((file) => (
            <li key={file.name} className={styles.file}>
              <DocumentRegular />
              <Body1>{file.name}</Body1>
              <Caption1>{formatBytes(file.size)}</Caption1>
              <Button
                appearance="subtle"
                size="small"
                icon={<DismissRegular />}
                aria-label={`Remove ${file.name}`}
                onClick={() => onChange(files.filter((other) => other !== file))}
              />
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
