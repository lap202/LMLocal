import { createCallback } from '@app/lib/callback.js';

export class ModelSelectorDialog {
    constructor(models = []) {
        this.modelsList = models;
        this.dialog = null;
        this.containerElement = null;
        this.refreshBtn = null;
        this.closeBtn = null;
        this.isLoading = false;
        this.selectedModel = null;

        this.onLoad = createCallback();
        this.onSelect = createCallback();

        // Store handler references for proper event cleanup
        this._onRefreshClick = null;
        this._onCloseClick = null;
    }

    _getElements() {
        return {
            dialog: document.getElementById('model-selector-dialog'),
            container: document.getElementById('models-list-container'),
            refreshBtn: document.getElementById('model-refresh-top'),
            closeBtn: document.getElementById('model-selector-close')
        };
    }

    async _loadModels() {
        if (this.isLoading) return;

        this.isLoading = true;
        try {
            this._showLoadingState();

            const result = await this.onLoad.emitResult();

            if (!result.success) {
                this._showErrorState(result.error ? result.error.message : 'Failed to load models');
                return;
            }

            const response = result.data || {};
            const models = Array.isArray(response.models) ? response.models : [];

            if (models.length > 0) {
                this.modelsList = models;
                this._renderModels();
            } else {
                this._showEmptyState();
            }
        } catch (error) {
            console.error('Failed to load models:', error);
            this._showErrorState(`Failed to load models: ${error.message}`);
        } finally {
            this.isLoading = false;
        }
    }

    /**
     * Display loading placeholder
     */
    _showLoadingState() {
        if (this.containerElement) {
            this.containerElement.innerHTML = `
                <div class="loading-placeholder">
                    <div class="spinner"></div>
                    <span>Fetching models from endpoint...</span>
                </div>
            `;
        }
    }

    /**
     * Display error message
     */
    _showErrorState(errorMessage) {
        if (this.containerElement) {
            this.containerElement.innerHTML = `
                <div class="error-placeholder">
                    <span style="color: var(--danger-color); padding: 20px;">Error: ${this._escapeHtml(errorMessage)}</span>
                </div>
            `;
        }
    }

    /**
     * Display empty state when no models found
     */
    _showEmptyState() {
        if (this.containerElement) {
            this.containerElement.innerHTML = `
                <div class="empty-placeholder">
                    <span style="padding: 20px;">No models available</span>
                </div>
            `;
        }
    }

    /**
     * Render models in the grid.
     * Each model displays:
     * - id (required)
     * - name (optional, display name from provider)
     * - maxTokens (optional, context length)
     * - isActive (indicates if model is loaded/active)
     */
    _renderModels() {
        if (!this.containerElement || !this.modelsList.length) {
            this._showEmptyState();
            return;
        }

        const modelsHtml = this.modelsList.map(model => {
            const modelId = model.id || 'unknown';
            const modelName = model.name || modelId;

            const maxTokensDisplay = model.maxTokens ? `${model.maxTokens} context` : '';

            const isSelected = model.id === this.currentModelId;

            const statusClass = model.isActive ? 'status-loaded' : 'status-unloaded';
            const statusText = model.isActive ? 'Active' : 'Inactive';

            return `
        <div class="model-card ${isSelected ? 'active' : ''}" data-model-id="${this._escapeHtml(modelId)}">
            <div class="model-card-header">
                <div class="model-name">${this._escapeHtml(modelName)}</div>
                <div class="model-status-badge ${statusClass}">${statusText}</div>
            </div>
            <div class="model-id">${this._escapeHtml(modelId)}</div>
            
            <div class="model-metadata">
                ${maxTokensDisplay ? `
                    <div class="model-tokens">
                        ${this._escapeHtml(maxTokensDisplay)}
                    </div>` : ''}
                
                ${model.supportsToolUse !== null ? `
                    <div class="model-tooluse ${model.supportsToolUse ? 'model-tooluse-active' : 'model-tooluse-none'}">
                        ${model.supportsToolUse ? 'Tool Use: Yes' : 'Tool Use: No'}
                    </div>` : ''}
            </div>
        </div>
    `;
        }).join('');

        this.containerElement.innerHTML = `<div class="models-grid">${modelsHtml}</div>`;
        this._attachModelCardHandlers();
    }

    /**
     * Attach click handlers to model cards
     */
    _attachModelCardHandlers() {
        const cards = this.containerElement.querySelectorAll('.model-card');
        cards.forEach(card => {
            card.addEventListener('click', async (e) => {
                e.stopPropagation();
                const modelId = card.dataset.modelId;
                const model = this.modelsList.find(m => m.id === modelId);
                if (model) {
                    await this._selectModel(model);
                }
            });
        });
    }

    async _selectModel(model) {
        try {
            const success = await this.onSelect.emitResult(model);
            if (success) {
                this.selectedModel = model;
                if (this.dialog) {
                    this.dialog.close();
                }
            } else {
                this._showErrorState('Failed to set active model');
            }
        } catch (error) {
            console.error('Model selection failed:', error);
            this._showErrorState(`Model selection failed: ${error.message}`);
        }
    }

    /**
     * Escape HTML special characters
     */
    _escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Attach event handlers
     */
    _attachEvents() {
        this._onRefreshClick = (e) => {
            e.stopPropagation();
            this._loadModels();
        };

        this._onCloseClick = (e) => {
            e.stopPropagation();
            if (this.dialog) {
                this.dialog.close();
            }
        };

        if (this.refreshBtn) {
            this.refreshBtn.addEventListener('click', this._onRefreshClick);
        }

        if (this.closeBtn) {
            this.closeBtn.addEventListener('click', this._onCloseClick);
        }
    }

    /**
     * Detach event handlers
     */
    _detachEvents() {
        if (this.refreshBtn && this._onRefreshClick) {
            this.refreshBtn.removeEventListener('click', this._onRefreshClick);
        }

        if (this.closeBtn && this._onCloseClick) {
            this.closeBtn.removeEventListener('click', this._onCloseClick);
        }

        this._onRefreshClick = null;
        this._onCloseClick = null;
    }

    /**
     * Show the dialog and render pre-loaded models or fetch new ones
     * Returns the selected model if user selected one, null if cancelled
     */
    async show() {
        const elements = this._getElements();

        if (!elements.dialog) {
            throw new Error('Dialog #model-selector-dialog not found');
        }

        this.dialog = elements.dialog;
        this.containerElement = elements.container;
        this.refreshBtn = elements.refreshBtn;
        this.closeBtn = elements.closeBtn;

        this._attachEvents();

        if (this.modelsList && this.modelsList.length > 0) {
            this._renderModels();
        } else {
            await this._loadModels();
        }

        this.dialog.showModal();

        return new Promise((resolve) => {
            const onClose = () => {
                this._detachEvents();
                this.dialog.removeEventListener('close', onClose);

                resolve(this.selectedModel || null);
            };

            this.dialog.addEventListener('close', onClose);
        });
    }
}