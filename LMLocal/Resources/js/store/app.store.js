import { AppStatus } from '@app/store/app.globals.js';
import { BaseStoreClass } from "@app/store/base.store.js";

/**
 * Simple observable application store that holds UI-related state and notifies
 * subscribers on changes. Designed as a minimal, synchronous state container
 * with a small API surface suitable for UI components to subscribe to updates.
 **/
class AppStoreClass extends BaseStoreClass {
    constructor() {
        super({
            status: AppStatus.OFFLINE,
            tokenUsed: 0,
            tokenSpeed: 0,
            error: null,
            accumulatedText: "",
            accumulatedThoughtText: "",
            userMessage: ""
        });
    }
}


const appStore = new AppStoreClass();
export default appStore;


