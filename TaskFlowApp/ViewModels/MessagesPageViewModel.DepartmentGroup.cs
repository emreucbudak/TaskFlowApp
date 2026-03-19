using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Constants;
using TaskFlowApp.Models.Identity;
using TaskFlowApp.Services.ApiClients;

namespace TaskFlowApp.ViewModels;

public partial class MessagesPageViewModel
{
    public ObservableCollection<LeaderDepartmentOption> ManagedDepartments { get; } = [];
    public ObservableCollection<SelectableDepartmentUserItem> EligibleDepartmentUsers { get; } = [];

    [ObservableProperty]
    private bool isDepartmentLeader;

    [ObservableProperty]
    private bool isCreatingDepartmentGroup;

    [ObservableProperty]
    private string groupNameInput = string.Empty;

    [ObservableProperty]
    private string groupCreationStatus = string.Empty;

    [ObservableProperty]
    private LeaderDepartmentOption? selectedManagedDepartment;

    partial void OnSelectedManagedDepartmentChanged(LeaderDepartmentOption? value)
    {
        GroupCreationStatus = string.Empty;
        RefreshEligibleDepartmentUsers();
    }

    [RelayCommand]
    private async Task CreateDepartmentGroupAsync()
    {
        if (IsCreatingDepartmentGroup || !IsDepartmentLeader)
        {
            return;
        }

        if (UserSession.CompanyId is not Guid companyId)
        {
            ErrorMessage = "Sirket bilgisi bulunamadi. Tekrar giris yapin.";
            return;
        }

        if (SelectedManagedDepartment is null)
        {
            ErrorMessage = "Grup icin yonettiginiz departmani secin.";
            return;
        }

        var trimmedGroupName = GroupNameInput.Trim();
        if (trimmedGroupName.Length < 3 || trimmedGroupName.Length > 100)
        {
            ErrorMessage = "Grup adi 3 ile 100 karakter arasinda olmalidir.";
            return;
        }

        var selectedUserIds = EligibleDepartmentUsers
            .Where(item => item.IsSelected)
            .Select(item => item.UserId)
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToList();

        if (selectedUserIds.Count == 0)
        {
            ErrorMessage = "Gruba eklemek icin en az bir calisan secin.";
            return;
        }

        try
        {
            IsCreatingDepartmentGroup = true;
            ErrorMessage = string.Empty;
            GroupCreationStatus = string.Empty;

            await identityApiClient.AddGroupsCommandRequestAsync(new
            {
                Name = trimmedGroupName,
                CompanyId = companyId,
                DepartmentId = SelectedManagedDepartment.DepartmentId,
                UserIds = selectedUserIds
            });

            foreach (var item in EligibleDepartmentUsers)
            {
                item.IsSelected = false;
            }

            GroupNameInput = string.Empty;
            GroupCreationStatus = "Departman grubu basariyla olusturuldu.";
            StatusText = $"{trimmedGroupName} grubu olusturuldu. Secilen calisan sayisi: {selectedUserIds.Count}";
        }
        catch (ApiException ex) when (ex.StatusCode == 400)
        {
            ErrorMessage = "Grup olusturulamadi. Grup adi ve secilen kullanicilari kontrol edin.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Grup olusturulamadi. Lutfen tekrar deneyin.");
        }
        catch (HttpRequestException)
        {
            ErrorMessage = GenericConnectionErrorMessage;
        }
        catch (Exception)
        {
            ErrorMessage = "Grup olusturulurken bir sorun olustu. Lutfen tekrar deneyin.";
        }
        finally
        {
            IsCreatingDepartmentGroup = false;
        }
    }

    private void ConfigureDepartmentGroupState(Guid currentUserId)
    {
        ManagedDepartments.Clear();
        EligibleDepartmentUsers.Clear();
        IsDepartmentLeader = false;
        GroupCreationStatus = string.Empty;
        SelectedManagedDepartment = null;

        if (!string.Equals(UserSession.Role, AppRoles.Worker, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var currentUser = allCompanyUsers.FirstOrDefault(user => user.Id == currentUserId);
        if (currentUser is null)
        {
            return;
        }

        var managedDepartments = currentUser.DepartmentMemberships
            .Where(membership => membership.DepartmentId != Guid.Empty && membership.DepartmentRoleId == DepartmentLeaderRoleId)
            .Select(membership => new LeaderDepartmentOption
            {
                DepartmentId = membership.DepartmentId,
                DepartmentName = string.IsNullOrWhiteSpace(membership.DepartmentName)
                    ? "Departman"
                    : membership.DepartmentName.Trim()
            })
            .DistinctBy(item => item.DepartmentId)
            .OrderBy(item => item.DepartmentName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (managedDepartments.Count == 0)
        {
            return;
        }

        foreach (var department in managedDepartments)
        {
            ManagedDepartments.Add(department);
        }

        IsDepartmentLeader = true;
        SelectedManagedDepartment = ManagedDepartments[0];
        RefreshEligibleDepartmentUsers();
    }

    private void RefreshEligibleDepartmentUsers()
    {
        var selectedIds = EligibleDepartmentUsers
            .Where(item => item.IsSelected)
            .Select(item => item.UserId)
            .ToHashSet();

        EligibleDepartmentUsers.Clear();

        if (SelectedManagedDepartment is null || UserSession.UserId is not Guid currentUserId)
        {
            return;
        }

        var selectedDepartmentId = SelectedManagedDepartment.DepartmentId;
        var selectedDepartmentName = SelectedManagedDepartment.DepartmentName;

        var eligibleUsers = allCompanyUsers
            .Where(user => user.Id != Guid.Empty && user.Id != currentUserId)
            .Where(user => user.DepartmentMemberships.Any(membership => membership.DepartmentId == selectedDepartmentId))
            .OrderBy(user => ResolveDisplayName(user), StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var user in eligibleUsers)
        {
            EligibleDepartmentUsers.Add(new SelectableDepartmentUserItem
            {
                UserId = user.Id,
                Name = ResolveDisplayName(user),
                DepartmentName = selectedDepartmentName,
                IsSelected = selectedIds.Contains(user.Id)
            });
        }
    }
}

public sealed record LeaderDepartmentOption
{
    public Guid DepartmentId { get; init; }
    public string DepartmentName { get; init; } = string.Empty;
}

public partial class SelectableDepartmentUserItem : ObservableObject
{
    public Guid UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string DepartmentName { get; init; } = string.Empty;

    [ObservableProperty]
    private bool isSelected;
}
