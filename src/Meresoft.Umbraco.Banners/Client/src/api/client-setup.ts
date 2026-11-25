import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import { UmbContextConsumerController } from "@umbraco-cms/backoffice/context-api";
import type { UmbApiClient } from "@umbraco-cms/backoffice/http-client";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";

/**
 * Wires a hey-api generated client to the backoffice auth context (base URL,
 * bearer token, credentials, 401 handling). Pass the `client` export of
 * `client.gen.ts`. `onReady` runs once the client can make authorized calls, so a
 * view loads its data only after that.
 */
export function configureBannersClient(host: UmbControllerHost, client: UmbApiClient, onReady?: () => void): void {
  new UmbContextConsumerController(host, UMB_AUTH_CONTEXT, (authContext) => {
    if (!authContext) {
      return;
    }
    authContext.configureClient(client);
    onReady?.();
  });
}
