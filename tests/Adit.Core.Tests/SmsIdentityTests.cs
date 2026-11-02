using Adit.Core.Models;
using Adit.Core.Services;
namespace Adit.Core.Tests;
public class SmsIdentityTests
{
    private static MessageRecord Sms(string handle, bool sent = false) => new(
        sent ? "sent" : "inbox", handle, "SMS_GSM", null, sent ? "20261101T120100" : "20261101T120000",
        sent ? "Me" : "Alice", sent ? "name:Me" : "+12025550114", sent ? "+12025550114" : "+12025550100",
        0, 0, null, true, sent, false, handle, "SMSGSM", "Read",
        [new(sent ? "Me" : "Alice", sent ? [] : ["+12025550114"], [])], []);

    [Theory]
    [InlineData("SMS_GSM")]
    [InlineData("SMS_CDMA")]
    public void DirectSmsWithoutOwnerCard_MergesInboundAndOutbound(string type)
    {
        var result = new ConversationSynthesizer().Synthesize(
            [Sms("in") with { Type = type }, Sms("out", true) with { Type = type }], []);
        var conversation = Assert.Single(result.Conversations);
        Assert.False(conversation.IsGroup);
        Assert.Equal(2, conversation.MessageCount);
        Assert.Equal("+12025550114", Assert.Single(conversation.Participants).Phones.Single());
        Assert.Empty(result.SelfPhones); // Local direction rule must not become a global identity guess.
    }

    [Theory]
    [InlineData("SMS_GSM")]
    [InlineData("MMS")]
    public void MultipleRecipients_ArePreserved(string type)
    {
        var message = Sms("group") with { Type = type,
            Recipients = [new("Owner", ["+12025550100"], []), new("Bob", ["+12025550115"], [])] };
        var result = new ConversationSynthesizer().Synthesize([message], [], selfPhoneNumbers: ["+12025550100"]);
        var conversation = Assert.Single(result.Conversations);
        Assert.True(conversation.IsGroup);
        Assert.Equal(2, conversation.Participants.Count);
        Assert.Contains(conversation.Participants, p => p.Phones.Contains("+12025550115"));
    }

    [Fact]
    public void ExplicitNumbersOverrideIncorrectOwnerCard_AndHandleMultipleSims()
    {
        ContactRecord[] contacts = [new("0", "My Number", [new("+12025550114", "+12025550114", "Cell")], [])];
        var result = new ConversationSynthesizer().Synthesize(
            [Sms("sim1"), Sms("sim2") with { RecipientAddressing = "+12025550101" }], contacts,
            selfPhoneNumbers: ["+12025550100", "+12025550101"]);
        Assert.Equal(2, result.SelfPhones.Count);
        Assert.DoesNotContain("+12025550114", result.SelfPhones);
        Assert.Single(result.Conversations);
    }

    [Fact]
    public void UnknownDirectionAndMms_AreNotAssumedToBeDirectSms()
    {
        foreach (var message in new[] { Sms("unknown") with { Folder = "unknown", Sent = null }, Sms("mms") with { Type = "MMS" } })
        {
            var result = new ConversationSynthesizer().Synthesize([message], []);
            Assert.Equal(2, Assert.Single(result.Conversations).Participants.Count);
        }
    }

    [Fact]
    public void SuggestionsDoNotChangeOwnerIdentity()
    {
        var messages = Enumerable.Range(0, 3).Select(i => Sms(i.ToString())).ToArray();
        var candidate = Assert.Single(SelfPhoneSuggestions.Build(messages, []));
        Assert.Equal("+12025550100", candidate.Phone);
        Assert.Equal(3, candidate.IncomingSmsCount);
        Assert.Empty(new ConversationSynthesizer().Synthesize(messages, []).SelfPhones);
    }

    [Fact]
    public void SmsSenderPhoneAndEmailInOneCard_AreOneCounterparty()
    {
        var incoming = Sms("card") with { Originators = [new("Alice", ["+12025550114"], ["alice@example.test"])] };
        var result = new ConversationSynthesizer().Synthesize([incoming, Sms("reply", true)], []);
        var conversation = Assert.Single(result.Conversations);
        Assert.False(conversation.IsGroup);
        Assert.Equal(2, conversation.MessageCount);
        var participant = Assert.Single(conversation.Participants);
        Assert.Contains("+12025550114", participant.Phones);
        Assert.Contains("alice@example.test", participant.Emails);
        Assert.DoesNotContain(conversation.Participants, p => p.Phones.Contains("+12025550100"));
    }

    [Fact]
    public void SmsOutgoingCardAliases_MergeWithIncomingPhone()
    {
        var outgoing = Sms("out", true) with { Recipients = [new("Alice", ["+12025550114"], ["alice@example.test"])] };
        var result = new ConversationSynthesizer().Synthesize([Sms("in"), outgoing], []);
        Assert.False(Assert.Single(result.Conversations).IsGroup);
        Assert.Single(result.Conversations[0].Participants);
    }

    [Fact]
    public void CardAliases_DoNotCollapseRealGroup()
    {
        var incoming = Sms("group") with {
            Originators = [new("Alice", ["+12025550114"], ["alice@example.test"])],
            Recipients = [new("Owner", ["+12025550100"], []), new("Bob", ["+12025550115"], [])] };
        var result = new ConversationSynthesizer().Synthesize([incoming], [], selfPhoneNumbers: ["+12025550100"]);
        var conversation = Assert.Single(result.Conversations);
        Assert.True(conversation.IsGroup);
        Assert.Equal(2, conversation.Participants.Count);
    }

    [Fact]
    public void SharedEmailOrMultiplePhoneCard_IsNotFolded()
    {
        var shared = Sms("shared") with {
            Originators = [new("Alice", ["+12025550114"], ["shared@example.test"])],
            Recipients = [new("Bob", ["+12025550115"], ["shared@example.test"])] };
        var multiple = Sms("multiple") with {
            Originators = [new("Alice", ["+12025550114", "+12025550115"], ["alice@example.test"])] };
        foreach (var message in new[] { shared, multiple })
        {
            var result = new ConversationSynthesizer().Synthesize([message], []);
            Assert.True(Assert.Single(result.Conversations).IsGroup);
            Assert.Contains(result.Conversations[0].Participants, p => p.Key.StartsWith("email:"));
        }
    }

    [Fact]
    public void MmsCardEmails_AreNotFoldedBySmsRule()
    {
        var message = Sms("mms") with { Type = "MMS", Originators = [new("Alice", ["+12025550114"], ["alice@example.test"])] };
        var result = new ConversationSynthesizer().Synthesize([message], []);
        Assert.Contains(Assert.Single(result.Conversations).Participants, p => p.Key.StartsWith("email:"));
    }
}
