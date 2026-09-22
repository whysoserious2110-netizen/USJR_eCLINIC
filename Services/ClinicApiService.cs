using System.Net.Http.Json;
using System.Text.Json;

namespace USJR_eCLINIC.Services;

public sealed class ApiRegistrationResponse
{
    public bool Registered { get; set; }

    public Guid? UserId { get; set; }

    public string Message { get; set; } =
        string.Empty;
}

public sealed class ApiStudentUser
{
    public Guid Id { get; set; }

    public string StudentId { get; set; } =
        string.Empty;

    public string FullName { get; set; } =
        string.Empty;

    public string Email { get; set; } =
        string.Empty;

    public string Role { get; set; } =
        string.Empty;
}

public sealed class ApiLoginResponse
{
    public bool Authenticated { get; set; }

    public ApiStudentUser? User { get; set; }

    public string Message { get; set; } =
        string.Empty;
}

public sealed class ApiPasswordResetRequestResponse
{
    public Guid RequestId { get; set; }

    public string Message { get; set; } =
        string.Empty;
}

public sealed class ApiPasswordResetVerificationResponse
{
    public bool Verified { get; set; }

    public string ResetToken { get; set; } =
        string.Empty;

    public string Message { get; set; } =
        string.Empty;
}

public sealed class ApiPasswordResetCompletionResponse
{
    public bool Reset { get; set; }

    public string Message { get; set; } =
        string.Empty;
}

public sealed class ClinicApiService
{
    public static ClinicApiService Instance { get; } =
        new ClinicApiService();

    private readonly HttpClient _httpClient;

    private readonly JsonSerializerOptions
        _jsonOptions =
            new()
            {
                PropertyNameCaseInsensitive = true
            };

    private ClinicApiService()
    {
#if ANDROID
        const string baseAddress =
            "http://10.0.2.2:5254";
#else
        const string baseAddress =
            "http://localhost:5254";
#endif

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseAddress),

            Timeout =
                TimeSpan.FromSeconds(30)
        };
    }

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            using var response =
                await _httpClient.GetAsync(
                    "/api/health");

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ApiRegistrationResponse>
        RegisterStudentAsync(
            string fullName,
            string studentId,
            string email,
            string password)
    {
        try
        {
            var request = new
            {
                fullName,
                studentId,
                email,
                password
            };

            using var response =
                await _httpClient.PostAsJsonAsync(
                    "/api/auth/register/student",
                    request);

            var result =
                await response.Content
                    .ReadFromJsonAsync<
                        ApiRegistrationResponse>(
                        _jsonOptions);

            return result ??
                   RegistrationFailure(
                       "The server returned an " +
                       "invalid response.");
        }
        catch (TaskCanceledException)
        {
            return RegistrationFailure(
                "The request timed out. " +
                "Please try again.");
        }
        catch (HttpRequestException)
        {
            return RegistrationFailure(
                "The e-Clinic server could not be reached.");
        }
        catch
        {
            return RegistrationFailure(
                "Registration could not be completed.");
        }
    }

    public async Task<ApiRegistrationResponse>
        RegisterPatientAsync(
            string fullName,
            string idNumber,
            string email,
            string password,
            string role)
    {
        try
        {
            var request = new
            {
                fullName,
                idNumber,
                email,
                password,
                role
            };

            using var response =
                await _httpClient.PostAsJsonAsync(
                    "/api/auth/register/patient",
                    request);

            var result =
                await response.Content
                    .ReadFromJsonAsync<
                        ApiRegistrationResponse>(
                        _jsonOptions);

            return result ??
                   RegistrationFailure(
                       "The server returned an " +
                       "invalid response.");
        }
        catch (TaskCanceledException)
        {
            return RegistrationFailure(
                "The request timed out. " +
                "Please try again.");
        }
        catch (HttpRequestException)
        {
            return RegistrationFailure(
                "The e-Clinic server could not be reached.");
        }
        catch
        {
            return RegistrationFailure(
                "Registration could not be completed.");
        }
    }

    public async Task<ApiLoginResponse>
        LoginPatientAsync(
            string emailOrId,
            string password)
    {
        try
        {
            var request = new
            {
                emailOrId,
                password
            };

            using var response =
                await _httpClient.PostAsJsonAsync(
                    "/api/auth/login/patient",
                    request);

            var result =
                await response.Content
                    .ReadFromJsonAsync<
                        ApiLoginResponse>(
                        _jsonOptions);

            return result ??
                   LoginFailure(
                       "The server returned an " +
                       "invalid response.");
        }
        catch (TaskCanceledException)
        {
            return LoginFailure(
                "The request timed out. " +
                "Please try again.");
        }
        catch (HttpRequestException)
        {
            return LoginFailure(
                "The e-Clinic server could not be reached.");
        }
        catch
        {
            return LoginFailure(
                "Login could not be completed.");
        }
    }

    public async Task<ApiLoginResponse>
        LoginStudentAsync(
            string emailOrStudentId,
            string password)
    {
        try
        {
            var request = new
            {
                emailOrStudentId,
                password
            };

            using var response =
                await _httpClient.PostAsJsonAsync(
                    "/api/auth/login/student",
                    request);

            var result =
                await response.Content
                    .ReadFromJsonAsync<
                        ApiLoginResponse>(
                        _jsonOptions);

            return result ??
                   LoginFailure(
                       "The server returned an " +
                       "invalid response.");
        }
        catch (TaskCanceledException)
        {
            return LoginFailure(
                "The request timed out. " +
                "Please try again.");
        }
        catch (HttpRequestException)
        {
            return LoginFailure(
                "The e-Clinic server could not be reached.");
        }
        catch
        {
            return LoginFailure(
                "Login could not be completed.");
        }
    }

    public async Task<
        ApiPasswordResetRequestResponse>
        RequestPasswordResetAsync(
            string studentId)
    {
        try
        {
            var request = new
            {
                studentId
            };

            using var response =
                await _httpClient.PostAsJsonAsync(
                    "/api/password-reset/request",
                    request);

            var result =
                await response.Content
                    .ReadFromJsonAsync<
                        ApiPasswordResetRequestResponse>(
                        _jsonOptions);

            return result ??
                   new ApiPasswordResetRequestResponse
                   {
                       Message =
                           "The server returned an " +
                           "invalid response."
                   };
        }
        catch (TaskCanceledException)
        {
            return new ApiPasswordResetRequestResponse
            {
                Message =
                    "The request timed out. " +
                    "Please try again."
            };
        }
        catch (HttpRequestException)
        {
            return new ApiPasswordResetRequestResponse
            {
                Message =
                    "The e-Clinic server could not be reached."
            };
        }
        catch
        {
            return new ApiPasswordResetRequestResponse
            {
                Message =
                    "The reset request could not be completed."
            };
        }
    }

    public async Task<
        ApiPasswordResetVerificationResponse>
        VerifyPasswordResetCodeAsync(
            Guid requestId,
            string verificationCode)
    {
        try
        {
            var request = new
            {
                requestId =
                    requestId.ToString(),

                verificationCode
            };

            using var response =
                await _httpClient.PostAsJsonAsync(
                    "/api/password-reset/verify",
                    request);

            var result =
                await response.Content
                    .ReadFromJsonAsync<
                        ApiPasswordResetVerificationResponse>(
                        _jsonOptions);

            return result ??
                   VerificationFailure(
                       "The server returned an " +
                       "invalid response.");
        }
        catch (TaskCanceledException)
        {
            return VerificationFailure(
                "The request timed out. " +
                "Please try again.");
        }
        catch (HttpRequestException)
        {
            return VerificationFailure(
                "The e-Clinic server could not be reached.");
        }
        catch
        {
            return VerificationFailure(
                "The code could not be verified.");
        }
    }

    public async Task<
        ApiPasswordResetCompletionResponse>
        CompletePasswordResetAsync(
            Guid requestId,
            string resetToken,
            string newPassword,
            string confirmPassword)
    {
        try
        {
            var request = new
            {
                requestId =
                    requestId.ToString(),

                resetToken,
                newPassword,
                confirmPassword
            };

            using var response =
                await _httpClient.PostAsJsonAsync(
                    "/api/password-reset/complete",
                    request);

            var result =
                await response.Content
                    .ReadFromJsonAsync<
                        ApiPasswordResetCompletionResponse>(
                        _jsonOptions);

            return result ??
                   CompletionFailure(
                       "The server returned an " +
                       "invalid response.");
        }
        catch (TaskCanceledException)
        {
            return CompletionFailure(
                "The request timed out. " +
                "Please try again.");
        }
        catch (HttpRequestException)
        {
            return CompletionFailure(
                "The e-Clinic server could not be reached.");
        }
        catch
        {
            return CompletionFailure(
                "The password could not be reset.");
        }
    }

    private static ApiRegistrationResponse
        RegistrationFailure(string message)
    {
        return new ApiRegistrationResponse
        {
            Registered = false,
            Message = message
        };
    }

    private static ApiLoginResponse
        LoginFailure(string message)
    {
        return new ApiLoginResponse
        {
            Authenticated = false,
            Message = message
        };
    }

    private static
        ApiPasswordResetVerificationResponse
        VerificationFailure(string message)
    {
        return new ApiPasswordResetVerificationResponse
        {
            Verified = false,
            Message = message
        };
    }

    private static
        ApiPasswordResetCompletionResponse
        CompletionFailure(string message)
    {
        return new ApiPasswordResetCompletionResponse
        {
            Reset = false,
            Message = message
        };
    }
}