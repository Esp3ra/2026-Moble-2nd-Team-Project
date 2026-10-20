using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Independent TEST ONLY byte verifier. No runtime PC relay key or signing service.
int checks = 0;
void Check(bool condition, string message)
{ checks++; if (!condition) throw new InvalidOperationException(message); }
static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "enrollment_protocol_draft.json")));
byte[]? ticketInput = null;
foreach (var vector in fixture.RootElement.GetProperty("vectors").EnumerateArray())
{
    string Field(string name) => vector.GetProperty(name).GetString()!;
    string kind = Field("kind");
    var key = Convert.FromHexString(fixture.RootElement.GetProperty(kind == "enroll_request" ? "public_test_request_key_hex" : "public_test_result_key_hex").GetString()!);
    string tail = kind + "\0" + Field("device_id") + "\0" + Field("key_id") + "\0" + Field("payload_b64");
    byte[] input = Encoding.ASCII.GetBytes("RW-ENROLL-v1\0" + tail);
    Check(B64(Encoding.UTF8.GetBytes(Field("payload_json"))) == Field("payload_b64"), "Exact UTF8 payload bytes");
    Check(Convert.ToHexString(input).ToLowerInvariant() == Field("mac_input_hex"), "MAC input independently assembled");
    Check(B64(HMACSHA256.HashData(key, input)) == Field("mac_b64"), "Independent HMAC matches public fixture");
    Check(B64(HMACSHA256.HashData(key, Encoding.ASCII.GetBytes("RW-ID-v1\0" + tail))) != Field("mac_b64"), "Identity domain is separate");
    var tampered = input.ToArray(); tampered[^1] ^= 1;
    Check(!CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(key, input), HMACSHA256.HashData(key, tampered)), "Tampering changes MAC");
    using var payload = JsonDocument.Parse(Field("payload_json"));
    Check(payload.RootElement.GetProperty("reserved_slot").GetInt32() == 7, "Single reserved slot");
    if (kind == "enroll_request")
    {
        Check(payload.RootElement.GetProperty("target_user_id").GetInt32() == 88, "Employee is an integer distinct from administrator");
        ticketInput = input;
    }
    else
    {
        Check(!payload.RootElement.TryGetProperty("target_user_id", out _) && !payload.RootElement.TryGetProperty("nonce", out _), "Result cache needs no raw employee ID/nonce");
        Check(Convert.ToHexString(SHA256.HashData(ticketInput!)).ToLowerInvariant() == payload.RootElement.GetProperty("ticket_digest").GetString(), "Result binds exact request MAC input, not serialized envelope");
        Check(payload.RootElement.GetProperty("outcome").GetString() == "sensor_stored", "Sensor proof is not mapping completed");
    }
}
Console.WriteLine($"PASS enrollment public vectors: {checks} checks; independent C# bytes only; no runtime keys/DB/device.");
