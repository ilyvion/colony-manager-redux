# CLAUDE.local.md — Colony Manager Redux

A mod that assigns managerial tasks (hunting, forestry, livestock, production, etc.) to a colonist with the manager work type. Its manager jobs and tabs are Def-based, so third-party mods can add their own; the documentation those mod authors read is the GitHub wiki.

## Keeping the wiki up to date

The wiki is a separate git repo, cloned as the sibling directory `../colony-manager-redux.wiki` (`git@github.com:ilyvion/colony-manager-redux.wiki.git`; clone it there if it's missing). `Adding-a-custom-manager-feature.md` covers `ManagerDef` and its fields, and each base class a custom manager feature subclasses has its own page, named after the class (e.g. `ManagerJob.md`, `ManagerSettings.md`, `ManagerDefaultSettings.md`, `ManagerTab.md`).

Whenever a change adds, changes or removes something mod authors use to build a custom manager feature (a `ManagerDef` field, a job or manager comp, or a public/protected member of one of those base classes, or a change to how one behaves), update the wiki in the same task:

- New feature: document it on the relevant page in the style of the existing content (what it does, and an XML or C# example where one helps); a new extension point gets its own page, linked from `Adding-a-custom-manager-feature.md`.
- Changed feature: update its description and examples to match.
- Removed feature: remove it from its page, or delete the page if nothing is left on it.

Don't commit or push wiki changes; tell the user they're in the wiki repo for review. When producing a commit message, also produce one for the Wiki if it was edited during the session.
