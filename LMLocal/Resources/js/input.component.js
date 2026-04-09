import { AppStatus, UIText, AppSelectors } from './app.globals.js';
import { createCallback } from './callback.js';

const InputComponent = (() => {
    let elements = {};
    const onClick = createCallback();
    const onEnter = createCallback();
    function getElements() {
        return {
            userInput: document.getElementById('userInput'),
            mainBtn: document.getElementById('mainBtn')
        };
    }
    function attachEvents() {
        elements.userInput.addEventListener('input', () => {
            elements.userInput.style.height = 'auto';
            if (elements.userInput.value.length > 0) {
                elements.userInput.style.height = `${elements.userInput.scrollHeight}px`;
            }
        });

        elements.userInput.addEventListener('keydown', async (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                if (await onEnter.emit(elements.userInput.value)) {
                    clearInput();
                }
            }
        });

        elements.mainBtn.addEventListener('click', async () => {
            if (await onClick.emit(elements.userInput.value)) {
                clearInput();
            }
        });
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
            elements = getElements();
            attachEvents();
            return this;
        },
        update(state, prev) {
            updateControls(state, prev);
        },
        onClick: onClick,
        onEnter: onEnter
    };
})();

export default InputComponent;