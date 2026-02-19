using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Identity;
using TaskFlowApp.Services.ApiClients;

namespace TaskFlowApp.ViewModels;

public partial class MainPageViewModel(
    IdentityApiClient identityApiClient,
    IUserSession userSession,
    INavigationService navigationService) : ObservableObject
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
                ErrorMessage = "Giris yaniti bos dondu.";
                return;
            }

            userSession.SetTokens(response.AccessToken, response.RefreshToken);
            await navigationService.GoToRootAsync("DashBoardPage");
        }
        catch (ApiException ex)
        {
            ErrorMessage = $"Giris basarisiz ({ex.StatusCode}).";
        }
        catch (Exception)
        {
            ErrorMessage = "Beklenmeyen bir hata olustu.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
