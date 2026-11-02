using Adit.Daemon.Services;
namespace Adit.Daemon.Tests;
public class SelfPhoneOptionsTests
{
    [Fact]
    public void NumbersAreScopedToDevice()
    {
        var options = new DaemonOptions { SelfPhoneNumbersByDevice = DaemonOptions.ParseSelfPhoneNumbers(
            """{"phone-a":["+12025550100","+12025550101"],"phone-b":["+12025550102"]}""") };
        Assert.Equal(2, options.GetSelfPhoneNumbers("PHONE-A").Count);
        Assert.Single(options.GetSelfPhoneNumbers("phone-b"));
        Assert.Empty(options.GetSelfPhoneNumbers("unknown"));
    }
    [Theory]
    [InlineData("{\"phone\":[\"2025550100\"]}")]
    [InlineData("{\"phone\":[\"+12\"]}")]
    [InlineData("{\"phone\":null}")]
    public void InvalidNumbersAreRejected(string json) => Assert.Throws<ArgumentException>(() => DaemonOptions.ParseSelfPhoneNumbers(json));

    [Fact]
    public async Task ResynthesisMergesExistingSplitWithoutLosingMessages()
    {
        var dir = Path.Combine(Path.GetTempPath(), "adit-identity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var store = new SqliteCacheStore(new DaemonOptions { DatabasePath = Path.Combine(dir, "test.db"), EncryptDatabaseAtRest = false });
            await store.InitializeAsync(CancellationToken.None);
            var inbound = new Adit.Core.Models.MessageRecord("inbox", "h1", "SMS_GSM", null, "20261101T120000",
                "Alice", "+12025550114", "+12025550100", 0, 0, null, true, false, false, "hello", "SMSGSM", "Read", [], []);
            var outbound = inbound with { Folder = "sent", Handle = "h2", Sent = true,
                SenderName = "Me", SenderAddressing = "name:Me", RecipientAddressing = "+12025550114", Body = "reply" };
            var synthesizer = new Adit.Core.Services.ConversationSynthesizer();
            // Emulate the old two-participant projection, keeping the original message intact.
            var old = synthesizer.Synthesize([inbound with { Type = "MMS" }, outbound], []);
            await store.ReplaceMessageSnapshotAsync("phone", old, CancellationToken.None);
            Assert.Equal(2, (await store.GetConversationsAsync("phone", 10, CancellationToken.None)).Count);
            var updated = synthesizer.Synthesize([inbound, outbound], []);
            await store.ReplaceMessageSnapshotAsync("phone", updated, CancellationToken.None);
            var conversation = Assert.Single(await store.GetConversationsAsync("phone", 10, CancellationToken.None));
            Assert.False(conversation.IsGroup);
            Assert.Equal(2, conversation.MessageCount);
            Assert.Equal(2, (await store.GetStoredMessagesAsync("phone", null, conversation.ConversationId, 10, CancellationToken.None)).Count);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(dir, true);
        }
    }
}
