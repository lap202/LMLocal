import { HighlightWorkerClient } from '@app/workers/highlight/highlight.worker.client.js';
/**
 * Factory function that returns a new `HighlightWorkerClient` instance.
 * Use this to obtain a client for highlighting code blocks in a DOM container.
 */
export const createHighlightParser = () => new HighlightWorkerClient();