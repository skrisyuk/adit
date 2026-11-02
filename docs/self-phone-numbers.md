# Own numbers and SMS conversations

Plain incoming SMS with one sender and at most one recipient uses the sender as counterparty. The local recipient is not a group member. MMS, unknown message types/directions, multiple participants and notification group hints retain their existing handling. This local SMS rule does not infer global owner identity.

Optional confirmed numbers are scoped by exact classic Bluetooth device ID. Set `ADIT_SELF_PHONE_NUMBERS_BY_DEVICE` to a JSON object:

```json
{"Bluetooth#device-id-from-v1-devices": ["+12025550100", "+12025550101"]}
```

Numbers must use international +country-code format. A nonempty explicit list overrides the PBAP owner-card phone list for that device; an empty or missing list retains legacy owner-card detection. Other devices never inherit it.

The local `work/start-adit.ps1` reads the same object from `selfPhoneNumbersByDevice` in `work/adit-state/config.json` and passes it through the environment. Preserve the other config fields, including the secret token. Restart after changing it. UI editing is not implemented; configuration is the supported editing surface.

`GET /v1/identity?deviceId=...` returns confirmedNumbers and unconfirmed candidates, with incomingSmsCount and ownerCard evidence. It uses cached data and requires the same Bearer token as other endpoints. Candidates from repeated incoming SMS recipients or owner cards are suggestions, not proof; they are never automatically saved or applied. Review them and explicitly add correct numbers to configuration.

Normal synchronization recalculates conversation projections from stored and live messages. No database deletion or hand-written thread merge is required.

## SMS participant vCard aliases

For SMS_GSM/SMS_CDMA, a single phone plus emails in one MAP participant card may represent one person. Email aliases are folded into the phone participant only when the card has exactly one phone matching the SMS sender/recipient address, the email occurs in only that card, and it is not independently addressed. Sender/recipient roles must match. The phone key remains canonical and emails are retained as metadata. Names alone never merge people. MMS, multiple-phone cards, shared or separately addressed emails remain conservative.
