"use strict";

import AppManager from './app.manager.js';
import AppController from './app.controller.js';
import MessageRenderer from './message.renderer.js';

window.lmInit = async () => {
    try {
        if (typeof AppController !== 'undefined' && AppController.init && !AppController.initialized) {
            // Call init if it hasn't been called yet (for tests, for example)
            await AppController.init();
        }
        await AppManager.onAppInit();

        /* Choose the Markdown renderer for message content.
           Options:
             - MessageRenderer.RendererType.MARKED
               Renders the complete Markdown after the message finishes.
             - MessageRenderer.RendererType.STREAMING_MARKDOWN
               Renders Markdown progressively as content arrives (useful for streaming responses / low-latency UX).
           Default here is STREAMING_MARKDOWN to enable incremental rendering of streamed messages.
        */

        MessageRenderer.setRenderer(MessageRenderer.RendererType.STREAMING_MARKDOWN);

    } catch (error) {
        console.error('Error during app initialization:', error);
    }
};