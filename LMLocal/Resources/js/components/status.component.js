import { AppStatus, UIText } from '@app/store/app.globals.js';
import { appSelectors } from '@app/store/app.selectors.js';
import { createCallback } from '@app/lib/callback.js';

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
        this.tokenMax = 0;
        this.retryTimeout = null;
        this.connectTimeout = null;
        this.onRetry = createCallback();
        this.onConnect = createCallback();
    }

    _getElements() {
        return {
            connStatus: document.getElementById('conn-status'),
            retryBtn: document.getElementById('retry-btn'),
            connectBtn: document.getElementById('connect-btn'),
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

    _updateConnectionStatus(status, error, prevStatus) {
        if (prevStatus !== undefined && status === prevStatus) return;

        let connText = '', connClass = '';

        switch (status) {
            case AppStatus.CONNECTING:
                connText = UIText.STATUS_CONNECTING;
                connClass = 'status-label waiting';
                break;
            case AppStatus.OFFLINE:
                connText = UIText.STATUS_OFFLINE;
                if (error) {
                    connClass = 'status-label offline';
                } else {
                    connClass = 'status-label waiting';
                }
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
        if (this.elements.retryBtn && this.elements.connectBtn) {
            if (status === AppStatus.OFFLINE) {
                if (error) {
                    this.elements.retryBtn.style.display = 'inline-block';
                    this.elements.connectBtn.style.display = 'none';
                } else {
                    this.elements.retryBtn.style.display = 'none';
                    this.elements.connectBtn.style.display = 'inline-block';
                }
            } else {
                this.elements.retryBtn.style.display = 'none';
                this.elements.connectBtn.style.display = 'none';
            }
        } else if (this.elements.retryBtn) {
            this.elements.retryBtn.style.display = status === AppStatus.OFFLINE ? 'inline-block' : 'none';
        }
    }

    _updateModelVisibility(status) {
        const showModel = !(status === AppStatus.CONNECTING || status === AppStatus.OFFLINE);
        if (this.elements.modelName) {
            this.elements.modelName.style.display = showModel ? 'inline' : 'none';
        }
        if (this.elements.separator) this.elements.separator.style.display = showModel ? 'inline' : 'none';
    }

    _updateModelNameText(modelName, prevModelName) {
        if (prevModelName !== undefined && modelName === prevModelName) return;

        if (this.elements.modelName && modelName) {
            this.elements.modelName.textContent = modelName;
        }
    }

    _updateTokenBar(prevTokenMax) {
        if (this.elements.tokenBarFill && this.elements.barInfoTooltip) {
            const percent = this.tokenMax > 0 ? Math.min((this.totalTokens / this.tokenMax) * 100, 100) : 0;
            this.elements.tokenBarFill.style.transform = `scaleY(${percent / 100})`;
            this.elements.barInfoTooltip.title = `Context usage: ${Math.round(percent)}%`;
        }
    }

    _updateTokenCounter(tokenUsed, tokenSpeed, status, prevTokenUsed, prevTokenSpeed) {
        if (prevTokenUsed !== undefined && tokenUsed === prevTokenUsed && tokenSpeed === prevTokenSpeed) return;

        if (this.elements.liveTokenCount) {
            const isWorking = appSelectors.isBusy({ status });
            this.elements.liveTokenCount.style.display = isWorking ? "inline" : "none";
        }
        if (this.elements.tokenCountText && tokenUsed !== prevTokenUsed) {
            this.elements.tokenCountText.textContent = tokenUsed > 0 ? `${tokenUsed} ${UIText.TEXT_TOKENS}` : "";
        }
        if (this.elements.tokensSpeed && tokenSpeed !== prevTokenSpeed) {
            this.elements.tokensSpeed.textContent = tokenSpeed > 0 ? `(${tokenSpeed.toFixed(1)} ${UIText.TEXT_TOKENS_PER_SECOND})` : "";
        }
    }

    _updateStatusText(state, prev) {
        if (prev && state.status === prev.status && state.error === prev.error) return;
        if (!this.elements.statusText) return;

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

    _onConnectClick = async (e) => {
        if (this.connectTimeout) clearTimeout(this.connectTimeout);
        const btn = e.currentTarget;
        btn.classList.add('retry-animate');
        this.connectTimeout = setTimeout(() => {
            if (btn) btn.classList.remove('retry-animate');
            this.connectTimeout = null;
        }, 500);
        await this.onConnect.emit();
    };

    _attachEvents() {
        if (this.elements.retryBtn) {
            this.elements.retryBtn.addEventListener('click', this._onRetryClick);
        }
        if (this.elements.connectBtn) {
            this.elements.connectBtn.addEventListener('click', this._onConnectClick);
        }
    }

    _detachEvents() {
        if (this.elements.retryBtn) {
            this.elements.retryBtn.removeEventListener('click', this._onRetryClick);
        }
        if (this.elements.connectBtn) {
            this.elements.connectBtn.removeEventListener('click', this._onConnectClick);
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
        if (this.connectTimeout) {
            clearTimeout(this.connectTimeout);
            this.connectTimeout = null;
        }
        this.elements = {};
        this.totalTokens = 0;
        this.tokenMax = 0;
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

        this._updateConnectionStatus(state.status, state.error, prev?.status);
        this._updateModelVisibility(state.status);
        this._updateTokenCounter(state.tokenUsed ?? 0, state.tokenSpeed ?? 0, state.status, prev?.tokenUsed, prev?.tokenSpeed);
        this._updateTokenBar(prev?.tokenMax);
        this._updateStatusText(state, prev);
        this._updateGeneratingAnimation(state, prev);
    }

    updateModelName(modelName, prevModelName) {
        this._updateModelNameText(modelName, prevModelName);
    }

    updateModelContext(tokenMax, prevTokenMax) {
        this.tokenMax = tokenMax;
        this._updateTokenBar(prevTokenMax);
    }
}

const statusComponent = new StatusComponent();
export { statusComponent };