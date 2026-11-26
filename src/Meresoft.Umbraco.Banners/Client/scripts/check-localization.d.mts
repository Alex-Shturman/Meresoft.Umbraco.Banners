export interface LocalizationProblem {
  file: string;
  line: number;
  text: string;
}
export function checkSource(source: string, file?: string, knownKeys?: Set<string>): LocalizationProblem[];
export function knownKeysOf(dictionarySource: string): Set<string>;
export function checkFolder(folder: string): LocalizationProblem[];
