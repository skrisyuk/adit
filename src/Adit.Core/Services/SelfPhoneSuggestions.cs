using Adit.Core.Models;
using Adit.Core.Utilities;

namespace Adit.Core.Services;

public sealed record SelfPhoneCandidate(string Phone, int IncomingSmsCount, bool OwnerCard);

public static class SelfPhoneSuggestions
{
    public static IReadOnlyList<SelfPhoneCandidate> Build(IEnumerable<MessageRecord> messages, IEnumerable<ContactRecord> contacts)
    {
        var counts = messages.Where(m => string.Equals(m.Folder, "inbox", StringComparison.OrdinalIgnoreCase)
                && m.Sent != true && (m.Type is "SMS_GSM" or "SMS_CDMA"))
            .Select(m => PhoneNumberNormalizer.Normalize(m.RecipientAddressing))
            .Where(p => p is not null && p.Length >= 8)
            .GroupBy(p => p!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        var ownerPhones = contacts.Where(c => c.UniqueIdentifier == "0"
                || string.Equals(c.DisplayName, "My Number", StringComparison.OrdinalIgnoreCase))
            .SelectMany(c => c.Phones).Select(p => PhoneNumberNormalizer.Normalize(p.Normalized ?? p.Raw))
            .Where(p => p is not null).Select(p => p!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return counts.Keys.Concat(ownerPhones).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(p => ownerPhones.Contains(p) || counts.GetValueOrDefault(p) >= 3)
            .Select(p => new SelfPhoneCandidate(p, counts.GetValueOrDefault(p), ownerPhones.Contains(p)))
            .OrderByDescending(c => c.OwnerCard).ThenByDescending(c => c.IncomingSmsCount).ToArray();
    }
}
