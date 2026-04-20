import { createCallback }  from './callback.js';
/**
 * Lightweight component that manages a simple dropdown menu controlled by a
 * menu button. The component wires DOM elements, attaches event handlers and
 * exposes a callback-based `onClick` hook for menu actions.
 **/
class MenuComponent {
    constructor() {
        this.elements = {};
        this.onClick = createCallback();
    }

    _getElements() {
        return {
            menuBtn: document.getElementById('menu-btn'),
            dropDownMenu: document.getElementById('dropdown-menu')
        };
    }

    _toggleMenu = () => {
        if (this.elements.dropDownMenu) {
            this.elements.dropDownMenu.classList.toggle('show');
        }
    };

    hideMenu = () => {
        if (this.elements.dropDownMenu && this.elements.dropDownMenu.classList.contains('show')) {
            this.elements.dropDownMenu.classList.remove('show');
        }
    };

    _handleMenuBtnClick = (e) => {
        e.stopPropagation();
        this._toggleMenu();
    };

    _handleDropdownClick = async (e) => {
        e.stopPropagation();
        const button = e.target.closest('button');
        if (!button) return;
        const action = button.dataset.action;
        if (await this.onClick.emit(action)) {
            this.hideMenu();
        }
    };

    _attachEvents() {
        if (this.elements.menuBtn && this.elements.dropDownMenu) {
            this.elements.menuBtn.addEventListener('click', this._handleMenuBtnClick);
            this.elements.dropDownMenu.addEventListener('click', this._handleDropdownClick);
        }
    }

    _detachEvents() {
        if (this.elements.menuBtn) {
            this.elements.menuBtn.removeEventListener('click', this._handleMenuBtnClick);
        }
        if (this.elements.dropDownMenu) {
            this.elements.dropDownMenu.removeEventListener('click', this._handleDropdownClick);
        }
    }

    setup() {
        this.reset();
        this.elements = this._getElements();
        this._attachEvents();
        return this;
    }

    reset() {
        this._detachEvents();
        this.elements = {};
    }
}

const menuComponent = new MenuComponent();
export default menuComponent;