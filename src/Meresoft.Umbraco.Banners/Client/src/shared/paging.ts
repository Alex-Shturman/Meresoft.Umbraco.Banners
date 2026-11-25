/** One page of a list endpoint. */
export interface Page<T> {
  items: T[];
  total: number;
}

/** A guard against an endpoint that keeps promising more than it gives. */
const MAX_PAGES = 100;

/**
 * Loads a list page by page until the total is reached. Gives undefined when a page could not be loaded, so a failure
 * is not mistaken for an empty list. Stops at an empty page even when the total says there should be more.
 */
export async function loadAllPages<T>(
  fetchPage: (skip: number, take: number) => Promise<Page<T> | undefined>,
  take = 100,
): Promise<T[] | undefined> {
  const all: T[] = [];
  for (let pageIndex = 0; pageIndex < MAX_PAGES; pageIndex++) {
    const page = await fetchPage(all.length, take);
    if (!page) {
      return undefined;
    }
    all.push(...page.items);
    if (page.items.length === 0 || all.length >= page.total) {
      break;
    }
  }
  return all;
}
