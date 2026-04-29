import { BaseStoreClass } from "@app/store/base.store.js";

class SettingsStoreClass extends BaseStoreClass {
    constructor() {
        super({
            LmStudioBaseUrl: "http://localhost:1234",
            AutoLoadOnStartup: true,
            EnableHistoryCompression: true,
            EnableHistoryCompaction: true,
            Theme: 0,
            StreamInactivityTimeoutSeconds: 20,
            EnableChatLogging: false
        });
    }
}

const settingsStore = new SettingsStoreClass();
export default settingsStore;
