using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Identity;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Services.Realtime;

namespace TaskFlowApp.ViewModels;

public partial class MainPageViewModel(
    IdentityApiClient identityApiClient,
    IUserSession userSession,
    INavigationService navigationService,
    IRealtimeConnectionManager realtimeConnectionManager) : ObservableObject
{
    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "E-posta ve sifre zorunludur.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            var response = await identityApiClient.LoginCommandRequestAsync(new LoginCommandRequestDto
            {
                Email = Email.Trim(),
                Password = Password
            });

            if (response is null || string.IsNullOrWhiteSpace(response.AccessToken))
            {
                ErrorMessage = "Giris su anda tamamlanamiyor. Lutfen tekrar deneyin.";
                return;
            }

            userSession.SetRawTokens(response.AccessToken, response.RefreshToken);

            var currentUserContext = await identityApiClient.GetCurrentUserContextAsync();
            if (currentUserContext is null ||
                currentUserContext.UserId == Guid.Empty ||
                currentUserContext.CompanyId == Guid.Empty ||
                string.IsNullOrWhiteSpace(currentUserContext.Role))
            {
                userSession.Clear();
                ErrorMessage = "Sirket bilgisi alinamadi. Lutfen tekrar giris yapin.";
                return;
            }

            userSession.SetTokens(
                response.AccessToken,
                response.RefreshToken,
                currentUserContext.UserId,
                currentUserContext.CompanyId,
                currentUserContext.Role);
            try
            {
                await realtimeConnectionManager.ConnectAllAsync();
            }
            catch
            {
                // Login should still succeed even if realtime channels are temporarily unavailable.
            }

            var targetRoute = string.Equals(userSession.Role, "company", StringComparison.OrdinalIgnoreCase)
                ? "CompanyDashboardPage"
                : "DashBoardPage";

            await navigationService.GoToRootAsync(targetRoute);
        }
        catch (ApiException ex) when (ex.StatusCode is 400 or 401 or 403)
        {
            ErrorMessage = "E-posta veya sifre hatali.";
        }
        catch (ApiException)
        {
            ErrorMessage = "Giris su anda yapilamiyor. Lutfen tekrar deneyin.";
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Giris su anda yapilamiyor. Lutfen tekrar deneyin.";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "Giris su anda yapilamiyor. Lutfen tekrar deneyin.";
        }
        catch (Exception)
        {
            ErrorMessage = "Beklenmeyen bir sorun olustu. Lutfen tekrar deneyin.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
