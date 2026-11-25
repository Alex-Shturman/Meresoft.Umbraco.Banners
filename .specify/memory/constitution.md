<!--
Sync Impact Report
- Version change: template -> 1.0.0 (first ratification)
- Principles added: I. Generic Package, II. Umbraco Native, III. Privacy By Design, IV. Fast Pages,
  V. Test First, VI. Stable Public Surface
- Sections added: Distribution and Quality, Development Workflow, Governance
- Removed sections: none
- Deferred TODOs: none
-->
# Meresoft.Umbraco.Banners Constitution

## Core Principles

### I. Generic Package

The package is a general purpose banner manager for any Umbraco site, published by Meresoft.

- It MUST NOT contain code, names, data, styles or assumptions of any one client site.
- Site specific work (for example a migration of old banners from another CMS) lives in the
  consuming site, or in a separate optional add-on package, never in the core package.
- Every feature MUST be usable by an editor through the back office without code changes,
  except placing a banner zone in a template, which takes one line of markup.

Rationale: the package is a public product; client code in it would leak data and narrow its use.

### II. Umbraco Native

- Target the current Umbraco LTS line (Umbraco 17, .NET 10) first. Support for a newer major is
  added as a new package major or minor release, never by breaking the LTS line.
- Back office UI MUST use the official extension model of the new back office (manifests, web
  components with Lit, the Umbraco UI library); no AngularJS, no copied core files.
- Own data lives in own tables created by Umbraco package migrations. Installing, upgrading and
  removing the package MUST never change or delete Umbraco core data or other packages' data.
- Use Umbraco services (media, members, users, permissions, caching, background jobs, scopes)
  instead of parallel implementations.
- SQL Server and SQLite MUST both work, because both are supported Umbraco databases.

Rationale: agencies install packages that feel like part of Umbraco and survive upgrades.

### III. Privacy By Design

- Statistics are aggregated counts per banner, zone and day. The package MUST NOT store IP
  addresses, user agents, cookies, member ids, visitor ids or any other personal data.
- The package sets no cookies and needs no consent banner.
- Logs carry ids and counts only.

Rationale: a package that needs no GDPR review is easy to adopt.

### IV. Fast Pages

- Rendering a page with banner zones MUST NOT wait on a database write. Impressions are counted
  in memory and written in batches by a background task; a lost batch on a crash is acceptable.
- Banner selection reads from a cache that is refreshed when an editor saves, never per request
  from the database.
- Known bots and crawlers are not counted. A click is counted through a redirect endpoint that
  only redirects to the URL stored for that banner (no open redirect).
- The package MUST stay correct with output caching and CDNs: there is a documented client side
  rendering option for cached pages.

Rationale: banners sit on the busiest pages; the package must never be the slow part.

### V. Test First

- Every behaviour starts with a failing automated test: unit tests for selection, scheduling,
  weighting, bot filtering and counting; integration tests against a real Umbraco instance on
  SQLite (and SQL Server in CI) for migrations, the delivery endpoints and the management API.
- Tests use invented data only.
- A release is blocked while any test fails.

Rationale: the package runs on sites we do not control; regressions cost other people's time.

### VI. Stable Public Surface

- Semantic versioning. The public surface is: the tag helper and view component, the delivery
  and click endpoints, the management API, the C# services marked public, configuration keys and
  the database schema through migrations. Breaking any of them needs a major version.
- License: MIT. Free to use, including commercially.
- Every release has release notes and an updated README.

Rationale: people build on the package; surprises break their sites.

## Distribution and Quality

- Published on NuGet as `Meresoft.Umbraco.Banners` with the `umbraco-marketplace` tag and an
  `umbraco-marketplace.json`, so it is listed on the Umbraco Marketplace. Announced on the
  Umbraco forum.
- The repository has a CI build that runs all tests and packs the NuGet package.
- The README covers install, placing a zone, editor guide, configuration, cached pages, privacy
  statement, upgrade notes and how to report issues.
- Secrets, API keys and connection strings never enter the repository.

## Development Workflow

- Work follows Spec Kit: specify, plan, tasks, analyze, implement.
- Commit after each task, never push without the owner's approval. Commit messages end with the
  co-author line required by the session.
- No em dashes anywhere (code, comments, docs, commit messages).
- Every change is reviewed before merge into `main`; the review checks this constitution.

## Governance

This constitution supersedes other practices in this repository. Amendments are made with the
speckit constitution command, recorded with a version bump (MAJOR for removed or redefined
principles, MINOR for new principles or sections, PATCH for wording) and approved by the owner.
Every plan has a Constitution Check that lists each principle as pass or justified deviation.

**Version**: 1.0.0 | **Ratified**: 2026-11-25 | **Last Amended**: 2026-11-25
