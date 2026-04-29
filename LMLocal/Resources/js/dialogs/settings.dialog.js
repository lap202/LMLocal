import { createCallback } from '@app/lib/callback.js';

export class SettingsDialog {
    constructor() {
        this.onLoad = createCallback();
        this.onSave = createCallback();
    }

    async show() {
        const dialog = document.getElementById('settings-dialog');
        if (!dialog) throw new Error('Dialog #settings-dialog not found');

        const body = dialog.querySelector('.modal-body');
        const confirmBtn = dialog.querySelector('#dialog-confirm');
        const cancelBtn = dialog.querySelector('#dialog-cancel');

        if (!body || !confirmBtn || !cancelBtn) {
            throw new Error('Missing .modal-body, #dialog-confirm or #dialog-cancel');
        }

        try {
            const result = await this.onLoad.emitResult();
            const settings = result.success ? result.data : null;
            if (settings) {
                const elems = body.querySelectorAll('[data-setting]');
                elems.forEach(el => {
                    const key = el.getAttribute('data-setting');
                    if (!key) return;
                    const val = settings[key];
                    if (el.type === 'checkbox') {
                        el.checked = Boolean(val);
                    } else if (el.type === 'radio') {
                        if (val === undefined || val === null) return;
                        el.checked = String(val) === String(el.value);
                    } else {
                        el.value = val !== undefined && val !== null ? String(val) : '';
                    }
                });
            }
        }
        catch (e) {
            console.error('Failed to populate settings dialog', e);
        }

        return new Promise((resolve) => {
            dialog.showModal();

            const onConfirm = async () => {
                try {
                    const form = dialog.querySelector('form');
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
                            newSettings[key] = el.value;
                        }
                    });

                    const result = await this.onSave.emitResult(newSettings);
                    this.onLoad.off();
                    this.onSave.off();
                    dialog.close();
                    resolve(result.success);
                }
                catch (err) {
                    console.error('Failed to save settings', err);
                    this.onLoad.off();
                    this.onSave.off();
                    dialog.close();
                    resolve(false);
                }
            };

            const onCancel = () => {
                this.onLoad.off();
                this.onSave.off();
                dialog.close();
                resolve(false);
            };
            const onClose = () => {
                this.onLoad.off();
                this.onSave.off();
                resolve(false);
            };

            confirmBtn.onclick = onConfirm;
            cancelBtn.onclick = onCancel;
            dialog.onclose = onClose;
        });
    }
}
