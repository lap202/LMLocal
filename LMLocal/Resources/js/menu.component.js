import createCallback from './callback.js';

/**
 * MenuComponent - handles dropdown menu UI and actions.
 * Manages menu button and dropdown event handlers, exposes an `onClick` callback
 * that emits menu action identifiers to callers and provides `hideMenu`/`destroy` helpers.
 */
const MenuComponent = (() => {
    let elements = {};
    const onClick = createCallback();

    function getElements() {
        return {
            menuBtn: document.getElementById('menu-btn'),
            dropDownMenu: document.getElementById('dropdown-menu')
        };
    }

    function toggleMenu() {
        if (elements.dropDownMenu) {
            elements.dropDownMenu.classList.toggle('show');
        }
    }

    function hideMenu() {
        if (elements.dropDownMenu && elements.dropDownMenu.classList.contains('show')) {
            elements.dropDownMenu.classList.remove('show');
        }
    }

    function handleMenuBtnClick(e) {
        e.stopPropagation();
        toggleMenu();
    }

    async function handleDropdownClick(e) {
        e.stopPropagation();
        const button = e.target.closest('button');
        if (!button) return;
        const action = button.dataset.action;
        if (await onClick.emit(action)) {
            hideMenu();
        }
    }

    return {
        init() {
            this.destroy();

            elements = getElements();
            if (elements.menuBtn && elements.dropDownMenu) {
                elements.menuBtn.addEventListener('click', handleMenuBtnClick);
                elements.dropDownMenu.addEventListener('click', handleDropdownClick);
            }
            return this;
        },

        hideMenu,
        onClick,

        destroy() {
            if (elements.menuBtn) {
                elements.menuBtn.removeEventListener('click', handleMenuBtnClick);
            }
            if (elements.dropDownMenu) {
                elements.dropDownMenu.removeEventListener('click', handleDropdownClick);
            }
            elements = {};
        }
    };
})();

export default MenuComponent;