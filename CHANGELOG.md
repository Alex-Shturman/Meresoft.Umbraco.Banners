# Changelog

## 1.0.0

First release.

- Banner zones and image or HTML banners, managed in a Banners section of the Umbraco 17 back office.
- Draft and publish, schedules, weighted rotation, several banners per zone, impression and click limits.
- Server mode and client mode zones (for cached pages), placed with a tag helper or a view component.
- Viewable impression and click statistics, daily report with chart and CSV export.
- No cookies, IP addresses or user agents are stored.
- Section access and a separate permission for HTML banners; changes are written to the Umbraco audit log.
- `IBannerImportService` to bring in statistics history from another system.
- Works on SQL Server and SQLite.
