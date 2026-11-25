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
