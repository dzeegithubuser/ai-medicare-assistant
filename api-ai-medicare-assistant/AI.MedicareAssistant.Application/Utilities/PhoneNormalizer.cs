namespace Application.Utilities;

/// <summary>
/// US phone-number normalization used at every account-creation boundary.
/// Stores the phone as a plain 10-digit string so uniqueness checks against the
/// <c>users.phone</c> index are consistent regardless of how the caller formatted it.
/// </summary>
public static class PhoneNormalizer
{
    /// <summary>
    /// Returns the phone as a plain 10-digit string when possible. Strips non-digits,
    /// drops a leading <c>1</c> country code, and falls back to <c>phone.Trim()</c>
    /// when the input doesn't look like a US number.
    /// </summary>
    public static string NormalizeUsPhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && digits.StartsWith('1'))
            digits = digits[1..];
        return digits.Length == 10 ? digits : phone.Trim();
    }
}
