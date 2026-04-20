import { AppStatus, UIText } from './app.globals.js';
import { appSelectors } from './app.store.js';
import { createCallback } from './callback.js';

/**
 * InputComponent - manages the user input area and submit controls.
 * Handles input resizing, Enter/Send events, and exposes `onClick` and `onEnter`
 * callbacks for the controller to handle send/stop behavior.
 * Provides `setup` and `reset` methods to (re)initialize or clean up DOM connections.
 */
class InputComponent {
    constructor() {
        this.elements = {};
        this.isProcessing = false;
        this.onClick = createCallback();
        this.onEnter = createCallback();
    }

    _getElements() {
        return {
            inputWrapper: document.querySelector('.input-wrapper'),
            userInput: document.getElementById('userInput'),
            mainBtn: document.getElementById('mainBtn'),
            contextToggleBtn: document.getElementById('contextToggleBtn')
        };
    }

    _handleInput = () => {
        const el = this.elements.userInput;
        if (!el) return;

        el.style.height = 'auto';
        if (el.value.length > 0) {
            el.style.height = `${el.scrollHeight}px`;
        }
        if (this.elements.inputWrapper) this.elements.inputWrapper.classList.add('expanded');
    };

    _handleKeydown = async (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            const value = this.elements.userInput?.value;
            const include = this.elements.contextToggleBtn.classList.contains('active');
            if (await this.onEnter.emit(value, include)) {
                this.clearInput();
            }
        }
    };

    _handleClick = async () => {
        const value = this.elements.userInput?.value;
        const include = this.elements.contextToggleBtn.classList.contains('active');
        if (await this.onClick.emit(value, include)) {
            this.clearInput();
        }
    };

    _handleContextToggle = async (e) => {
        if (e && typeof e.preventDefault === 'function') e.preventDefault();
        this.elements.contextToggleBtn.classList.toggle('active');
    };


    _attachEvents() {
        const { userInput, mainBtn, contextToggleBtn } = this.elements;
        if (!userInput || !mainBtn) return;

        userInput.addEventListener('input', this._handleInput);
        userInput.addEventListener('keydown', this._handleKeydown);
        mainBtn.addEventListener('click', this._handleClick);
        contextToggleBtn.addEventListener('click', this._handleContextToggle);
    }

    _detachEvents() {
        const { userInput, mainBtn, contextToggleBtn } = this.elements;
        if (userInput) {
            userInput.removeEventListener('input', this._handleInput);
            userInput.removeEventListener('keydown', this._handleKeydown);
        }
        if (mainBtn) {
            mainBtn.removeEventListener('click', this._handleClick);
        }
        if (contextToggleBtn) {
            contextToggleBtn.removeEventListener('click', this._handleContextToggle);
        }
    }

    _updateControls(state, prev) {
        if (
            prev &&
            state.status === prev.status &&
            appSelectors.isBusy(state) === appSelectors.isBusy(prev) &&
            appSelectors.isGenerating(state) === appSelectors.isGenerating(prev)
        ) {
            return;
        }

        const isBusy = appSelectors.isBusy(state);
        const isGenerating = appSelectors.isGenerating(state);
        const isStopping = state.status === AppStatus.STOPPING;

        this.elements.userInput.disabled = isBusy;
        this.elements.mainBtn.disabled = isStopping || state.status === AppStatus.FINISHING;
        this.elements.mainBtn.textContent = isGenerating
            ? UIText.BUTTON_STOP
            : isStopping
                ? UIText.BUTTON_WAIT
                : UIText.BUTTON_SEND;
        this.elements.mainBtn.className = `main-btn ${isGenerating || isStopping ? 'btn-stop' : ''}`;
    }

    clearInput() {
        const el = this.elements.userInput;
        if (!el) return;
        el.value = '';
        el.style.height = 'auto';
        if (this.elements.inputWrapper) this.elements.inputWrapper.classList.remove('expanded');
        if (this.elements.contextToggleBtn) this.elements.contextToggleBtn.classList.remove('active');
    }

    setup() {
        this.reset();
        this.elements = this._getElements();
        if (!this.elements.userInput || !this.elements.mainBtn) {
            console.error('InputComponent setup failed: required elements not found');
            return this;
        }
        this._attachEvents();
        return this;
    }

    update(state, prev) {
        if (this.elements.userInput && this.elements.mainBtn) {
            this._updateControls(state, prev);
        }
    }

    reset() {
        this._detachEvents();
        this.clearInput();
        this.elements = {};
    }
}

const inputComponent = new InputComponent();
export default inputComponent;