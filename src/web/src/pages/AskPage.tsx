import { Caption1, Title2, makeStyles, tokens } from '@fluentui/react-components'
import { ChatPanel } from '../components/ChatPanel'

const useStyles = makeStyles({
  page: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
})

/** The chat without a run: questions over the knowledge base only. */
export function AskPage() {
  const styles = useStyles()
  return (
    <div className={styles.page}>
      <Title2>Ask</Title2>
      <Caption1>Questions over the knowledge base. To ask about one run, use the Ask tab on that run.</Caption1>
      <ChatPanel placeholder="For example: how do we usually integrate a file-based system?" />
    </div>
  )
}
