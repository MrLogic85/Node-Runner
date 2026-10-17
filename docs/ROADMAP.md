# Roadmap

Node Runner grows in small, always-shippable increments: each version is an
Android APK you can install and demo. Do not start version N+1 before
version N runs end-to-end on a device.

## Project stage

**Current stage: Development with testers (alpha)**, since 0.13.0, the first
published build.

The stage decides how changes treat data already saved on a device
(Creations, training, progression, settings):

| Stage | Saved user data |
| --- | --- |
| **Development with testers** (alpha) | Every Creation and Progression save from 0.13.0 on must load in every later version: a change to their shape migrates older files on load (`docs/SAVE_FORMAT.md` → "Versions and migration"). Other saved data may still be reset when migrating it is not worth it. |
| **Released** | Everything saved must be migrated. |

## Plan

The plan is the open
[GitHub milestones](https://github.com/MrLogic85/Node-Runner/milestones):
each one's description holds its goal, and its issues hold the work. The
Brain v2 epic [#522](https://github.com/MrLogic85/Node-Runner/issues/522)
sets the direction up to 0.20.0.
