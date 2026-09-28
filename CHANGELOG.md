# Changelog

## Unreleased
- Added `JevNoulQuestion` and `JevClient.EvaluateNoulsAsync`, which evaluate a runtime-built list of Noul questions with caller-chosen IDs in a single request and return one `JevNoul` probability per ID.
- Added an `EvaluateAsync<T>` overload that accepts a structured state (object or array) instead of plain text, for both attribute-based and dynamic questions.
- Simplified `EvaluateAsync<T>` to a single method taking the state as an object; plain strings keep working and produce the same request. This removes the former string-only overload.

---

## Version 0.9.3 (September 23, 2026)
- Updated ReadMe

---

## Version 0.9.2 (September 22, 2026)

- Added `AddJevClient` dependency injection registration with API key and custom options overloads.
- Added Coveralls coverage reporting and a coverage badge to the README.
- Reorganized the wiki into a step-by-step implementation path, with Choice, Score, and Noul nested under building the return object.
- Improved validation and response error messages with corrective guidance and links to the relevant wiki pages.
- Fixed NuGet package icon metadata so the icon is included in newly built packages.
- Added a wiki with return-object, Choice, Score, Noul, and troubleshooting guides; pointed the package project URL to it.
- Added NuGet, wiki, changelog, video, API reference, and repository-status links and badges to the README.

---

## Version 0.9.1 (September 22, 2026)
- New Icon
- Better ReadMe

---

## Version 0.9.0 (September 22, 2026)

- Initial Release

---
