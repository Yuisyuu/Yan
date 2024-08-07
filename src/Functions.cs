using LiteDB;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Yan.Utils;
using Timer = System.Timers.Timer;

namespace Yan;

internal static class Functions
{
    public static async Task OnCallback(this CallbackQuery callbackQuery)
    {
        if (callbackQuery.Message is null)
        {
            return;
        }

        Localizer lang = Program.Localizer.GetLocalizer(callbackQuery.From.LanguageCode);
        if (!Program.GroupData.TryGetValue(callbackQuery.Message.Chat.Id, out Dictionary<long, int>? data) ||
            !data.TryGetValue(callbackQuery.From.Id, out int historyMessageId) ||
            historyMessageId != callbackQuery.Message.MessageId)
        {
            await Program.BotClient.AnswerCallbackQueryAsync(callbackQuery.Id, lang["Failed"]);
            return;
        }

        try
        {
            await Program.BotClient.ApproveChatJoinRequest(callbackQuery.Message.Chat.Id, callbackQuery.From.Id);
        }
        catch (ApiRequestException)
        {
            await Program.BotClient.AnswerCallbackQueryAsync(callbackQuery.Id, lang["Failed"]);
            return;
        }

        await Program.BotClient.AnswerCallbackQueryAsync(callbackQuery.Id, lang["Pass"]);
    }

    public static async Task OnRequest(this ChatJoinRequest chatJoinRequest)
    {
        if (!Program.GroupData.TryGetValue(chatJoinRequest.Chat.Id, out Dictionary<long, int>? value))
        {
            Program.GroupData[chatJoinRequest.Chat.Id] = new();
        }
        else if (value.ContainsKey(chatJoinRequest.From.Id))
        {
            return;
        }

        Localizer lang = Program.Localizer.GetLocalizer(chatJoinRequest.From.LanguageCode);
        const int min = 3; // TODO：群组管理员自定义时长
        string message = lang.Translate("Message",
            string.IsNullOrWhiteSpace(chatJoinRequest.From.Username)
                ? $"[{$"{chatJoinRequest.From.FirstName}{chatJoinRequest.From.LastName ?? string.Empty}".Escape()}](tg://user?id={chatJoinRequest.From.Id})"
                : $"@{chatJoinRequest.From.Username.Escape()}", min);
        try
        {
            Message msg = await Program.BotClient.SendTextMessageAsync(chatJoinRequest.Chat.Id,
                message,
                chatJoinRequest.Chat.IsForum
                    ? Program.Database.GetCollection<ChatData>("chats").FindById(chatJoinRequest.Chat.Id)
                        .MessageThreadId
                    : default, ParseMode.MarkdownV2,
                replyMarkup: new InlineKeyboardMarkup(new[]
                    { InlineKeyboardButton.WithCallbackData(lang["VerifyButton"]) }));
            Program.GroupData[chatJoinRequest.Chat.Id][chatJoinRequest.From.Id] = msg.MessageId;
            Timer timer = new(min * 60000)
            {
                AutoReset = false
            };
            timer.Elapsed += async (_, _) =>
            {
                if (!Program.GroupData.TryGetValue(chatJoinRequest.Chat.Id, out Dictionary<long, int>? members) ||
                    !members.ContainsKey(chatJoinRequest.From.Id))
                {
                    return;
                }

                members.Remove(chatJoinRequest.From.Id);
                try
                {
                    await Program.BotClient.DeleteMessageAsync(chatJoinRequest.Chat.Id, msg.MessageId);
                }
                catch (ApiRequestException)
                {
                }
            };
            timer.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidDataException(message, ex);
        }
    }

    public static async Task OnSet(this Message message)
    {
        if (message.From is null || !message.Chat.IsForum ||
            (await Program.BotClient.GetChatAdministratorsAsync(message.Chat.Id)).All(chatMember =>
                chatMember.User.Id != message.From.Id))
        {
            return;
        }

        Localizer lang = Program.Localizer.GetLocalizer(message.From.LanguageCode);
        ILiteCollection<ChatData> col = Program.Database.GetCollection<ChatData>("chats");
        col.Upsert(new ChatData(message.Chat.Id, message.MessageThreadId ?? default));
        await Program.BotClient.SendTextMessageAsync(message.Chat.Id, lang["UpdateSuccess"], message.MessageThreadId,
            ParseMode.MarkdownV2, replyParameters: message);
    }

    public static async Task OnJoin(this User member, long chatId, Dictionary<long, int> data)
    {
        if (!data.TryGetValue(member.Id, out int value))
        {
            return;
        }

        try
        {
            await Program.BotClient.DeleteMessageAsync(chatId, value);
        }
        catch (ApiRequestException)
        {
        }

        data.Remove(member.Id);
    }
}