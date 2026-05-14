import { AppStatus } from '@app/store/app.status.js';

export const appSelectors = {
    isTerminal: (status) => [AppStatus.OFFLINE, AppStatus.IDLE, AppStatus.ERROR].includes(status),
    // Busy if not IDLE (includes STOPPING, CONNECTING, OFFLINE, etc.)
    isBusy: (status) => ![AppStatus.IDLE, AppStatus.ERROR, AppStatus.OFFLINE].includes(status),
    // Generating only during active token flow
    isGenerating: (status) => [AppStatus.PROCESSING, AppStatus.THINKING, AppStatus.STREAMING, AppStatus.EXECUTING, AppStatus.RESPONDING, AppStatus.FINISHING].includes(status),
    // Can send requests when online
    canSend: (status) => [AppStatus.IDLE, AppStatus.ERROR].includes(status),
};
