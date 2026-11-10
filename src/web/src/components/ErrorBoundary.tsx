import { MessageBar, MessageBarBody, MessageBarTitle } from '@fluentui/react-components'
import { Component } from 'react'
import type { ErrorInfo, ReactNode } from 'react'

interface ErrorBoundaryProps {
  children: ReactNode
}

interface ErrorBoundaryState {
  error: Error | null
}

/**
 * Catches a crash while a page renders and shows the error. Without it, React removes the whole page and leaves it
 * white. Error boundaries can only be class components; React has no hook for this.
 */
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { error: null }

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { error }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('DocAgent page crashed:', error, info.componentStack)
  }

  render() {
    if (!this.state.error) return this.props.children
    return (
      <div style={{ padding: 24 }}>
        <MessageBar intent="error" layout="multiline">
          <MessageBarBody>
            <MessageBarTitle>Something broke on this page</MessageBarTitle>
            {this.state.error.message}
          </MessageBarBody>
        </MessageBar>
        <pre style={{ whiteSpace: 'pre-wrap', fontSize: 12 }}>{this.state.error.stack}</pre>
      </div>
    )
  }
}
