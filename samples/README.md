# Sample site (local use only)

`samples/Meresoft.Umbraco.Banners.Sample` is an Umbraco 17 site on SQLite for manual checks and the
browser tests. It is not packed (`IsPackable` is false) and holds only invented content.

## Run it

```bash
cd samples/Meresoft.Umbraco.Banners.Sample
export Umbraco__CMS__Unattended__UnattendedUserName="Sample Admin"
export Umbraco__CMS__Unattended__UnattendedUserEmail="<admin email>"
export Umbraco__CMS__Unattended__UnattendedUserPassword="<admin password>"
export MB_SAMPLE_EDITOR_EMAIL="<editor email>"        # optional: creates an editor without the Banners section
export MB_SAMPLE_EDITOR_PASSWORD="<editor password>"
dotnet run --urls http://localhost:5100
```

Credentials come from environment variables or user-secrets
(`dotnet user-secrets set "Umbraco:CMS:Unattended:UnattendedUserPassword" "<value>"`). They are never
committed.

## Local settings worth knowing

- `appsettings.json` sets `Umbraco:CMS:Global:UseHttps` to `false` so the back office login works on
  plain `http://localhost`. Do not copy that setting to a real site.
- Umbraco writes a generated `Umbraco:CMS:Imaging:HMACSecretKey` into `appsettings.json` on the first
  run when none is set. Keep it out of git by setting it before the first run, for example:

  ```bash
  dotnet user-secrets set "Umbraco:CMS:Imaging:HMACSecretKey" "$(openssl rand -base64 64 | tr -d '\n')"
  ```

  If Umbraco has already written it, run `git checkout -- appsettings.json` before committing.
- `appsettings.Development.json` and `appsettings.Local.json` in samples are ignored by git; use them
  for any machine specific setting.
- The SQLite database, logs and media under `umbraco/` and `wwwroot/media/` are ignored by git.
