import { AppStatus, UIText, Config } from './app.globals.js';
import { appSelectors } from './app.store.js';
import { createCallback } from './callback.js';

/**
 * StatusComponent
 *
 * UI component that displays application connection and token usage status.
 * The component wires DOM elements, updates visual state from the application
 * store, and exposes an `onRetry` callback for retrying connections.
 **/
class StatusComponent {
    constructor() {
        this.elements = {};
        this.totalTokens = 0;
        this.retryTimeout = null;
        this.onRetry = createCallback();
    }

    _getElements() {
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

    _updateConnectionUI(state, prev) {
        if (prev && state.status === prev.status && state.modelName === prev.modelName) return;

        const status = state.status;
        let connText = '', connClass = '';

        switch (status) {
            case AppStatus.CONNECTING:
                connText = UIText.STATUS_CONNECTING;
                connClass = 'status-label waiting';
                break;
            case AppStatus.OFFLINE:
                connText = UIText.STATUS_OFFLINE;
                connClass = 'status-label offline';
                break;
            case AppStatus.IDLE:
            case AppStatus.PROCESSING:
            case AppStatus.STREAMING:
            case AppStatus.THINKING:
            case AppStatus.FINISHING:
            case AppStatus.COMPACTING:
            case AppStatus.STOPPING:
            case AppStatus.ERROR:
                connText = UIText.STATUS_ONLINE;
                connClass = 'status-label online';
                break;
            default:
                connText = UIText.STATUS_UNKNOWN;
                connClass = 'status-label';
        }

        if (this.elements.connStatus) {
            this.elements.connStatus.textContent = connText;
            this.elements.connStatus.className = connClass;
        }
        if (this.elements.retryBtn) {
            this.elements.retryBtn.style.display = status === AppStatus.OFFLINE ? 'inline-block' : 'none';
        }

        const showModel = !(status === AppStatus.CONNECTING || status === AppStatus.OFFLINE);
        if (this.elements.modelName) {
            this.elements.modelName.style.display = showModel ? 'inline' : 'none';
            if (showModel && state.modelName !== prev?.modelName) {
                this.elements.modelName.textContent = state.modelName;
            }
        }
        if (this.elements.separator) this.elements.separator.style.display = showModel ? 'inline' : 'none';
    }

    _updateTokenStats(state, prev) {
        if (prev && state.tokenUsed === prev.tokenUsed && state.tokenSpeed === prev.tokenSpeed && state.tokenMax === prev.tokenMax) return;

        const used = state.tokenUsed ?? 0;
        const speed = state.tokenSpeed ?? 0;
        const max = state.tokenMax ?? Config.MAX_TOKENS;

        if (this.elements.tokenBarFill && this.elements.barInfoTooltip && used !== prev?.tokenUsed) {
            const percent = max > 0 ? Math.min((this.totalTokens / max) * 100, 100) : 0;
            this.elements.tokenBarFill.style.height = percent + "%";
            this.elements.barInfoTooltip.title = `Context usage: ${Math.round(percent)}%`;
        }
        if (this.elements.liveTokenCount) {
            const isWorking = appSelectors.isBusy(state);
            this.elements.liveTokenCount.style.display = isWorking ? "inline" : "none";
        }
        if (this.elements.tokenCountText && used !== prev?.tokenUsed) {
            this.elements.tokenCountText.textContent = `${used} ${UIText.TEXT_TOKENS}`;
        }
        if (this.elements.tokensSpeed && speed !== prev?.tokenSpeed) {
            this.elements.tokensSpeed.textContent = speed > 0 ? `(${speed.toFixed(1)} ${UIText.TEXT_TOKENS_PER_SECOND})` : "";
        }
    }

    _updateStatusText(state, prev) {
        if (prev && state.status === prev.status && state.error === prev.error) return;

        const status = state.status;
        this.elements.statusText.classList.remove('error');

        switch (status) {
            case AppStatus.ERROR:
                this.elements.statusText.textContent = state.error || UIText.STATUS_ERROR;
                this.elements.statusText.classList.add('error');
                break;
            case AppStatus.OFFLINE:
                if (state.error) {
                    this.elements.statusText.textContent = state.error;
                    this.elements.statusText.classList.add('error');
                } else {
                    this.elements.statusText.textContent = '';
                }
                break;
            case AppStatus.CONNECTING:
                this.elements.statusText.textContent = '';
                break;
            case AppStatus.CLEARING:
                this.elements.statusText.textContent = UIText.STATUS_CLEARING;
                break;
            default:
                const label = UIText[`STATUS_${status}`] || UIText.STATUS_UNKNOWN;
                if (label !== this.elements.statusText.textContent) {
                    this.elements.statusText.textContent = label;
                }
        }
    }

    _updateGeneratingAnimation(state, prev) {
        const isTerminalState = appSelectors.isTerminal(state);
        if (prev && isTerminalState === appSelectors.isTerminal(prev)) return;

        if (this.elements.statusBar) this.elements.statusBar.classList.toggle('generating', !isTerminalState);
    }

    _onRetryClick = async (e) => {
        if (this.retryTimeout) clearTimeout(this.retryTimeout);
        const btn = e.currentTarget;
        btn.classList.add('retry-animate');
        this.retryTimeout = setTimeout(() => {
            if (btn) btn.classList.remove('retry-animate');
            this.retryTimeout = null;
        }, 500);
        await this.onRetry.emit();
    };

    _attachEvents() {
        if (this.elements.retryBtn) {
            this.elements.retryBtn.addEventListener('click', this._onRetryClick);
        }
    }

    _detachEvents() {
        if (this.elements.retryBtn) {
            this.elements.retryBtn.removeEventListener('click', this._onRetryClick);
        }
    }

    setup() {
        this.reset();
        this.elements = this._getElements();
        this._attachEvents();
        return this;
    }

    reset() {
        this._detachEvents();
        if (this.retryTimeout) {
            clearTimeout(this.retryTimeout);
            this.retryTimeout = null;
        }
        this.elements = {};
        this.totalTokens = 0;
    }

    update(state, prev) {
        if (state.status === AppStatus.CLEARING) {
            this.totalTokens = 0;
        } else {
            const used = state.tokenUsed ?? 0;
            const prevUsed = prev?.tokenUsed ?? 0;
            if (used > prevUsed) {
                this.totalTokens += used - prevUsed;
            }
        }

        this._updateConnectionUI(state, prev);
        this._updateTokenStats(state, prev);
        this._updateStatusText(state, prev);
        this._updateGeneratingAnimation(state, prev);
    }
}

const statusComponent = new StatusComponent();
export default statusComponent;