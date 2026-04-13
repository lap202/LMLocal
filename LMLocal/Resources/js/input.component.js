import { AppStatus, UIText, AppSelectors } from './app.globals.js';
import createCallback from './callback.js';

/**
 * InputComponent - manages the user input area and submit controls.
 * Handles input resizing, Enter/Send events, and exposes `onClick` and `onEnter`
 * callbacks for the controller to handle send/stop behavior. Provides a `destroy`
 * method to remove DOM event listeners.
 */
const InputComponent = (() => {
    let elements = {};
    const onClick = createCallback();
    const onEnter = createCallback();

    let inputHandler = null;
    let keydownHandler = null;
    let clickHandler = null;

    function getElements() {
        return {
            userInput: document.getElementById('userInput'),
            mainBtn: document.getElementById('mainBtn')
        };
    }

    function attachEvents() {

        inputHandler = () => {
            elements.userInput.style.height = 'auto';
            if (elements.userInput.value.length > 0) {
                elements.userInput.style.height = `${elements.userInput.scrollHeight}px`;
            }
        };

        keydownHandler = async (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                if (await onEnter.emit(elements.userInput.value)) {
                    clearInput();
                }
            }
        };

        clickHandler = async () => {
            if (await onClick.emit(elements.userInput.value)) {
                clearInput();
            }
        };

        elements.userInput.addEventListener('input', inputHandler);
        elements.userInput.addEventListener('keydown', keydownHandler);
        elements.mainBtn.addEventListener('click', clickHandler);
    }

    function updateControls(state, prev) {
        if (prev &&
            state.status === prev.status &&
            AppSelectors.isBusy(state) === AppSelectors.isBusy(prev) &&
            AppSelectors.isGenerating(state) === AppSelectors.isGenerating(prev)) return;

        const isBusy = AppSelectors.isBusy(state);
        const isGenerating = AppSelectors.isGenerating(state);
        const isStopping = state.status === AppStatus.STOPPING;

        elements.userInput.disabled = isBusy;
        elements.mainBtn.disabled = isStopping || state.status === AppStatus.FINISHING;
        elements.mainBtn.textContent = isGenerating ? UIText.BUTTON_STOP : (isStopping ? UIText.BUTTON_WAIT : UIText.BUTTON_SEND);
        elements.mainBtn.className = `main-btn ${(isGenerating || isStopping) ? 'btn-stop' : ''}`;
    }

    function clearInput() {
        elements.userInput.value = '';
        elements.userInput.style.height = 'auto';
    }

    return {
        init() {
            this.destroy();
            elements = getElements();
            attachEvents();
            return this;
        },

        update(state, prev) {
            updateControls(state, prev);
        },

        onClick,
        onEnter,

        destroy() {
            if (elements.userInput) {
                if (inputHandler) {
                    elements.userInput.removeEventListener('input', inputHandler);
                    elements.userInput.removeEventListener('keydown', keydownHandler);
                    inputHandler = null;
                    keydownHandler = null;
                }
            }
            if (elements.mainBtn && clickHandler) {
                elements.mainBtn.removeEventListener('click', clickHandler);
                clickHandler = null;
            }
            elements = {};
        }
    };
})();

export default InputComponent;