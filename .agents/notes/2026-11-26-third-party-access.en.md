---
status: implemented
category: repository
---
# Third-party Internet access

English | [中文](2026-11-26-third-party-access.zh.md)

Every integration in this repository that retrieves third-party data or calls a third-party service over the Internet must use a channel that the third party officially provides. This applies to product code, plugins, tools and prototypes, including requests made through dependencies and fallback paths. When the provider offers no such channel for a particular operation, leave that online operation unavailable.

The provider chooses how to publish its data and services. An official database dump, RSS feed, account login API or an API that the site provides to its own frontend can qualify. For frontend APIs, use the site's robots.txt rules for LazyTuiHub to decide which paths to request. Honor rate limits and explicit retry instructions. Use a widely compatible User-Agent that identifies LazyTuiHub truthfully, such as the common `Mozilla/5.0 (compatible; LazyTuiHub; +https://github.com/tuihub/lazytuihub)` form. Identify an official channel from what the provider publishes or uses in its own frontend. [Robots Exclusion Protocol](https://www.rfc-editor.org/rfc/rfc9309.html)

This rule places no restriction on reading, importing or converting local files. A plugin may read local Steam installation manifests or query a SQLite conversion of a database dump. If repository code also downloads a file or follows a link found inside it, the rule applies to that Internet request.
