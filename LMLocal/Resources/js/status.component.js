import { AppStatus, UIText, CONFIG, AppSelectors } from './app.globals.js';
import createCallback from './callback.js';

/**
 * StatusComponent - updates connection and generation status UI.
 * Renders connection label, model name, token usage bar and speed,
 * shows status text and exposes an `onRetry` callback for retry actions.
 */
const StatusComponent = (() => {
    let elements = {};
    let retryHandler = null;
    let totalTokens = 0;
    const onRetry = createCallback();

    function getElements() {
        return {
            connStatus: document.getElementById('conn-status'),
            retryBtn: document.getElementById('retry-btn'),
            modelName: document.getElementById('model-name'),
            separator: document.getElementById('status-separator'),
            tokenBarFill: document.getElementById('token-bar-fill'),
            barInfoTooltip: document.getElementById('info-tooltip'),
            liveTokenCount: document.getElementById('live-token-count'),
            tokenCountText: document.getElementById('token-number'),
            tokensSpeed: document.getElementById('tokens-speed'),
            statusText: document.getElementById('status-text'),
            statusBar: document.getElementById('status-bar')
        };
    }

    function updateConnectionUI(state, prev) {
        if (prev && state.status === prev.status && state.modelName === prev.modelName) return;

        const status = state.status;
        let connText = '', connClass = '';

        if (status === AppStatus.CONNECTING) {
            connText = UIText.STATUS_CONNECTING;
            connClass = 'status-label waiting';
        } else if (status === AppStatus.OFFLINE) {
            connText = UIText.STATUS_OFFLINE;
            connClass = 'status-label offline';
        } else if ([
            AppStatus.IDLE, AppStatus.PROCESSING, AppStatus.STREAMING, AppStatus.THINKING,
            AppStatus.FINISHING, AppStatus.COMPACTING, AppStatus.STOPPING, AppStatus.ERROR
        ].includes(status)) {
            connText = UIText.STATUS_ONLINE;
            connClass = 'status-label online';
        } else {
            connText = UIText.STATUS_UNKNOWN;
            connClass = 'status-label';
        }

        if (elements.connStatus) {
            elements.connStatus.textContent = connText;
            elements.connStatus.className = connClass;
        }
        if (elements.retryBtn) {
            elements.retryBtn.style.display = status === AppStatus.OFFLINE ? 'inline-block' : 'none';
        }

        const showModel = !(status === AppStatus.CONNECTING || status === AppStatus.OFFLINE);
        if (elements.modelName) {
            elements.modelName.style.display = showModel ? 'inline' : 'none';
            if (showModel && state.modelName !== prev?.modelName) {
                elements.modelName.textContent = state.modelName;
            }
        }
        if (elements.separator) elements.separator.style.display = showModel ? 'inline' : 'none';
    }

    function updateTokenStats(state, prev) {
        if (prev && state.tokenUsed === prev.tokenUsed && state.tokenSpeed === prev.tokenSpeed && state.tokenMax === prev.tokenMax) return;

        const used = state.tokenUsed ?? 0;
        const speed = state.tokenSpeed ?? 0;
        const max = state.tokenMax ?? CONFIG.MAX_TOKENS;

        if (elements.tokenBarFill && elements.barInfoTooltip && used !== prev?.tokenUsed) {
            const percent = max > 0 ? Math.min((totalTokens / max) * 100, 100) : 0;
            elements.tokenBarFill.style.height = percent + "%";
            elements.barInfoTooltip.title = `Context usage: ${Math.round(percent)}%`;
        }
        if (elements.liveTokenCount) {
            const isWorking = AppSelectors.isBusy(state);
            elements.liveTokenCount.style.display = isWorking ? "inline" : "none";
        }
        if (elements.tokenCountText && used !== prev?.tokenUsed) {
            elements.tokenCountText.textContent = `${used} ${UIText.TEXT_TOKENS}`;
        }
        if (elements.tokensSpeed && speed !== prev?.tokenSpeed) {
            elements.tokensSpeed.textContent = speed > 0 ? `(${speed.toFixed(1)} ${UIText.TEXT_TOKENS_PER_SECOND})` : "";
        }
    }

    function updateStatusText(state, prev) {
        if (prev && state.status === prev.status && state.error === prev.error) return;

        if (state.status === AppStatus.ERROR) {
            elements.statusText.textContent = state.error || UIText.STATUS_ERROR;
            elements.statusText.classList.add('error');
        } else if (state.status === AppStatus.OFFLINE && state.error) {
            elements.statusText.textContent = state.error;
            elements.statusText.classList.add('error');
        } else if ([AppStatus.CONNECTING, AppStatus.OFFLINE].includes(state.status)) {
            elements.statusText.textContent = '';
            elements.statusText.classList.remove('error');
        } else if (state.status === AppStatus.CLEARING) {
            elements.statusText.textContent = UIText.STATUS_CLEARING;
            elements.statusText.classList.remove('error');
        } else {
            elements.statusText.classList.remove('error');
            const label = UIText[`STATUS_${state.status}`] || UIText.STATUS_UNKNOWN;
            if (label !== elements.statusText.textContent) {
                elements.statusText.textContent = label;
            }
        }
    }

    function updateGeneratingAnimation(state, prev) {
        const isTerminalState = AppSelectors.isTerminal(state);
        if (prev && isTerminalState === AppSelectors.isTerminal(prev)) return;

        if (elements.statusBar) elements.statusBar.classList.toggle('generating', !isTerminalState);
    }

    async function onRetryClick(e) {
        const btn = e.currentTarget;
        btn.classList.add('retry-animate');
        setTimeout(() => {
            btn.classList.remove('retry-animate');
        }, 500);
        await onRetry.emit();
    }

    return {
        init() {
            this.destroy();

            elements = getElements();
            if (elements.retryBtn) {
                retryHandler = onRetryClick;
                elements.retryBtn.addEventListener('click', retryHandler);
            }
            return this;
        },

        update(state, prev) {
            if (state.status === AppStatus.CLEARING) {
                totalTokens = 0;
            } else {
                const used = state.tokenUsed ?? 0;
                const prevUsed = prev?.tokenUsed ?? 0;
                if (used > prevUsed) {
                    totalTokens += used - prevUsed;
                }
            }

            updateConnectionUI(state, prev);
            updateTokenStats(state, prev);
            updateStatusText(state, prev);
            updateGeneratingAnimation(state, prev);
        },

        onRetry,

        destroy() {
            if (elements.retryBtn && retryHandler) {
                elements.retryBtn.removeEventListener('click', retryHandler);
                retryHandler = null;
            }
            elements = {};
        }
    };
})();

export default StatusComponent;