using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using RatBot.Discord.Handlers;
using RatBot.Infrastructure.Data;
using RatBot.Infrastructure.RoleColours;

namespace RatBot.Discord.Commands.Color;

[Group("colour", "Pick or remove your display colour.")]
public sealed class ColourModule(BotDbContext db, IRoleColourReconciler reconciler)
    : InteractionModuleBase<IInteractionContext>
{
    private const string SwapPrefix = "colour-swap";

    private static readonly ConcurrentDictionary<string, Session>
        Sessions = new ConcurrentDictionary<string, Session>();

    [SlashCommand("swap", "Swap to another available display colour.")]
    public async Task SwapAsync()
    {
        if (Context.Guild is null)
        {
            await RespondAsync("This command can only be used in a guild.", ephemeral: true);
            return;
        }

        IGuildUser invoker = (IGuildUser)Context.User;
        IReadOnlyCollection<ulong> roleIds = invoker.RoleIds;

        IReadOnlyList<RoleColourOption> eligible = await ListEligibleRoleColourOptions.ExecuteAsync(
            db,
            new ListEligibleRoleColourOptions.Query(roleIds),
            CancellationToken.None);

        Log.Debug(
            "colour_swap start guild_id={GuildId} user_id={UserId} eligible_count={Eligible}",
            Context.Guild.Id,
            Context.User.Id,
            eligible.Count);

        switch (eligible.Count)
        {
            case 0:
                await RespondAsync(
                    "You do not currently have any colour roles that can be selected.",
                    ephemeral: true);

                return;
            case > 25:
                await RespondAsync(
                    "You have too many eligible colours to show in one menu. Somehow. Uh, contact ratgirl I guess",
                    ephemeral: true);

                return;
        }

        string nonce = Guid.NewGuid().ToString("N");
        string selectId = $"{SwapPrefix}:select:{Context.User.Id}:{nonce}";
        string applyId = $"{SwapPrefix}:apply:{Context.User.Id}:{nonce}";
        DateTimeOffset expires = DateTimeOffset.UtcNow.AddMinutes(5);

        Sessions[nonce] = new Session(Context.User.Id, null, expires);

        SelectMenuBuilder menu = new SelectMenuBuilder()
            .WithCustomId(selectId)
            .WithPlaceholder("Choose a colour…");

        foreach (RoleColourOption opt in eligible)
            menu.AddOption(opt.Label, opt.OptionId.Value.ToString());

        ComponentBuilder components = new ComponentBuilder()
            .WithSelectMenu(menu)
            .WithButton("Apply", applyId, disabled: true);

        await RespondAsync("Select a colour, then press Apply.", components: components.Build(), ephemeral: true);
    }

    [SlashCommand("remove", "Remove your display colour.")]
    public async Task RemoveAsync()
    {
        if (Context.Guild is null)
        {
            await RespondAsync("This command can only be used in a guild.", ephemeral: true);
            return;
        }

        Log.Debug("colour_remove start guild_id={GuildId} user_id={UserId}", Context.Guild.Id, Context.User.Id);
        await DeferAsync(ephemeral: true);

        SetNoColourPreference.Result res = await SetNoColourPreference.ExecuteAsync(
            db,
            new SetNoColourPreference.Command(Context.User.Id),
            CancellationToken.None);

        if (!res.Success)
        {
            await FollowupAsync(res.ErrorDescription ?? "Failed to update preference.", ephemeral: true);
            return;
        }

        Log.Debug("colour_remove reconcile guild_id={GuildId} user_id={UserId}", Context.Guild.Id, Context.User.Id);
        await reconciler.ReconcileMemberAsync(Context.Guild, Context.User.Id, CancellationToken.None);
        await FollowupAsync("Your display colour has been removed.", ephemeral: true);
    }

    [ComponentInteraction($"{SwapPrefix}:select:*:*", true)]
    public async Task OnSwapSelectAsync(ulong ownerUserId, string nonce, string[] values)
    {
        if (Context.User.Id != ownerUserId)
        {
            await RespondAsync("This colour selection menu is not for you.", ephemeral: true);
            return;
        }

        if (!Sessions.TryGetValue(nonce, out Session? session) || session.IsExpired)
        {
            await UpdateOrRespondExpiredAsync();
            return;
        }

        if (values.Length == 0)
        {
            await RespondAsync("Please choose a colour.", ephemeral: true);
            return;
        }

        if (!Guid.TryParse(values[0], out Guid optId))
        {
            await RespondAsync("Invalid selection.", ephemeral: true);
            return;
        }

        // Update session with selected option
        Sessions[nonce] = session with { Selected = new RoleColourOption.Id(optId) };

        Log.Debug(
            "colour_swap select guild_id={GuildId} user_id={UserId} option_id={OptionId}",
            Context.Guild?.Id,
            Context.User.Id,
            optId);

        // Rebuild components from fresh eligible list and mark selected as default
        IGuildUser invoker = (IGuildUser)Context.User;
        IReadOnlyCollection<ulong> roleIds = invoker.RoleIds;

        IReadOnlyList<RoleColourOption> eligible = await ListEligibleRoleColourOptions.ExecuteAsync(
            db,
            new ListEligibleRoleColourOptions.Query(roleIds),
            CancellationToken.None);

        string applyId = $"{SwapPrefix}:apply:{ownerUserId}:{nonce}";
        string selectId = $"{SwapPrefix}:select:{ownerUserId}:{nonce}";

        SelectMenuBuilder menu = new SelectMenuBuilder()
            .WithCustomId(selectId)
            .WithPlaceholder("Choose a colour…")
            .WithMinValues(1)
            .WithMaxValues(1);

        foreach (RoleColourOption opt in eligible)
        {
            bool isDefault = opt.OptionId.Value == optId;
            menu.AddOption(new SelectMenuOptionBuilder(opt.Label, opt.OptionId.Value.ToString(), isDefault: isDefault));
        }

        ComponentBuilder builder = new ComponentBuilder()
            .WithSelectMenu(menu)
            .WithButton("Apply", applyId);

        if (Context.Interaction is SocketMessageComponent smc)
        {
            await smc.UpdateAsync(m => { m.Components = builder.Build(); });
        }
        else
        {
            // Fallback: acknowledge with an ephemeral response if somehow not a component
            await RespondAsync("Selection updated.", ephemeral: true, components: builder.Build());
        }
    }

    [ComponentInteraction($"{SwapPrefix}:apply:*:*", true)]
    public async Task OnSwapApplyAsync(ulong ownerUserId, string nonce)
    {
        if (Context.Guild is null)
        {
            await RespondAsync("This command can only be used in a guild.", ephemeral: true);
            return;
        }

        if (Context.User.Id != ownerUserId)
        {
            await RespondAsync("This colour selection menu is not for you.", ephemeral: true);
            return;
        }

        if (!Sessions.TryGetValue(nonce, out Session? session) || session.IsExpired)
        {
            await UpdateOrRespondExpiredAsync();
            return;
        }

        if (session.Selected is null)
        {
            await RespondAsync("Please choose a colour.", ephemeral: true);
            return;
        }

        // Revalidate eligibility against current roles and option state
        IGuildUser invoker = (IGuildUser)Context.User;
        IReadOnlyCollection<ulong> roleIds = invoker.RoleIds;

        ApplyRoleColourSelection.Result res = await ApplyRoleColourSelection.ExecuteAsync(
            db,
            new ApplyRoleColourSelection.Command(Context.User.Id, session.Selected.Value, roleIds),
            CancellationToken.None);

        if (!res.Success)
        {
            await DisableComponentsAsync();
            await RespondAsync("That colour is no longer available to you.", ephemeral: true);
            return;
        }

        Log.Debug(
            "colour_swap apply_ok guild_id={GuildId} user_id={UserId} option_id={OptionId}",
            Context.Guild.Id,
            Context.User.Id,
            session.Selected.Value.Value);

        await reconciler.ReconcileMemberAsync(Context.Guild, Context.User.Id, CancellationToken.None);
        Log.Debug("colour_swap reconciled guild_id={GuildId} user_id={UserId}", Context.Guild.Id, Context.User.Id);

        // Try to get the label to show success; reload option
        RoleColourOption? option = await db.RoleColourOptions
            .SingleOrDefaultAsync(o => o.OptionId == session.Selected.Value);

        string label = option?.Label ?? "your chosen colour";

        if (Context.Interaction is SocketMessageComponent smc2)
        {
            await smc2.UpdateAsync(m =>
            {
                m.Content = $"You are now wearing {label}.";
                m.Components = new ComponentBuilder().Build();
            });
        }
        else
        {
            await RespondAsync($"You are now wearing {label}.", ephemeral: true);
        }

        Sessions.TryRemove(nonce, out _);
    }

    private async Task UpdateOrRespondExpiredAsync()
    {
        // Disable components if we can
        await DisableComponentsAsync();
        await RespondAsync("This colour selection menu has expired. Run `/colour swap` again.", ephemeral: true);
    }

    private async Task DisableComponentsAsync()
    {
        try
        {
            if (Context.Interaction is SocketMessageComponent smc)
            {
                await smc.UpdateAsync(m => { m.Components = new ComponentBuilder().Build(); });
            }
        }
        catch
        {
            // Don't care
        }
    }

    private sealed record Session(ulong OwnerUserId, RoleColourOption.Id? Selected, DateTimeOffset ExpiresAtUtc)
    {
        public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAtUtc;
    }
}