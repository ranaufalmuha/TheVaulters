![The Vaulters Thumbnail](public/GraveyardHackathon.png)

# The Vaulters

2D arena fighting game built in Unity with Solana Web3 integration (wallet login, on-chain profile, match commit, and progression updates).

<p align="center">
  <img alt="Solana" src="https://img.shields.io/badge/Solana-3A3A3A?style=for-the-badge&logo=solana&logoColor=14F195" />
  <img alt="Unity" src="https://img.shields.io/badge/Unity_6-111111?style=for-the-badge&logo=unity&logoColor=white" />
</p>

## MagicBlock Features Used

  <img alt="MagicBlock" src="https://mintcdn.com/magicblock-42/5iyVpKJBt1PkwHw4/logo/MagicBlock-Logo-White.png?fit=max&auto=format&n=5iyVpKJBt1PkwHw4&q=85&s=5729c944bf3688e76971fe8e4237bd07" />

The Vaulters is built with a MagicBlock-style delegated execution flow so players can keep gameplay smooth while still anchoring progression to their main wallet identity.

- **Session key onboarding:** `set_session_key(profile, session, expiry)` is executed during login.
- **Profile delegation:** `delegate_profile(profile, owner, rollup)` delegates the player profile to the configured rollup.
- **Delegated match lifecycle:** `create_match(match_id, is_bot=true)` at match start and `commit_match(player_a_win)` at match end are submitted by the session account.
- **Delegated progression updates:** `update_mmr`, `gain_xp`/`add_xp`, and `gain_level` are session-signed after commit.
- **Rollup target in use:** `MAS1Dt9qreoRMQ14YQuhg8UTZMMzDdKhmkZMECCzk57`.
- **UX impact:** one main-wallet login bundle, then low-friction gameplay transactions via session key.

## Overview

The Vaulters is a fast arena-fighter where match activity is connected to on-chain player progression on Solana.

Project snapshot:

- Engine: Unity `6000.3.9f1` (Unity 6)
- Build scenes: `Assets/Scenes/MainMenu.unity`, `Assets/Scenes/SampleScene.unity`
- Web3 SDK: `com.solana.unity_sdk`
- Default RPC network: Solana Devnet

## Gameplay

- PvE match (player vs bot enemy).
- Default match timer: `180s`
- Stock/lives system: `3` lives per fighter.
- Ring-out/out-of-bounds causes stock loss.
- Timeout resolution: higher lives wins, then higher current HP, otherwise draw.

### Controls (Keyboard)

- Move: Arrow keys
- Jump: `Space`
- Attack: `D`
- Defend/Block: `S`

## Tech Stack

| Technology                        | What It Is Used For                                                         |
| --------------------------------- | --------------------------------------------------------------------------- |
| Unity 6 (`6000.3.9f1`)            | Core game engine, scene system, physics, UI, and gameplay loop              |
| C#                                | Gameplay logic, AI, combat, match flow, and Web3 transaction orchestration  |
| Solana Unity SDK                  | Wallet adapter integration, transaction building/signing, RPC communication |
| Solana Devnet                     | Development/test network for player profile and match-related program calls |
| Anchor-style instruction encoding | Building instruction discriminators and payloads for your on-chain programs |
| PDA derivation                    | Deterministic player profile and match account address derivation           |
| MagicBlock delegation flow        | Session-key based delegated execution for smoother gameplay transaction UX  |

## Web3 Flow

Main menu flow:

1. Connect main wallet (Devnet wallet adapter).
2. Generate local session account.
3. Derive player profile PDA (`"profile" + owner`).
4. If profile is missing, send `initialize_player(username)`.
5. Send login bundle in one transaction:
6. `set_session_key(profile, session, expiry)`
7. Session top-up to `0.01 SOL` when needed.
8. `delegate_profile` to MagicBlock rollup.

Match flow:

1. On match start, send `create_match(match_id, is_bot=true)`.
2. On match end, send `commit_match(player_a_win)`.
3. Apply progression updates:
4. `update_mmr(delta)` with Win `+20`, Lose `-10`, Draw `+5`.
5. XP gain with Win `+30`, Lose `+12`, Draw `+18`.
6. Level-up rule: `100 XP` per level (sends `gain_level` for gained levels).

## On-Chain Program Configuration

Current program IDs in code:

- Player program: `A4WGoXEELbB7iAaaa2oNQHCtuirB4cfvDeAYsA7v8yTa`
- Match program: `HjSbgWXcGaWnaj4x3RVvK38edxkWsomMrHdb1f77yx3a`
- Rollup delegate key: `MAS1Dt9qreoRMQ14YQuhg8UTZMMzDdKhmkZMECCzk57`

Config locations:

- `Assets/Web3/Scripts/Programs/PlayerProgram.cs`
- `Assets/Web3/Scripts/Programs/MatchProgram.cs`
- `Assets/Game/Scripts/UI/MainMenuController.cs`
- `Assets/Web3/Scripts/Core/Web3Manager.cs`

## RPC Configuration

`Web3Manager` defaults to Solana Devnet:

- `UseMagicBlockRouter = false`
- If switched to `true`, it uses `https://devnet-router.magicblock.app` and falls back to Devnet on failure.

## Getting Started

### 1. Requirements

- Unity Hub + Unity Editor `6000.3.9f1`
- Git installed (required for Git-based Unity package dependencies)
- A Solana wallet supported by Solana Unity SDK (for Devnet)

### 2. Open Project

1. Open Unity Hub.
2. Add/open this folder: `/Users/ranaufal/Development/Game/TheVaulters`
3. Let Unity resolve packages and compile scripts.

### 3. Play Locally

1. Open `Assets/Scenes/MainMenu.unity`.
2. Press Play.
3. Click Connect Wallet.
4. If prompted, create username.
5. Click Play to load `SampleScene`.

## Project Structure

- `Assets/Game/Scripts/UI/MainMenuController.cs`: wallet connect, profile initialize/login, scene transition
- `Assets/Game/Scripts/MatchFlowManager.cs`: match lifecycle + on-chain match/progression transactions
- `Assets/Web3/Scripts/Core/Web3Manager.cs`: RPC initialization and blockhash retrieval
- `Assets/Web3/Scripts/Wallet/WalletManager.cs`: wallet adapter + session account
- `Assets/Web3/Scripts/Programs/*.cs`: typed instruction builders for player/match programs
- `Assets/Web3/Scripts/Utils/*.cs`: Anchor discriminator/instruction and PDA helpers

## Notes

- The project includes both `gain_xp` and `add_xp` compatibility handling.
- Match commit/progression is session-key signed; wallet connect/login is main-wallet signed.
- Main menu attempts Devnet airdrop if owner balance is below minimum required thresholds.
