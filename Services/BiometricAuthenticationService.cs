#if ANDROID
using Android.App;
using Android.Content;
using Android.Hardware.Biometrics;
using Android.OS;
using Java.Lang;
#endif

namespace USJR_eCLINIC.Services;

public enum BiometricAuthenticationStatus
{
    Success,
    Cancelled,
    Failed,
    NotAvailable,
    Error
}

public sealed class BiometricAuthenticationResult
{
    public BiometricAuthenticationStatus Status
    {
        get;
    }

    public string Message
    {
        get;
    }

    public bool IsSuccessful =>
        Status == BiometricAuthenticationStatus.Success;

    public BiometricAuthenticationResult(
        BiometricAuthenticationStatus status,
        string message)
    {
        Status = status;
        Message = message;
    }
}

public class BiometricAuthenticationService
{
    public static BiometricAuthenticationService Instance
    {
        get;
    } = new BiometricAuthenticationService();

    private BiometricAuthenticationService()
    {
    }

    public bool IsSupported
    {
        get
        {
#if ANDROID
            return OperatingSystem.IsAndroidVersionAtLeast(28);
#else
            return false;
#endif
        }
    }

    public async Task<BiometricAuthenticationResult>
        AuthenticateAsync()
    {
#if ANDROID
        if (!OperatingSystem.IsAndroidVersionAtLeast(28))
        {
            return new BiometricAuthenticationResult(
                BiometricAuthenticationStatus.NotAvailable,
                "Biometric authentication requires Android 9 or later.");
        }

        var activity =
            Microsoft.Maui.ApplicationModel.Platform
                .CurrentActivity;

        if (activity == null)
        {
            return new BiometricAuthenticationResult(
                BiometricAuthenticationStatus.Error,
                "The Android activity is not available.");
        }

        var completionSource =
            new TaskCompletionSource<
                BiometricAuthenticationResult>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            try
            {
                var executor =
                    activity.MainExecutor;

                var callback =
                    new AuthenticationCallback(
                        completionSource);

                var negativeButtonListener =
                    new NegativeButtonListener(
                        completionSource);

                var prompt =
                    new BiometricPrompt.Builder(activity)
                        .SetTitle(
                            new Java.Lang.String(
                                "Unlock USJ-R e-Clinic"))
                        .SetSubtitle(
                            new Java.Lang.String(
                                "Confirm your identity"))
                        .SetDescription(
                            new Java.Lang.String(
                                "Use your fingerprint to continue."))
                        .SetNegativeButton(
                            new Java.Lang.String(
                                "Use password"),
                            executor,
                            negativeButtonListener)
                        .Build();

                var cancellationSignal =
                    new CancellationSignal();

                prompt.Authenticate(
                    cancellationSignal,
                    executor,
                    callback);
            }
            catch (System.Exception ex)
            {
                completionSource.TrySetResult(
                    new BiometricAuthenticationResult(
                        BiometricAuthenticationStatus.Error,
                        ex.Message));
            }
        });

        return await completionSource.Task;
#else
        await Task.CompletedTask;

        return new BiometricAuthenticationResult(
            BiometricAuthenticationStatus.NotAvailable,
            "Biometric authentication is not available on this platform.");
#endif
    }

#if ANDROID
    private sealed class AuthenticationCallback :
        BiometricPrompt.AuthenticationCallback
    {
        private readonly TaskCompletionSource<
            BiometricAuthenticationResult>
            _completionSource;

        public AuthenticationCallback(
            TaskCompletionSource<
                BiometricAuthenticationResult>
                completionSource)
        {
            _completionSource = completionSource;
        }

        public override void OnAuthenticationSucceeded(
            BiometricPrompt.AuthenticationResult?
                result)
        {
            base.OnAuthenticationSucceeded(result);

            _completionSource.TrySetResult(
                new BiometricAuthenticationResult(
                    BiometricAuthenticationStatus.Success,
                    "Authentication successful."));
        }

        public override void OnAuthenticationFailed()
        {
            base.OnAuthenticationFailed();

            // Do not complete the task here.
            // Android allows another fingerprint attempt.
        }

        public override void OnAuthenticationError(
            BiometricErrorCode errorCode,
            ICharSequence? errorMessage)
        {
            base.OnAuthenticationError(
                errorCode,
                errorMessage);

            _completionSource.TrySetResult(
                new BiometricAuthenticationResult(
                    BiometricAuthenticationStatus.Error,
                    errorMessage?.ToString() ??
                    "Biometric authentication failed."));
        }
    }

    private sealed class NegativeButtonListener :
        Java.Lang.Object,
        IDialogInterfaceOnClickListener
    {
        private readonly TaskCompletionSource<
            BiometricAuthenticationResult>
            _completionSource;

        public NegativeButtonListener(
            TaskCompletionSource<
                BiometricAuthenticationResult>
                completionSource)
        {
            _completionSource = completionSource;
        }

        public void OnClick(
            IDialogInterface? dialog,
            int which)
        {
            _completionSource.TrySetResult(
                new BiometricAuthenticationResult(
                    BiometricAuthenticationStatus.Cancelled,
                    "Use password instead."));
        }
    }
#endif
}