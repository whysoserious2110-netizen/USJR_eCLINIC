namespace USJR_eCLINIC.Services;

public class BiometricPreferenceService
{
    public static BiometricPreferenceService Instance { get; } =
        new BiometricPreferenceService();

    private BiometricPreferenceService()
    {
    }

    public async Task<bool> IsEnabledAsync(int userId)
    {
        try
        {
            var value =
                await SecureStorage.Default.GetAsync(
                    GetEnabledKey(userId));

            return string.Equals(
                value,
                "true",
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ShouldOfferSetupAsync(int userId)
    {
        try
        {
            var choice =
                await SecureStorage.Default.GetAsync(
                    GetChoiceKey(userId));

            return string.IsNullOrWhiteSpace(choice);
        }
        catch
        {
            return false;
        }
    }

    public async Task EnableAsync(int userId)
    {
        await SecureStorage.Default.SetAsync(
            GetEnabledKey(userId),
            "true");

        await SecureStorage.Default.SetAsync(
            GetChoiceKey(userId),
            "enabled");
    }

    public async Task DeclineAsync(int userId)
    {
        await SecureStorage.Default.SetAsync(
            GetEnabledKey(userId),
            "false");

        await SecureStorage.Default.SetAsync(
            GetChoiceKey(userId),
            "declined");
    }

    public async Task DisableAsync(int userId)
    {
        await SecureStorage.Default.SetAsync(
            GetEnabledKey(userId),
            "false");
    }

    public void ResetChoice(int userId)
    {
        SecureStorage.Default.Remove(
            GetEnabledKey(userId));

        SecureStorage.Default.Remove(
            GetChoiceKey(userId));
    }

    private static string GetEnabledKey(int userId)
    {
        return $"usjr_eclinic_biometric_enabled_{userId}";
    }

    private static string GetChoiceKey(int userId)
    {
        return $"usjr_eclinic_biometric_choice_{userId}";
    }
}