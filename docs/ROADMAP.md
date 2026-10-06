# Roadmap

Node Runner grows in small, always-shippable increments: each version is an
Android APK you can install and demo. Do not start version N+1 before
version N runs end-to-end on a device.

## Project stage

**Current stage: Development with testers (alpha)**, since 0.13.0, the first
published build (#744).

The stage decides how changes treat data already saved on a device
(Creations, training, progression, settings):

| Stage | Saved user data |
| --- | --- |
| **Pre-alpha** | Existing data does not matter. Save formats may change freely; no migration is needed, and old data may be discarded. |
| **Development with testers** (alpha) | Every Creation and Progression save from 0.13.0 on must load in every later version: a change to their shape migrates older files on load (`docs/SAVE_FORMAT.md` → "Versions and migration"). Other saved data may still be reset when migrating it is not worth it. |
| **Released** | Everything saved must be migrated. |

Update the current stage here when it changes.

## Plan

The plan is the open
[GitHub milestones](https://github.com/MrLogic85/Node-Runner/milestones):
each one's description holds its goal, and its issues hold the work. The
Brain v2 epic [#522](https://github.com/MrLogic85/Node-Runner/issues/522)
sets the direction from 0.12.0 to 0.20.0.

