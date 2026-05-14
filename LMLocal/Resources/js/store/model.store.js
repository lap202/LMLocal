import { BaseStoreClass } from "@app/store/base.store.js";

class ModelStoreClass extends BaseStoreClass {
    constructor() {
        super({
            modelId: '',
            modelName: '',
            tokenMax: 0,
            tokenUsed: 0,
            supportsMaxTokens: false
        });
    }
}

const modelStore = new ModelStoreClass();
export default modelStore;
