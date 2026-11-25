import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import { UmbContextConsumerController } from "@umbraco-cms/backoffice/context-api";
import type { UmbApiClient } from "@umbraco-cms/backoffice/http-client";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";

/**
 * Wires a hey-api generated client to the backoffice auth context (base URL,
 * bearer token, credentials, 401 handling). Once `npm run generate:api` has
 * produced `src/api/client.gen.ts`, pass its `client` export as the argument.
 */
export function configureBannersClient(host: UmbControllerHost, client: UmbApiClient): void {
  new UmbContextConsumerController(host, UMB_AUTH_CONTEXT, (authContext) => {
    authContext?.configureClient(client);
  });
}
