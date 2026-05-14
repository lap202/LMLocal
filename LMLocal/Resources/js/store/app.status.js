/**
 * CONNECTING → IDLE → [PROCESSING → THINKING → STREAMING → FINISHING → EXECUTING → RESPONDING]* → IDLE → COMPACTING → IDLE
 * 
 */

export const AppStatus = {
    // Connection states
    CONNECTING: 'CONNECTING',   // initial connection / reconnection in progress
    OFFLINE: 'OFFLINE',         // terminal, disconnected, show reload button

    // Operational states (only possible when ONLINE)
    IDLE: 'IDLE',               // terminal, ready to send, online
    PROCESSING: 'PROCESSING',   // pre-streaming (model thinking)
    THINKING: 'THINKING',       // actively receiving thinking tokens
    STREAMING: 'STREAMING',     // actively receiving tokens
    COMPACTING: 'COMPACTING',   // background history optimization
    FINISHING: 'FINISHING',     // post-processing (highlighting, copy buttons)

    // Tool execution states
    EXECUTING: 'EXECUTING',     // tool is being executed
    RESPONDING: 'RESPONDING',   // tool execution finished


    // Interruption / error states
    STOPPING: 'STOPPING',       // user requested stop, cleaning up
    ERROR: 'ERROR',             // terminal,online but an error occurred, can send message to try to recover
    CLEARING: 'CLEARING'        // clearing conversation, resetting state
};
