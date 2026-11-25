import { manifests as sectionManifests } from "./section/manifests.js";
import { manifests as localizationManifests } from "./lang/manifests.js";
import { manifests as zoneManifests } from "./zones/manifests.js";
import { manifests as bannerManifests } from "./banners/manifests.js";

export const manifests: Array<UmbExtensionManifest> = [
  ...sectionManifests,
  ...zoneManifests,
  ...bannerManifests,
  ...localizationManifests,
];
