import { manifests as sectionManifests } from "./section/manifests.js";
import { manifests as localizationManifests } from "./lang/manifests.js";

export const manifests: Array<UmbExtensionManifest> = [
  ...sectionManifests,
  ...localizationManifests,
];
