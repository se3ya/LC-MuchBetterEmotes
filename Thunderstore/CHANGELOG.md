# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-10-14

### Added

- Support for v81
- Ability to draw on the sign
- Save slots for drawing
- Show Other Players Drawings option
- Remember Drawing option
- Show While Drawing option
- Move While Emoting option
- Hide Held Item option
- Thumbs Up, Wave and 6 7 emotes

### Changed

- Direct emote keys start unbound

### Removed

- Runtime Netcode Patcher dependency

### Fixed

- Other mods player animations being overwritten every frame (BetterEmotes #51, #55)
- Animations added by game updates break (BetterEmotes #29, #34, #42)
- Ladder, lever, item charging and terminal animations looking wrong to other players (BetterEmotes #56)
- Emotes only working for the host on v80 (BetterEmotes #59)
- Getting stuck in the sign editor after joining a lobby (BetterEmotes #58)
- Other small stuff