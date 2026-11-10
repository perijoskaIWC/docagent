/**
 * Reads a fetch() response body as Server-Sent Events and yields each event's data, parsed as JSON.
 *
 * The browser's EventSource only does GET, and POST /api/ask needs a JSON body, so the stream is read by hand.
 * The format is simple: events are separated by a blank line, and each "data:" line carries (part of) the payload.
 * Stopping early (break, or aborting the fetch) closes the connection, which cancels the model call on the server.
 */
export async function* readSse<T>(response: Response): AsyncGenerator<T> {
  if (!response.body) return
  const reader = response.body.pipeThrough(new TextDecoderStream()).getReader()
  let buffer = ''
  try {
    while (true) {
      const { value, done } = await reader.read()
      if (done) return
      buffer += value
      let end: number
      while ((end = buffer.indexOf('\n\n')) >= 0) {
        const data = dataOf(buffer.slice(0, end))
        buffer = buffer.slice(end + 2)
        if (data) yield JSON.parse(data) as T
      }
    }
  } finally {
    await reader.cancel().catch(() => undefined)
  }
}

/** The data lines of one event, joined. Other fields (event:, id:, retry:) aren't used by our API. */
function dataOf(event: string): string {
  return event
    .split('\n')
    .map((line) => line.replace(/\r$/, ''))
    .filter((line) => line.startsWith('data:'))
    .map((line) => line.slice('data:'.length).replace(/^ /, ''))
    .join('\n')
}
