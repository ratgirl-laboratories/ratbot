# Quorum Module

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHALL**, **SHALL NOT**, **SHOULD**, **SHOULD NOT**, **RECOMMENDED**, **MAY**, and **OPTIONAL** in this document are to be interpreted as described in RFC 2119.

## Purpose

The Quorum module provides channel-scoped quorum configuration and quorum calculation for Discord guild text channels and forum channels.

It is intended to answer how many distinct eligible users are required to satisfy quorum for a configured channel.

## Scope

The module applies to Discord guild text channels and forum channels.

The module is limited to quorum configuration, quorum inspection, and quorum calculation. It does not record votes, decide whether a proposal passes, or manage votes or polls.

## User-Facing API

### `/config quorum register <channel> <proportion>`

Registers a text channel or forum channel as requiring quorum.

#### Parameters

- `<channel>` MUST be a guild text channel or guild forum channel.
- `<proportion>` MUST satisfy `0 < proportion <= 1`.

#### Behaviour

If the channel has no quorum configuration, the command MUST create one.

If the channel already has a quorum configuration, the command MUST update its quorum proportion.

A quorum configuration with no voter roles MUST be treated as incomplete.

### `/config quorum role <channel> <role> <should_add>`

Adds or removes a voter role from a channel's quorum configuration.

#### Parameters

- `<channel>` MUST be a guild text channel or guild forum channel.
- `<role>` MUST be a Discord role mention.
- `<should_add>` MUST be optional and MUST default to `true`.

#### Behaviour

If `<should_add>` is `true`, the command MUST add the role to the channel's configured voter roles.

If `<should_add>` is `false`, the command MUST remove the role from the channel's configured voter roles.

The command MUST fail if the channel has no quorum configuration.

Adding an already-configured role SHOULD be treated as a successful no-op.

Removing a role that is not configured SHOULD be treated as a successful no-op.

### `/config quorum remove <channel>`

Removes a channel's quorum configuration.

#### Parameters

- `<channel>` MUST be a guild text channel or guild forum channel.

#### Behaviour

The command MUST remove the channel's configured quorum proportion and voter roles.

The command MUST fail if the channel has no quorum configuration.

### `/quorum inspect <channel>`

Shows the stored quorum configuration for a channel.

#### Parameters

- `<channel>` MUST be a guild text channel or guild forum channel.

#### Behaviour

The response MUST be ephemeral.

The command MUST show the configured channel, quorum proportion, voter roles, and whether the configuration is complete.

The command MUST fail if the channel has no quorum configuration.

### `/quorum calculate <channel>`

Calculates the current quorum for a channel.

#### Parameters

- `<channel>` MUST be a guild text channel or guild forum channel.

#### Behaviour

The command MUST fail if the channel has no quorum configuration.

The command MUST return an ephemeral error if the channel's quorum configuration is incomplete.

The command MUST return the number of distinct eligible voters, the configured quorum proportion, and the required quorum number.

## Behaviour

An eligible voter is a non-bot guild member who has at least one configured voter role.

A member MUST be counted at most once, even if they have multiple configured voter roles.

The required quorum number MUST be calculated as `ceil(eligible_voter_count * quorum_proportion)`.

If the eligible voter count is zero, quorum calculation MUST fail rather than returning `0`.

## Permissions

All commands in this module MUST be restricted to users with the Discord `MUTE_MEMBERS` guild permission.

Permission failures MUST produce an ephemeral error.

## Persistence

A quorum configuration MUST persist the guild ID, channel ID, channel kind, quorum proportion, and configured voter role IDs.

The module MUST persist at most one quorum configuration per guild channel.

Configured voter role IDs MUST be deduplicated before persistence.

User IDs MUST NOT be persisted by this module.

## Privileged Intents

This module requires the `GUILD_MEMBERS` privileged intent.

The intent is warranted because quorum calculation requires RatBot to directly fetch the members of each configured voter role.

The module MUST NOT calculate quorum from the Discord member cache.

The module MUST only fetch members for the configured voter roles of the requested quorum channel.

## User-Identifiable Data

During quorum calculation, RatBot MUST temporarily hold fetched user IDs in memory to compute the union of members across the configured voter roles.

Fetched user IDs MUST be discarded after the quorum calculation completes.

Fetched user IDs MUST NOT be persisted.

Fetched user IDs MUST NOT be logged during normal operation.

The module MUST NOT inspect, store, or log message content.

## Errors

The module MUST produce clear ephemeral errors for unsupported channel types, missing quorum configuration, incomplete quorum configuration, invalid quorum proportions, missing or invalid roles, zero eligible voters, unavailable member data, and permission failures.