export class Modal {
    async confirm(message) {
        const dialog = document.getElementById('confirm-dialog');
        if (!dialog) throw new Error('Dialog #confirm-dialog not found');

        const body = dialog.querySelector('.modal-body');
        const confirmBtn = dialog.querySelector('#dialog-confirm');
        const cancelBtn = dialog.querySelector('#dialog-cancel');

        if (!body || !confirmBtn || !cancelBtn) {
            throw new Error('Missing .modal-body, #dialog-confirm or #dialog-cancel');
        }

        body.textContent = message;

        return new Promise((resolve) => {
            dialog.showModal();

            const onConfirm = () => {
                dialog.close();
                resolve(true);
            };
            const onCancel = () => {
                dialog.close();
                resolve(false);
            };
            const onClose = () => {
                resolve(false);
            };

            confirmBtn.onclick = onConfirm;
            cancelBtn.onclick = onCancel;
            dialog.onclose = onClose;
        });
    }
}