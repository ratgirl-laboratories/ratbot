# Interaction Logging

RatBot interaction logs use one ambient Serilog `LogContext` scope per Discord interaction. Logs written while a command, component, or modal is executing inherit the same correlation fields automatically.

## Common Fields

Every interaction-originated log should carry these fields:

- `log_area`: always `interaction`
- `interaction_id`, `interaction_type`, `interaction_name`, `interaction_created_at_utc`
- `user_id`, `guild_id`, `channel_id`
- `command_name` for application commands, using the full slash path
- `service_instance_id`, `process_id`

Execution logs add command metadata when Discord.Net has resolved the command:

- `command_group`
- `command_module`
- `command_leaf_name`
- `command_method`
- `command_run_mode`

Event-specific logs should set:

- `event_kind`: stable event name, such as `interaction.command_invoked`
- `component`: workflow component, such as `interaction_dispatch`
- `outcome`: stable result, such as `success`, `failed`, or `received`

## Data Policy

Log Discord IDs, command names, outcomes, counts, timings, and status values by default.

Do not log message content, modal content, free-form option text, usernames, channel names, or role names unless a future change explicitly opts in for a narrow reason.

## Loki Labels

The collector promotes low-cardinality fields to Loki labels:

- `service_name`, `service_instance_id`, `environment`, `level`
- `instrumentation_scope_name`
- `log_area`, `event_kind`, `component`, `outcome`
- `interaction_type`, `command_group`, `command_name`

High-cardinality IDs stay as structured attributes, not labels:

- `interaction_id`
- `user_id`
- `guild_id`
- `channel_id`

## Example Queries

All failed interaction events for a command:

```logql
{log_area="interaction", command_name="spam image-spam-config", outcome="failed"}
```

All command invocations:

```logql
{log_area="interaction", event_kind="interaction.command_invoked"}
```

One interaction by ID:

```logql
{log_area="interaction"} |= "interaction_id=1234567890"
```

Slow completed executions:

```logql
{log_area="interaction", event_kind="interaction.execution_completed"} | total_ms > 1000
```

One user's interaction history:

```logql
{log_area="interaction"} |= "user_id=1234567890"
```
