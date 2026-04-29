import { createCallback } from '@app/lib/callback.js';

class InstructionsModeManager {
    constructor() {
        this.modes = [];
    }

    async load() {
        const response = await fetch('https://app.local/js/instructions/tabs.json');
        const data = await response.json();
        this.modes = data.tabs || [];
    }

    getAllModes() {
        return this.modes;
    }

    getDefaultMode() {
        return this.modes.find(m => m.isDefault === true);
    }

    getModeConfig(name) {
        return this.modes.find(m => m.name === name);
    }

    isDefaultMode(name) {
        return this.modes.some(m => m.name === name && m.isDefault === true);
    }
}

class InstructionsDataManager {
    constructor() {
        this.sharedConfig = {};
        this.instructionsConfig = {};
        this.tabStates = {};
        this.modeManager = null;
    }

    async loadConfigs() {
        if (!this.modeManager) {
            this.modeManager = new InstructionsModeManager();
            await this.modeManager.load();
        }

        const sharedJson = await fetch('https://app.local/js/instructions/shared.instructions.json').then(r => r.json());
        const instructionsJson = await fetch('https://app.local/js/instructions/instructions.json').then(r => r.json());

        this.sharedConfig = sharedJson;
        this.instructionsConfig = instructionsJson;

        const sharedKeys = Object.keys(this.sharedConfig || {});
        const instructionKeys = Object.keys(this.instructionsConfig || {});

        const allModes = this.modeManager.getAllModes();

        allModes.forEach(modeConfig => {
            const modeName = modeConfig.name;
            const isDefault = modeConfig.isDefault;

            if (isDefault) {
                this.tabStates[modeName] = {};
                sharedKeys.forEach(k => {
                    const cfg = (this.sharedConfig || {})[k] || {};
                    if (cfg.cardinality === 'one') {
                        this.tabStates[modeName][k] = { enabled: false, value: cfg.items?.[0]?.id || '' };
                    } else {
                        this.tabStates[modeName][k] = [];
                    }
                });
            } else {
                this.tabStates[modeName] = { masterEnabled: false };
                instructionKeys.forEach(k => {
                    const cfg = (this.instructionsConfig || {})[k] || {};
                    if (cfg.cardinality === 'one') {
                        this.tabStates[modeName][k] = { enabled: false, value: cfg.items?.[0]?.id || '' };
                    } else {
                        this.tabStates[modeName][k] = [];
                    }
                });
            }
        });
    }

    initializeFromCurrent(current) {
        if (!current) return;

        const arr = Array.isArray(current) ? current : (Array.isArray(current.tabs) ? current.tabs : null);
        if (!arr) return;

        arr.forEach(tabObj => {
            const tabName = tabObj && tabObj.name;
            if (!tabName || !Object.prototype.hasOwnProperty.call(this.tabStates, tabName)) return;

            this.tabStates[tabName] = tabObj.state || this.tabStates[tabName];
            const isDefault = this.modeManager.isDefaultMode(tabName);
            if (!isDefault) {
                this.tabStates[tabName].masterEnabled = !!tabObj.enabled;
            }
        });
    }

    getTabState(tabName) {
        return this.tabStates[tabName] || {};
    }

    setTabState(tabName, state) {
        this.tabStates[tabName] = state;
    }

    buildPromptForTab(tabName) {
        const state = this.tabStates[tabName];
        if (!state) return '';

        const prompts = [];
        const isDefault = this.modeManager.isDefaultMode(tabName);
        const config = isDefault ? this.sharedConfig : this.instructionsConfig;

        for (const [key, value] of Object.entries(state)) {
            if (key === 'masterEnabled') continue;

            const configItem = config[key];
            if (!configItem || !configItem.items) continue;

            if (Array.isArray(value)) {
                value.forEach(itemId => {
                    const item = configItem.items.find(i => i.id === itemId);
                    if (item?.desc) {
                        prompts.push(item.desc);
                    }
                });
            } else if (typeof value === 'object' && value.value) {
                if (value.enabled !== false) {
                    const item = configItem.items.find(i => i.id === value.value);
                    if (item?.desc) {
                        prompts.push(item.desc);
                    }
                }
            }
        }

        return prompts.join('\n');
    }

    mergeAllTabStatesWithPriority(priorityTab) {
        const allModes = this.modeManager.getAllModes();
        const result = allModes.map(modeConfig => {
            const tabName = modeConfig.name;
            const state = this.tabStates[tabName] ? JSON.parse(JSON.stringify(this.tabStates[tabName])) : {};
            const isDefault = modeConfig.isDefault;
            const enabled = isDefault ? true : !!(state.masterEnabled);
            if (state && Object.prototype.hasOwnProperty.call(state, 'masterEnabled')) {
                delete state.masterEnabled;
            }
            return {
                name: tabName,
                displayName: modeConfig.displayName,
                isDefault: isDefault,
                enabled: enabled,
                state: state
            };
        });

        if (priorityTab && allModes.some(m => m.name === priorityTab)) {
            const idx = result.findIndex(r => r.name === priorityTab);
            if (idx > 0) {
                const [item] = result.splice(idx, 1);
                result.unshift(item);
            }
        }

        result.forEach(tabObj => {
            tabObj.prompt = this.buildPromptForTab(tabObj.name);
        });

        return { tabs: result };
    }
}

export class InstructionsDialog {
    constructor() {
        this.dataManager = new InstructionsDataManager();
        this._abortController = null;
        this.onLoad = createCallback();
        this.onSave = createCallback();
    }

    async show() {
        const dialog = document.getElementById('instructions-dialog');
        if (!dialog) throw new Error('Dialog #instructions-dialog not found');

        const body = dialog.querySelector('.modal-body');
        const confirmBtn = dialog.querySelector('#dialog-confirm');
        const cancelBtn = dialog.querySelector('#dialog-cancel');

        if (!body || !confirmBtn || !cancelBtn) {
            throw new Error('Missing elements in dialog');
        }

        try {
            this._abortController?.abort();
            this._abortController = new AbortController();

            await this.dataManager.loadConfigs();

            const result = await this.onLoad.emitResult();
            const currentJson = result.success ? result.data : null;
            this.dataManager.initializeFromCurrent(currentJson);


            await this._setupTabs(body);
        } catch (error) {
            console.error('Failed to populate instructions dialog:', error);
        }

        return new Promise((resolve) => {
            dialog.showModal();

            const onConfirm = async () => {
                try {
                    const currentTab = document.querySelector('.tab-btn.active').getAttribute('data-target');
                    this._saveTabState(currentTab, body);

                    const merged = this.dataManager.mergeAllTabStatesWithPriority(currentTab);

                    const result = await this.onSave.emitResult(merged);

                    if (result.success) {
                        this._abortController?.abort();
                        this._abortController = null;
                        this.onLoad.off();
                        this.onSave.off();
                        dialog.close();
                        resolve(true);

                    } else {
                        console.error('Failed to save instructions', result.error);
                        this._abortController?.abort();
                        this._abortController = null;
                        this.onLoad.off();
                        this.onSave.off();
                        dialog.close();
                        resolve(false);
                        return;
                    }

                } catch (error) {
                    console.error('Error saving instructions:', error);
                    this._abortController?.abort();
                    this._abortController = null;
                    this.onLoad.off();
                    this.onSave.off();
                    dialog.close();
                    resolve(false);
                    return;
                }
            };

            const onCancel = () => {
                this._abortController?.abort();
                this._abortController = null;
                this.onLoad.off();
                this.onSave.off();
                dialog.close();
                resolve(false);
            };

            confirmBtn.addEventListener('click', onConfirm, { signal: this._abortController?.signal });
            cancelBtn.addEventListener('click', onCancel, { signal: this._abortController?.signal });
            dialog.addEventListener('close', onCancel, { signal: this._abortController?.signal });

        });
    }

    async _setupTabs(body) {
        const sidebar = body.parentElement.querySelector('.settings-sidebar');
        if (!sidebar) {
            console.error('Settings sidebar not found');
            return;
        }

        sidebar.innerHTML = '';

        const allModes = this.dataManager.modeManager.getAllModes();
        allModes.forEach((modeConfig, index) => {
            const button = document.createElement('button');
            button.className = 'tab-btn' + (index === 0 ? ' active' : '');
            button.setAttribute('data-target', modeConfig.name);
            button.textContent = modeConfig.displayName;

            button.addEventListener('click', () => {
                const currentActiveTab = sidebar.querySelector('.tab-btn.active');
                const currentTarget = currentActiveTab?.getAttribute('data-target');

                if (currentTarget) {
                    this._saveTabState(currentTarget, body);
                }

                sidebar.querySelectorAll('.tab-btn').forEach(t => t.classList.remove('active'));
                button.classList.add('active');

                this._renderTab(body, modeConfig.name);
            }, { signal: this._abortController?.signal });

            sidebar.appendChild(button);
        });

        if (allModes.length > 0) {
            this._renderTab(body, allModes[0].name);
        }
    }

    _renderTab(container, tabName) {
        container.innerHTML = '';
        const isDefault = this.dataManager.modeManager.isDefaultMode(tabName);
        if (isDefault) {
            this._renderSharedTab(container);
        } else {
            this._renderModesTab(container, tabName);
        }
    }

    _renderSharedTab(container) {
        const defaultMode = this.dataManager.modeManager.getDefaultMode();
        const displayName = defaultMode ? defaultMode.displayName : 'Default';

        const section = document.createElement('section');
        section.className = 'tab-content';

        section.innerHTML = `
            <div class="settings-label">${escapeHtml(displayName)} Instructions</div>
            <div class="checkbox-description" style="margin-bottom:20px; margin-top:-10px">These rules remain active even if mode-specific rules are disabled.</div>
        `;

        const defaultModeName = defaultMode.name;
        const tabState = this.dataManager.getTabState(defaultModeName);

        Object.entries(this.dataManager.sharedConfig || {}).forEach(([key, config]) => {
            this._renderConstraint(section, key, config, tabState);
        });

        container.appendChild(section);
    }

    _renderModesTab(container, tabName) {
        const target = tabName;
        const tabState = this.dataManager.getTabState(target);
        const modeConfig = this.dataManager.modeManager.getModeConfig(tabName);
        const headerTitle = modeConfig ? modeConfig.displayName : tabName;

        const isTabEnabled = !!tabState.masterEnabled;

        const section = document.createElement('section');
        section.className = 'tab-content';

        section.innerHTML = `
            <label class="group-header-row" style="margin-bottom:0px;">
                <span class="settings-label" >${escapeHtml(headerTitle)} Instructions</span>
                <input type="checkbox" data-master-enable="${target}" ${isTabEnabled ? 'checked' : ''}>
            </label>
            <div class="checkbox-description" style="margin-bottom:15px">When enabled, this mode becomes active. You can quickly toggle it on or off via the mode indicator in the chat bar.</div>
        `;

        const optionsWrapper = document.createElement('div');
        optionsWrapper.className = 'tab-options-body';
        optionsWrapper.style.transition = 'opacity 0.2s ease';
        section.appendChild(optionsWrapper);

        const applyState = (isEnabled) => {
            optionsWrapper.style.opacity = isEnabled ? '1' : '0.5';
            optionsWrapper.style.pointerEvents = isEnabled ? 'auto' : 'none';
        };

        applyState(isTabEnabled);

        const masterCheckbox = section.querySelector('input[type="checkbox"][data-master-enable]');
        masterCheckbox.addEventListener('change', () => {
            applyState(masterCheckbox.checked);
        }, { signal: this._abortController?.signal });

        Object.entries(this.dataManager.instructionsConfig || {}).forEach(([key, config]) => {
            this._renderMode(optionsWrapper, key, config, tabState);
        });

        container.appendChild(section);
    }

    _renderMode(container, modeKey, config, tabState) {
        if (config.cardinality === 'many') {
            this._renderManyCardinality(container, modeKey, config, tabState);
        } else if (config.cardinality === 'one') {
            this._renderOneCardinality(container, modeKey, config, tabState);
        }
    }

    _renderManyCardinality(container, modeKey, config, tabState) {
        const selected = tabState[modeKey] || [];

        const itemsHtml = config.items.map(item => {
            const isChecked = Array.isArray(selected) ? selected.includes(item.id) : false;
            return `
                <label class="checkbox-container">
                    <input type="checkbox" data-mode="${modeKey}" data-item-id="${item.id}" ${isChecked ? 'checked' : ''}>
                    <span class="checkbox-content-wrapper">
                        <span class="checkbox-title">${escapeHtml(item.title)}</span>
                        <span class="checkbox-description">${escapeHtml(item.desc)}</span>
                    </span>
                </label>
            `;
        }).join('');

        const html = `
            <div class="settings-group">
                <label class="settings-label" style="margin-top:20px">${escapeHtml(config.label)}</label>
                <div class="checkbox-group">
                    ${itemsHtml}
                </div>
            </div>
        `;

        container.insertAdjacentHTML('beforeend', html);
    }

    _renderOneCardinality(container, modeKey, config, tabState) {
        const stateObj = tabState[modeKey] || { enabled: false, value: config.items[0]?.id };
        const isEnabled = stateObj.enabled !== false;

        const currentValue = stateObj.value || config.items[0]?.id;

        const itemsHtml = config.items.map(item => {
            const isChecked = item.id === currentValue;
            return `
                <label class="checkbox-container">
                    <input type="radio" name="${modeKey}-level" data-mode="${modeKey}" value="${item.id}" ${isChecked ? 'checked' : ''} ${!isEnabled ? 'disabled' : ''}>
                    <span class="checkbox-content-wrapper">
                        <span class="checkbox-title">${escapeHtml(item.title)}</span>
                        <span class="checkbox-description">${escapeHtml(item.desc)}</span>
                    </span>
                </label>
            `;
        }).join('');

        const html = `
            <div class="settings-group collapsible-group">
                <label class="checkbox-container group-header-row"><span class="settings-label">${escapeHtml(config.label)}</span><input type="checkbox" data-mode-enable="${modeKey}" ${isEnabled ? 'checked' : ''}></label>
                <div class="radio-group nested-options" style="${!isEnabled ? 'pointer-events:none' : ''}">
                    ${itemsHtml}
                </div>
            </div>
        `;

        const wrapper = document.createElement('div');
        wrapper.innerHTML = html;
        const group = wrapper.firstElementChild;
        const enableCheckbox = group.querySelector('input[type="checkbox"][data-mode-enable]');
        const radioGroup = group.querySelector('.radio-group');

        const enableHandler = () => {
            if (!enableCheckbox || !radioGroup) return;
            const radios = radioGroup.querySelectorAll('input[type="radio"]');
            radios.forEach(radio => {
                radio.disabled = !enableCheckbox.checked;
                radio.style.opacity = enableCheckbox.checked ? '1' : '0.5';
            });
            radioGroup.style.pointerEvents = enableCheckbox.checked ? 'auto' : 'none';
        };

        if (enableCheckbox) {
            enableCheckbox.addEventListener('change', enableHandler, { signal: this._abortController?.signal });
            enableHandler();
        }

        container.appendChild(group);
    }

    _renderConstraint(container, constraintKey, config, tabState) {
        if (config.cardinality === 'many') {
            this._renderManyCardinality(container, constraintKey, config, tabState);
        } else if (config.cardinality === 'one') {
            this._renderOneCardinality(container, constraintKey, config, tabState);
        }
    }

    _saveTabState(tabName, container) {
        const state = {};

        container.querySelectorAll('input[type="checkbox"][data-mode]').forEach(checkbox => {
            const modeKey = checkbox.getAttribute('data-mode');
            const itemId = checkbox.getAttribute('data-item-id');

            if (!state[modeKey]) {
                state[modeKey] = [];
            }

            if (checkbox.checked) {
                state[modeKey].push(itemId);
            }
        });

        container.querySelectorAll('input[type="checkbox"][data-mode-enable]').forEach(checkbox => {
            const modeKey = checkbox.getAttribute('data-mode-enable');
            const selectedRadio = container.querySelector(`input[type="radio"][name="${modeKey}-level"]:checked`);

            state[modeKey] = {
                enabled: checkbox.checked,
                value: selectedRadio ? selectedRadio.value : ''
            };
        });

        const isDefault = this.dataManager.modeManager.isDefaultMode(tabName);
        if (!isDefault) {
            const masterCheckbox = container.querySelector(`input[type="checkbox"][data-master-enable="${tabName}"]`);
            if (masterCheckbox) {
                state.masterEnabled = masterCheckbox.checked;
            } else {
                state.masterEnabled = true;
            }
        }

        this.dataManager.setTabState(tabName, state);
    }
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}
