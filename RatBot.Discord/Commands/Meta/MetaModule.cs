using RatBot.Application.Meta;

namespace RatBot.Discord.Commands.Meta;

[Group("meta", "Meta suggestion commands.")]
public sealed class MetaModule(
    MetaSuggestionService metaSuggestionService,
    MetaSuggestionPendingStore pendingStore,
    DiscordMetaSuggestionForumServiceFactory forumServiceFactory
) : SlashCommandBase
{
    private const string ModalCustomIdPrefix = "meta-suggest";
    private const string AnonymityCustomIdPrefix = "meta-suggest-anon";

    private static ErrorOr<MetaSuggestionAnonymity> ParseAnonymity(string value) =>
        value.ToLowerInvariant() switch
        {
            "anonymous" => MetaSuggestionAnonymity.Anonymous,
            "public" => MetaSuggestionAnonymity.Public,
            _ => MetaSuggestionErrors.InvalidAnonymityPreference
        };

    [SlashCommand("suggest", "Submit a meta suggestion.")]
    public async Task SuggestAsync()
    {
        if (Context.Guild is null)
        {
            await RespondAsync("This command can only be used in a guild.", ephemeral: true);
            return;
        }

        string customId = $"{ModalCustomIdPrefix}:{Context.User.Id}";
        await Context.Interaction.RespondWithModalAsync<MetaSuggestModal>(customId);
    }

    [ModalInteraction($"{ModalCustomIdPrefix}:*", true)]
    public async Task SuggestModalAsync(ulong invokerUserId, MetaSuggestModal modal)
    {
        if (Context.User.Id != invokerUserId)
        {
            await RespondAsync("Only the user who opened this modal can submit it.", ephemeral: true);
            return;
        }

        string token = pendingStore.Save(
            new MetaSuggestionDraft(
                Context.Guild.Id,
                Context.User.Id,
                modal.SuggestionTitle,
                modal.Summary,
                modal.Motivation,
                modal.Specification)
        );

        ComponentBuilder components = new ComponentBuilder()
            .WithButton("Anonymous", $"{AnonymityCustomIdPrefix}:{Context.User.Id}:{token}:anonymous")
            .WithButton("Public", $"{AnonymityCustomIdPrefix}:{Context.User.Id}:{token}:public");

        await RespondAsync(
            "Choose how your identity should be treated if this suggestion is later accepted.",
            components: components.Build(),
            ephemeral: true
        );
    }

    [ComponentInteraction($"{AnonymityCustomIdPrefix}:*:*:*", true)]
    public async Task SubmitSuggestionAsync(ulong invokerUserId, string token, string anonymityValue)
    {
        if (Context.Guild is null)
        {
            await RespondAsync("This command can only be used in a guild.", ephemeral: true);
            return;
        }

        if (Context.User.Id != invokerUserId)
        {
            await RespondAsync("Only the user who opened this flow can continue.", ephemeral: true);
            return;
        }

        if (!pendingStore.TryTake(token, out MetaSuggestionDraft? draft) || draft is null)
        {
            await RespondAsync(
                "This suggestion draft has expired. Please submit `/meta suggest` again.",
                ephemeral: true);

            return;
        }

        ErrorOr<MetaSuggestionAnonymity> anonymityResult = ParseAnonymity(anonymityValue);

        if (anonymityResult.IsError)
        {
            await RespondAsync(anonymityResult.FirstError.Description, ephemeral: true);
            return;
        }

        await DeferAsync(true);

        ErrorOr<Success> submitResult = await metaSuggestionService.SubmitAsync(
            forumServiceFactory(Context.Guild),
            draft,
            anonymityResult.Value);

        await submitResult.SwitchFirstAsync(
            async _ =>
                await FollowupAsync(
                    "Your suggestion has been noted and will now be reviewed by the committee <a:wrattendown:1494139087614120076>",
                    ephemeral: true
                ),
            async error => await FollowupAsync(error.Description, ephemeral: true)
        );
    }
}