
// Lightweight async callback helper. emit calls the registered handler and returns true unless the handler explicitly returns/fails with false.
export const createCallback = () => {
    let fn = null;

    const api = {

        on(callback) {
            fn = typeof callback === 'function' ? callback : null;
            return api;
        },

        async emit(...args) { 
            if (!fn) return true;
            const result = fn(...args);
            return (await result) !== false;
        },
        off() {
            fn = null;
        },
        clear() {
            fn = null;
        }
    };

    return api;
};

export default createCallback;