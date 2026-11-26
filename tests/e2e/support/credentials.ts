export interface AdminCredentials {
  email: string;
  password: string;
}

function required(name: string): string {
  const value = process.env[name];
  if (!value) {
    throw new Error(
      `Environment variable ${name} is not set. Set MB_SAMPLE_ADMIN_EMAIL and MB_SAMPLE_ADMIN_PASSWORD to the sample site back office administrator.`,
    );
  }
  return value;
}

export function getAdminCredentials(): AdminCredentials {
  return {
    email: required("MB_SAMPLE_ADMIN_EMAIL"),
    password: required("MB_SAMPLE_ADMIN_PASSWORD"),
  };
}

/** Reads an optional pair of credentials; undefined when either variable is missing. */
function optionalPair(emailName: string, passwordName: string): AdminCredentials | undefined {
  const email = process.env[emailName];
  const password = process.env[passwordName];
  return email && password ? { email, password } : undefined;
}

/** The sample editor, who is in the built-in Editors group and has no Banners section (MB_SAMPLE_EDITOR_*). */
export function getEditorCredentials(): AdminCredentials | undefined {
  return optionalPair("MB_SAMPLE_EDITOR_EMAIL", "MB_SAMPLE_EDITOR_PASSWORD");
}

/** The sample banner editor: a group with the Banners section but not the HTML permission (MB_SAMPLE_BANNER_EDITOR_*). */
export function getBannerEditorCredentials(): AdminCredentials | undefined {
  return optionalPair("MB_SAMPLE_BANNER_EDITOR_EMAIL", "MB_SAMPLE_BANNER_EDITOR_PASSWORD");
}
