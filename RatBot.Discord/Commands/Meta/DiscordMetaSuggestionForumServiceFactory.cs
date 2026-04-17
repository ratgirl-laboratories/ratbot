using RatBot.Application.Meta;

namespace RatBot.Discord.Commands.Meta;

public delegate ISuggestionThreadPublisher DiscordMetaSuggestionForumServiceFactory(IGuild guild);
