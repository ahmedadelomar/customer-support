using System.Text;
using CustomerSupport.Domain.Channels;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Application.Channels.LiveChat;

/// <summary>Renders a chat transcript into plain text, for a promoted ticket's description or its first message.</summary>
internal static class ChatTranscript
{
    public static string Render(IEnumerable<ChatMessage> messages)
    {
        var builder = new StringBuilder();
        foreach (var message in messages.OrderBy(m => m.SentAt))
        {
            var who = message.AuthorType switch
            {
                MessageAuthorType.Customer => message.AuthorDisplayName ?? "Visitor",
                MessageAuthorType.Agent => message.AuthorDisplayName ?? "Agent",
                MessageAuthorType.Bot => "Bot",
                _ => "System",
            };

            builder.Append('[').Append(message.SentAt.ToString("u")).Append("] ").Append(who).Append(": ").AppendLine(message.Body);
        }

        return builder.ToString();
    }

    public static string Subject(ChatSession session, IReadOnlyList<ChatMessage> messages)
    {
        var firstVisitorMessage = messages
            .Where(m => m.AuthorType == MessageAuthorType.Customer)
            .OrderBy(m => m.SentAt)
            .Select(m => m.Body)
            .FirstOrDefault();

        var name = session.VisitorName ?? "visitor";
        if (string.IsNullOrWhiteSpace(firstVisitorMessage))
        {
            return $"Live chat with {name}";
        }

        var truncated = firstVisitorMessage.Length > 80 ? firstVisitorMessage[..80] + "…" : firstVisitorMessage;
        return $"Chat: {truncated}";
    }
}
