import {
  getInferenceProviders,
  saveInferenceProvider,
  saveInferenceRoute,
} from "./settings-api";

export interface InferenceSettingsEditor {
  load: typeof getInferenceProviders;
  saveProvider: typeof saveInferenceProvider;
  saveRoute: typeof saveInferenceRoute;
  isDraft: boolean;
}

export const savedInferenceEditor: InferenceSettingsEditor = {
  load: getInferenceProviders,
  saveProvider: saveInferenceProvider,
  saveRoute: saveInferenceRoute,
  isDraft: false,
};
