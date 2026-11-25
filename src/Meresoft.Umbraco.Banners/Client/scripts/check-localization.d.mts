export interface LocalizationProblem {
  file: string;
  line: number;
  text: string;
}
export function checkSource(source: string, file?: string): LocalizationProblem[];
export function checkFolder(folder: string): LocalizationProblem[];
