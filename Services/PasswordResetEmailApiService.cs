using System.Net.Http.Json;

namespace USJR_eCLINIC.Services;

public sealed class PasswordResetEmailApiService
{
    public static PasswordResetEmailApiService Instance { get; } = new();

    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    private PasswordResetEmailApiService()
    {
    }

    public async Task<bool> CheckHealthAsync()
    {
#if ANDROID
        var baseAddress = new Uri("http://10.0.2.2:5254");
#else
        var baseAddress = new Uri("http://localhost:5254");
#endif

        try
        {
            using var response = await Client.GetAsync(
                new Uri(baseAddress, "/api/health"));
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    public async Task SendCodeAsync(string email, string code)
    {
#if ANDROID
        var baseAddress = new Uri("http://10.0.2.2:5254");
#else
        var baseAddress = new Uri("http://localhost:5254");
#endif

        using var response = await Client.PostAsJsonAsync(
            new Uri(baseAddress, "/api/password-reset/send"),
            new SendPasswordResetEmailRequest(email, code));

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("The local email service could not send the code.");
    }

    private sealed record SendPasswordResetEmailRequest(string Email, string Code);
}
