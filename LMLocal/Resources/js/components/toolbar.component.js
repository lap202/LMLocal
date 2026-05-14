import { createCallback } from '@app/lib/callback.js';

class ToolbarComponent {
    constructor() {
        this.elements = {};
        this.onModelNameClick = createCallback();
    }

    _getElements() {
        return {
            modelName: document.getElementById('model-name'),
            separator: document.getElementById('status-separator'),
            tokenBarFill: document.getElementById('token-bar-fill'),
            barInfoTooltip: document.getElementById('info-tooltip')
        };
    }

    _updateModelName(modelState, prevModelState) {
        if (modelState.modelName === prevModelState?.modelName) return;

        if (this.elements.modelName && modelState.modelName) {
            this.elements.modelName.textContent = modelState.modelName;
        } else {
            this.elements.modelName.textContent = 'Select model...';
        }
    }

    _updateTokenBar(modelState, prevModelState) {
        if (modelState.tokenMax === prevModelState?.tokenMax &&
            modelState.tokenUsed === prevModelState?.tokenUsed &&
            modelState.supportsMaxTokens === prevModelState?.supportsMaxTokens) return;

        if (!modelState.supportsMaxTokens) {
            this.elements.tokenBarFill.style.display = 'none';

            const text = `Token usage: ${modelState.tokenUsed}`;
            this.elements.barInfoTooltip.title = text;
            this.elements.modelName.title = text;
            this.elements.tokenBarFill.style.transform = `scaleY(0)`;

            return;
        }

        this.elements.tokenBarFill.style.display = 'block';
        const percent = modelState.tokenMax > 0
            ? Math.min((modelState.tokenUsed / modelState.tokenMax) * 100, 100)
            : 0;
        this.elements.tokenBarFill.style.transform = `scaleY(${percent / 100})`;

        const text = `Context usage: ${Math.round(percent)}%, ${modelState.tokenUsed}/${modelState.tokenMax}`;
        this.elements.barInfoTooltip.title = text;
        this.elements.modelName.title = text;
    }

    _onModelNameClick = async (e) => {
        e.stopPropagation();
        this.elements.modelName.classList.add('generating');
        await this.onModelNameClick.emitResult();
        this.elements.modelName.classList.remove('generating');
    };

    _attachEvents() {
        if (this.elements.modelName) {
            this.elements.modelName.addEventListener('click', this._onModelNameClick);
            this.elements.modelName.style.cursor = 'pointer';
        }
    }

    _detachEvents() {
        if (this.elements.modelName) {
            this.elements.modelName.removeEventListener('click', this._onModelNameClick);
        }
    }

    setup() {
        this.elements = this._getElements();
        this._attachEvents();

        return this;
    }

    reset() {
        this._detachEvents();
        this.elements = {};
    }

    updateModelState(modelState, prevModelState) {
        this._updateModelName(modelState, prevModelState);
        this._updateTokenBar(modelState, prevModelState);
    }
}

const toolbarComponent = new ToolbarComponent();
export { toolbarComponent };
