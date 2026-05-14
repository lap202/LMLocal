import { UIText } from '@app/store/app.globals.js';
import { AppStatus } from '@app/store/app.status.js';
import { appSelectors } from '@app/store/app.selectors.js';
import { createCallback } from '@app/lib/callback.js';

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
            contextToggleBtn: document.getElementById('contextToggleBtn'),
            dropdown: document.getElementById('actionDropdown'),
            dropdownTrigger: document.querySelector('.dropdown-trigger'),
            selectedOption: document.getElementById('selectedOption'),
            dropdownMenu: document.querySelector('.dropdown-menu'),
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
            const hasActiveContent = this.elements.contextToggleBtn.classList.contains('active');
            const instructionsMode = this.elements.selectedOption?.getAttribute('data-selected');
            if (await this.onEnter.emit(value, hasActiveContent, instructionsMode)) {
                this.clearInput();
            }
        }
    };

    _handleClick = async () => {
        const value = this.elements.userInput?.value;
        const hasActiveContent = this.elements.contextToggleBtn.classList.contains('active');
        const instructionsMode = this.elements.selectedOption?.getAttribute('data-selected');
        if (await this.onClick.emit(value, hasActiveContent, instructionsMode)) {
            this.clearInput();
        }
    };

    _handleContextToggle = async (e) => {
        if (e && typeof e.preventDefault === 'function') e.preventDefault();
        this.elements.contextToggleBtn.classList.toggle('active');
    };


    _handleDropdownToggle = (e) => {
        if (e && typeof e.stopPropagation === 'function') e.stopPropagation();
        this.elements.dropdown.classList.toggle('active');
    };

    _handleDropdownItemClick = (e) => {
        const item = e.target.closest && e.target.closest('.dropdown-item');
        if (!item) return;
        const value = item.textContent || '';
        const selected = this.elements.selectedOption;
        const dropdown = this.elements.dropdown;
        if (selected) {
            selected.textContent = value;
            selected.setAttribute('data-selected', item.getAttribute('data-value'));
        }
        if (dropdown) {
            dropdown.classList.remove('active');
        }
    };

    _attachEvents() {
        const { userInput, mainBtn, contextToggleBtn, dropdownTrigger, dropdown } = this.elements;
        if (!userInput || !mainBtn || !contextToggleBtn || !dropdownTrigger || !dropdown) return;

        userInput.addEventListener('input', this._handleInput);
        userInput.addEventListener('keydown', this._handleKeydown);
        mainBtn.addEventListener('click', this._handleClick);
        contextToggleBtn.addEventListener('click', this._handleContextToggle);

        dropdownTrigger.addEventListener('click', this._handleDropdownToggle);
        dropdown.addEventListener('click', this._handleDropdownItemClick);
    }

    _detachEvents() {
        const { userInput, mainBtn, contextToggleBtn, dropdownTrigger, dropdown } = this.elements;
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
        if (dropdownTrigger) {
            dropdownTrigger.removeEventListener('click', this._handleDropdownToggle);
        }
        if (dropdown) {
            dropdown.removeEventListener('click', this._handleDropdownItemClick);
        }
    }

    _updateControls(state, prev) {
        if (
            prev &&
            state.status === prev.status &&
            appSelectors.isBusy(state.status) === appSelectors.isBusy(prev.status)
        ) {
            return;
        }

        const isBusy = appSelectors.isBusy(state.status);
        const isStopping = state.status === AppStatus.STOPPING || state.status === AppStatus.OFFLINE;

        this.elements.userInput.disabled = isBusy;
        this.elements.mainBtn.disabled = isStopping;

        const buttonText = isBusy
            ? UIText.BUTTON_STOP
            : isStopping
                ? UIText.BUTTON_WAIT
                : UIText.BUTTON_SEND


        this.elements.mainBtn.textContent = buttonText;
        this.elements.mainBtn.className = `main-btn ${isBusy || isStopping ? 'btn-stop' : ''}`;
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

    hideDropdown() {
        if (this.elements.dropdown && this.elements.dropdown.classList.contains('active')) {
            this.elements.dropdown.classList.remove('active');
        }
    }

    updateInstructionsState(state, prev) {
        if (!this.elements.dropdownMenu || !this.elements.selectedOption) return;

        if (state.instructions === prev?.instructions) return;

        const instructions = state.instructions || {};
        const tabs = instructions.tabs || [];

        if (tabs.length === 0) {
            return;
        }

        const enabledTabs = tabs.filter(tab => tab.enabled === true);
        const currentItems = this.elements.dropdownMenu.querySelectorAll('.dropdown-item');

        if (currentItems.length !== enabledTabs.length) {
            this.elements.dropdownMenu.innerHTML = '';

            enabledTabs.forEach(tab => {
                const item = document.createElement('div');
                item.className = 'dropdown-item';
                item.setAttribute('data-value', tab.name);
                item.textContent = tab.displayName;
                this.elements.dropdownMenu.appendChild(item);
            });
        } else {
            currentItems.forEach(item => {
                const dataValue = item.getAttribute('data-value');
                item.style.display = enabledTabs.some(t => t.name === dataValue) ? 'block' : 'none';
            });
        }

        const currentSelected = this.elements.selectedOption.getAttribute('data-selected');
        const defaultTab = tabs.find(t => t.isDefault === true);
        const firstEnabledTab = enabledTabs[0];

        if (!currentSelected || !tabs.some(t => t.name === currentSelected)) {
            if (defaultTab && enabledTabs.some(t => t.name === defaultTab.name)) {
                this.elements.selectedOption.textContent = defaultTab.displayName;
                this.elements.selectedOption.setAttribute('data-selected', defaultTab.name);
            } else if (firstEnabledTab) {
                this.elements.selectedOption.textContent = firstEnabledTab.displayName;
                this.elements.selectedOption.setAttribute('data-selected', firstEnabledTab.name);
            }
        }
    }
};

const inputComponent = new InputComponent();
export { inputComponent };