import { createCallback } from '@app/lib/callback.js';

export class SettingsDialog {
    constructor() {
        this.onLoad = createCallback();
        this.onSave = createCallback();
        this.onTestConnection = createCallback();
        this.el = {};
        this._toggleHandler = null;
        this._providerChangeHandler = null;
        this._testBtnClickHandler = null;
        this._testBtnTimeout = null;
    }

    _getElements() {

        const dialog = document.getElementById('settings-dialog');

        return {
            dialog,
            body: dialog.querySelector('.modal-body'),
            form: dialog.querySelector('form'),
            confirmBtn: dialog.querySelector('#dialog-confirm'),
            cancelBtn: dialog.querySelector('#dialog-cancel'),
            toggleBtn: dialog.querySelector('.password-toggle'),
            apiKeyInput: dialog.querySelector('[data-setting="ApiKey"]'),
            providerSelect: dialog.querySelector('[data-setting="Provider"]'),
            baseUrlInput: dialog.querySelector('[data-setting="LmStudioBaseUrl"]'),
            testBtn: dialog.querySelector('.test-connection-btn')
        };
    }

    _attachEvents() {
        const { toggleBtn, providerSelect, testBtn } = this.el;

        if (toggleBtn && this._toggleHandler) {
            toggleBtn.addEventListener('click', this._toggleHandler);
        }
        if (providerSelect && this._providerChangeHandler) {
            providerSelect.addEventListener('change', this._providerChangeHandler);
        }
        if (testBtn && this._testBtnClickHandler) {
            testBtn.addEventListener('click', this._testBtnClickHandler);
        }
    }

    _detachEvents() {
        const { toggleBtn, providerSelect, testBtn } = this.el;

        if (toggleBtn && this._toggleHandler) {
            toggleBtn.removeEventListener('click', this._toggleHandler);
        }
        if (providerSelect && this._providerChangeHandler) {
            providerSelect.removeEventListener('change', this._providerChangeHandler);
        }
        if (testBtn && this._testBtnClickHandler) {
            testBtn.removeEventListener('click', this._testBtnClickHandler);
        }
    }

    _cleanup() {
        this.onLoad.off();
        this.onSave.off();
        this.onTestConnection.off();
        this._detachEvents();
        if (this._testBtnTimeout) {
            clearTimeout(this._testBtnTimeout);
            this._testBtnTimeout = null;
        }
        this.el = {};
    }

    async show() {

        this.el = this._getElements();

        const { dialog, body, form, confirmBtn, cancelBtn, toggleBtn, apiKeyInput, providerSelect, baseUrlInput, testBtn } = this.el;

        if (!dialog || !body || !confirmBtn || !cancelBtn || !toggleBtn || !apiKeyInput || !providerSelect || !baseUrlInput) {
            throw new Error('Missing required dialog elements');
        }

        const defaultUrls = {
            'lmstudio': 'http://localhost:1234',
            'ollama': 'http://localhost:11434',
            'jan': 'http://localhost:1337',
            'openapi': ''
        };

        try {
            const result = await this.onLoad.emitResult();
            const settings = result.success ? result.data : null;
            if (settings) {
                const elems = body.querySelectorAll('[data-setting]');
                elems.forEach(el => {
                    const key = el.getAttribute('data-setting');
                    if (!key) return;
                    let val = settings[key];

                    if (el.type === 'checkbox') {
                        el.checked = Boolean(val);
                    } else if (el.type === 'radio') {
                        if (val === undefined || val === null) return;
                        el.checked = String(val) === String(el.value);
                    } else {
                        el.value = val !== undefined && val !== null ? String(val) : '';
                    }
                });

                if (!providerSelect.value || providerSelect.value === '') {
                    const baseUrl = baseUrlInput.value;
                    let detectedProvider = 'lmstudio';

                    if (baseUrl) {
                        for (const [provider, url] of Object.entries(defaultUrls)) {
                            if (url && baseUrl.includes(url)) {
                                detectedProvider = provider;
                                break;
                            }
                        }
                    }

                    providerSelect.value = detectedProvider;
                }
            }
        }
        catch (e) {
            console.error('Failed to populate settings dialog', e);
        }

        this._toggleHandler = () => {
            const isPassword = apiKeyInput.type === 'password';
            apiKeyInput.type = isPassword ? 'text' : 'password';
            toggleBtn.style.color = isPassword ? 'var(--accent-color)' : 'var(--muted-color)';
        };

        this._providerChangeHandler = (e) => {
            const selected = e.target.value;
            const newUrl = defaultUrls[selected] || '';
            baseUrlInput.value = newUrl;
        };

        this._testBtnClickHandler = async (e) => {
            e.preventDefault();

            const provider = providerSelect.value;
            const url = baseUrlInput.value;

            if (!provider) { providerSelect.focus(); return; }
            if (!url) { baseUrlInput.focus(); return; }

            const iconSlot = testBtn.querySelector('.btn-icon-slot');
            const originalIcon = iconSlot.innerHTML;

            const successIcon = `<svg class="btn-icon" width="14" height="14" viewBox="0 0 16 16" fill="currentColor"><path d="M13.854 3.646a.5.5 0 0 1 0 .708l-7 7a.5.5 0 0 1-.708 0l-3.5-3.5a.5.5 0 1 1 .708-.708L6.5 10.293l6.646-6.647a.5.5 0 0 1 .708 0z"/></svg>`;
            const errorIcon = `<svg class="btn-icon" width="14" height="14" viewBox="0 0 16 16" fill="currentColor"><path d="M4.646 4.646a.5.5 0 0 1 .708 0L8 7.293l2.646-2.647a.5.5 0 0 1 .708.708L8.707 8l2.647 2.646a.5.5 0 0 1-.708.708L8 8.707l-2.646 2.647a.5.5 0 0 1-.708-.708L7.293 8 4.646 5.354a.5.5 0 0 1 0-.708z"/></svg>`;

            testBtn.disabled = true;
            testBtn.classList.remove('success', 'error');
            iconSlot.innerHTML = '<span class="btn-spinner"></span>';

            try {
                const result = await this.onTestConnection.emitResult({
                    provider: provider,
                    url: url,
                    apiKey: apiKeyInput.value
                });

                if (result && result.success) {
                    iconSlot.innerHTML = successIcon;
                    testBtn.classList.add('success');
                } else {
                    iconSlot.innerHTML = errorIcon;
                    testBtn.classList.add('error');
                }
            } catch (err) {
                console.error('Test connection error', err);
                iconSlot.innerHTML = errorIcon;
                testBtn.classList.add('error');
            } finally {
                this._testBtnTimeout = setTimeout(() => {
                    if (testBtn) {
                        testBtn.disabled = false;
                        testBtn.classList.remove('success', 'error');
                        iconSlot.innerHTML = originalIcon;
                    }
                    this._testBtnTimeout = null;
                }, 3000);
            }
        };

        this._attachEvents();

        return new Promise((resolve) => {
            dialog.showModal();

            const onConfirm = async () => {
                try {
                    if (form && !form.checkValidity()) {
                        if (typeof form.reportValidity === 'function') form.reportValidity();
                        const firstInvalid = form.querySelector(':invalid');
                        if (firstInvalid) firstInvalid.focus();
                        return;
                    }

                    const elems = body.querySelectorAll('[data-setting]');
                    const newSettings = {};
                    elems.forEach(el => {
                        const key = el.getAttribute('data-setting');
                        if (!key) return;
                        if (el.type === 'checkbox') {
                            newSettings[key] = !!el.checked;
                        } else if (el.type === 'radio') {
                            if (!el.checked) return;
                            const asNum = parseInt(el.value, 10);
                            newSettings[key] = Number.isNaN(asNum) ? el.value : asNum;
                        } else {
                            // For select and text inputs, keep string value
                            newSettings[key] = el.value;
                        }
                    });

                    const result = await this.onSave.emitResult(newSettings);
                    this._cleanup();
                    dialog.close();
                    resolve(result.success);
                }
                catch (err) {
                    console.error('Failed to save settings', err);
                    this._cleanup();
                    dialog.close();
                    resolve(false);
                }
            };

            const onCancel = () => {
                this._cleanup();
                dialog.close();
                resolve(false);
            };

            const onClose = () => {
                this._cleanup();
                resolve(false);
            };

            confirmBtn.onclick = onConfirm;
            cancelBtn.onclick = onCancel;
            dialog.onclose = onClose;
        });
    }
}
