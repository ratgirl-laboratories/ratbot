# `/timezone`

Sets the invoking user's private IANA timezone for date-aware ratbot commands.

## Usage

```text
/timezone timezone:<timezone>
```

## Behaviour

The response is ephemeral.

On success, ratbot stores the timezone for the invoking user and shows the stored timezone ID plus the current local time in that timezone.

Invalid timezone input returns an ephemeral error with examples including `UTC`, `Europe/London`, `America/New_York`, and `Australia/Sydney`.

The command cannot inspect, expose, or modify another user's timezone.
