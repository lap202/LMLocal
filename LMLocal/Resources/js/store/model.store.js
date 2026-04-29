import { Config } from '@app/store/app.globals.js';
import { BaseStoreClass } from "@app/store/base.store.js";

class ModelStoreClass extends BaseStoreClass {
    constructor() {
        super({
            modelName: '',
            modelId: '',
            tokenMax: Config.MAX_TOKENS,
            modelDetails: null
        });
    }
}

const modelStore = new ModelStoreClass();
export default modelStore;
