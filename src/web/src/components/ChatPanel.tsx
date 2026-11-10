import {
  Body1, Button, Caption1, MessageBar, MessageBarBody, Spinner, Tag, Textarea, makeStyles, mergeClasses, tokens,
} from '@fluentui/react-components'
import { ArrowResetRegular, DismissCircleRegular, SendRegular } from '@fluentui/react-icons'
import { useEffect, useRef, useState } from 'react'
import { useChat } from '../api/hooks'

const useStyles = makeStyles({
  panel: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM, maxWidth: '860px' },
  messages: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
  bubble: {
    maxWidth: '80%',
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
    borderRadius: tokens.borderRadiusLarge,
    whiteSpace: 'pre-wrap',
  },
  user: { alignSelf: 'flex-end', backgroundColor: tokens.colorBrandBackground2 },
  assistant: { alignSelf: 'flex-start', backgroundColor: tokens.colorNeutralBackground3 },
  citations: { display: 'flex', flexWrap: 'wrap', gap: tokens.spacingHorizontalXS, marginTop: tokens.spacingVerticalS },
  input: { display: 'flex', gap: tokens.spacingHorizontalS, alignItems: 'flex-end' },
  textarea: { flexGrow: 1 },
})

interface ChatPanelProps {
  /** Ask about this run; without it, questions go to the knowledge base only. */
  runId?: string
  placeholder: string
}

/** A conversation with POST /api/ask. Answers appear word by word as the stream arrives; citations come at the end. */
export function ChatPanel({ runId, placeholder }: ChatPanelProps) {
  const styles = useStyles()
  const chat = useChat(runId)
  const [question, setQuestion] = useState('')
  const end = useRef<HTMLDivElement>(null)

  // Braces, so the effect returns nothing. Without them it would return scrollIntoView's result (a Promise in newer
  // browsers), React would take that for the cleanup function, and the page would crash when the panel unmounts.
  useEffect(() => {
    end.current?.scrollIntoView({ block: 'nearest' })
  }, [chat.entries])

  const submit = () => {
    const text = question.trim()
    if (!text || chat.busy) return
    setQuestion('')
    void chat.send(text)
  }

  return (
    <div className={styles.panel}>
      <div className={styles.messages}>
        {chat.entries.length === 0 && <Caption1>No questions yet.</Caption1>}
        {chat.entries.map((entry, index) => (
          <div key={index} className={mergeClasses(styles.bubble, entry.role === 'user' ? styles.user : styles.assistant)}>
            <Body1>{entry.text}</Body1>
            {entry.streaming && <Spinner size="extra-tiny" />}
            {entry.citations.length > 0 && (
              <div className={styles.citations}>
                {entry.citations.map((citation) => (
                  <Tag key={citation.ref} size="small" appearance="brand" title={citation.ref}>
                    {citation.label}
                  </Tag>
                ))}
              </div>
            )}
          </div>
        ))}
        <div ref={end} />
      </div>

      {chat.error && (
        <MessageBar intent="error">
          <MessageBarBody>{chat.error}</MessageBarBody>
        </MessageBar>
      )}

      <div className={styles.input}>
        <Textarea
          className={styles.textarea}
          value={question}
          placeholder={placeholder}
          resize="vertical"
          onChange={(_, data) => setQuestion(data.value)}
          onKeyDown={(event) => {
            // Enter sends; Shift+Enter starts a new line.
            if (event.key === 'Enter' && !event.shiftKey) {
              event.preventDefault()
              submit()
            }
          }}
        />
        {chat.busy ? (
          <Button icon={<DismissCircleRegular />} onClick={chat.stop}>
            Stop
          </Button>
        ) : (
          <Button appearance="primary" icon={<SendRegular />} disabled={!question.trim()} onClick={submit}>
            Ask
          </Button>
        )}
        <Button icon={<ArrowResetRegular />} disabled={chat.entries.length === 0} onClick={chat.reset}>
          New conversation
        </Button>
      </div>
    </div>
  )
}
