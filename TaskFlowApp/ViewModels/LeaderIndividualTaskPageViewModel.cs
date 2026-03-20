using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Authorization;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Identity;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Services.Realtime;
using TaskFlowApp.Infrastructure.Constants;
using TaskFlowApp.Services.State;

namespace TaskFlowApp.ViewModels;

public partial class LeaderIndividualTaskPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    IdentityApiClient identityApiClient,
    ProjectManagementApiClient projectManagementApiClient,
    IWorkerReportAccessResolver workerReportAccessResolver,
    IWorkerDashboardStateService workerDashboardStateService) : PageViewModelBase(navigationService, userSession, realtimeConnectionManager, workerReportAccessResolver, workerDashboardStateService)
{
    private const string AccessDeniedMessageText = "Bu sayfa sadece şirket hesabı veya departman liderleri içindir.";
    private const int DepartmentLeaderRoleId = 1;

    private readonly List<CompanyUserDto> allCompanyUsers = [];
    private readonly List<CompanyGroupDto> allCompanyGroups = [];

    public ObservableCollection<AssignableDepartmentUserOption> EligibleUsers { get; } = [];
    public ObservableCollection<TaskPriorityOption> PriorityOptions { get; } = [];
    public ObservableCollection<GroupDepartmentOption> ManagedDepartments { get; } = [];
    public ObservableCollection<SelectableGroupUserOption> GroupEligibleUsers { get; } = [];
    public ObservableCollection<SelectableGroupUserOption> SelectedGroupMembers { get; } = [];
    public ObservableCollection<SelectableGroupUserOption> AvailableGroupUserOptions { get; } = [];
    public ObservableCollection<ManageableGroupOption> AvailableGroups { get; } = [];

    [ObservableProperty]
    private bool hasManagementAccess;

    [ObservableProperty]
    private string accessMessage = AccessDeniedMessageText;

    [ObservableProperty]
    private AssignableDepartmentUserOption? selectedAssignedUser;

    [ObservableProperty]
    private TaskPriorityOption? selectedPriority;

    [ObservableProperty]
    private string taskTitle = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private DateTime deadlineDate = DateTime.Today.AddDays(1);

    [ObservableProperty]
    private DateTime minimumDeadlineDate = DateTime.Today;

    [ObservableProperty]
    private bool isCreatingTask;

    [ObservableProperty]
    private string createStatus = string.Empty;

    [ObservableProperty]
    private string currentDepartmentName = string.Empty;

    [ObservableProperty]
    private bool isCompanyUser;

    [ObservableProperty]
    private bool canCreateGroups;

    [ObservableProperty]
    private bool canDeleteGroups;

    [ObservableProperty]
    private string groupCreateHelperText = string.Empty;

    [ObservableProperty]
    private GroupDepartmentOption? selectedManagedDepartment;

    [ObservableProperty]
    private string groupNameInput = string.Empty;

    [ObservableProperty]
    private SelectableGroupUserOption? selectedGroupUserToAdd;

    [ObservableProperty]
    private bool isCreatingGroup;

    [ObservableProperty]
    private string groupCreationStatus = string.Empty;

    [ObservableProperty]
    private bool isGroupTaskEnabled = true;

    [ObservableProperty]
    private string groupTaskTitle = string.Empty;

    [ObservableProperty]
    private string groupTaskDescription = string.Empty;

    [ObservableProperty]
    private DateTime groupTaskDeadlineDate = DateTime.Today.AddDays(1);

    [ObservableProperty]
    private TaskPriorityOption? selectedGroupTaskPriority;

    public ObservableCollection<GroupMemberSubTaskOption> GroupMemberSubTasks { get; } = [];

    [ObservableProperty]
    private string groupDeleteHelperText = string.Empty;

    [ObservableProperty]
    private ManageableGroupOption? selectedGroupToDelete;

    [ObservableProperty]
    private bool isDeletingGroup;

    [ObservableProperty]
    private string groupDeletionStatus = string.Empty;

    [ObservableProperty]
    private string selectedDeleteGroupDepartmentsText = string.Empty;

    [ObservableProperty]
    private string selectedDeleteGroupMembersText = string.Empty;

    public bool HasNoManagementAccess => !HasManagementAccess;
    public bool ShowReportsNavigation => IsCompanyUser || CanAccessReportsPage;
    public bool ShowManagementNavigation => HasManagementAccess;
    public bool ShowGroupDepartmentPicker => !IsCompanyUser && ManagedDepartments.Count > 0;
    public bool HasSelectedGroupToDelete => SelectedGroupToDelete is not null;
    public bool CanUseDeleteGroupForm => CanDeleteGroups && HasSelectedGroupToDelete && !IsDeletingGroup;
    public string GroupCreateButtonText => "Grup ve Görev Oluştur";

    partial void OnHasManagementAccessChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoManagementAccess));
        OnPropertyChanged(nameof(ShowManagementNavigation));
    }

    partial void OnIsCompanyUserChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowReportsNavigation));
        OnPropertyChanged(nameof(ShowGroupDepartmentPicker));
    }

    partial void OnCanDeleteGroupsChanged(bool value)
    {
        RefreshGroupDeleteHelperText();
        OnPropertyChanged(nameof(CanUseDeleteGroupForm));
    }

    partial void OnIsGroupTaskEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(GroupCreateButtonText));
        if (value)
        {
            RefreshGroupMemberSubTasks();
            if (SelectedGroupTaskPriority is null)
            {
                SelectedGroupTaskPriority = PriorityOptions.FirstOrDefault(item => item.Id == 3) ?? PriorityOptions.FirstOrDefault();
            }
        }
    }

    partial void OnSelectedManagedDepartmentChanged(GroupDepartmentOption? value)
    {
        GroupCreationStatus = string.Empty;
        RefreshGroupEligibleUsers();
    }

    partial void OnSelectedGroupToDeleteChanged(ManageableGroupOption? value)
    {
        GroupDeletionStatus = string.Empty;
        SelectedDeleteGroupDepartmentsText = value?.DepartmentsSummary ?? string.Empty;
        SelectedDeleteGroupMembersText = value?.MembersSummary ?? string.Empty;
        OnPropertyChanged(nameof(HasSelectedGroupToDelete));
        OnPropertyChanged(nameof(CanUseDeleteGroupForm));
    }

    partial void OnIsDeletingGroupChanged(bool value)
    {
        OnPropertyChanged(nameof(CanUseDeleteGroupForm));
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (UserSession.UserId is not Guid currentUserId)
        {
            ErrorMessage = "Kullanıcı bilgisi bulunamadı. Tekrar giriş yapın.";
            return;
        }

        if (UserSession.CompanyId is not Guid companyId)
        {
            ErrorMessage = "Şirket bilgisi bulunamadı. Tekrar giriş yapın.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            CreateStatus = string.Empty;
            GroupCreationStatus = string.Empty;
            GroupDeletionStatus = string.Empty;
            MinimumDeadlineDate = DateTime.Today;
            if (DeadlineDate < MinimumDeadlineDate)
            {
                DeadlineDate = MinimumDeadlineDate;
            }

            IsCompanyUser = string.Equals(UserSession.Role, AppRoles.Company, StringComparison.OrdinalIgnoreCase);

            var accessState = WorkerReportAccessState.None;
            if (!IsCompanyUser)
            {
                accessState = await LoadWorkerReportAccessStateAsync();
            }

            var companyUsersTask = identityApiClient.GetAllCompanyUsersAsync(companyId);
            var companyGroupsTask = identityApiClient.GetAllCompanyGroupsAsync(companyId);

            await Task.WhenAll(companyUsersTask, companyGroupsTask);

            allCompanyUsers.Clear();
            allCompanyUsers.AddRange((await companyUsersTask ?? [])
                .Where(user => user.Id != Guid.Empty));

            allCompanyGroups.Clear();
            allCompanyGroups.AddRange(await companyGroupsTask ?? []);

            ConfigureManagedDepartments(currentUserId, accessState);
            ConfigureAccessState(accessState);
            EnsurePriorityOptions();

            if (!HasManagementAccess)
            {
                ApplyAccessDeniedState();
                return;
            }

            RefreshTaskEligibleUsers();
            RefreshGroupEligibleUsers();
            RefreshAvailableGroups();
            RefreshGroupCreateHelperText();
            RefreshGroupDeleteHelperText();

            StatusText = BuildReadyStatusMessage();
            OnPropertyChanged(nameof(ShowReportsNavigation));
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, GenericLoadErrorMessage);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = GenericConnectionErrorMessage;
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = GenericConnectionErrorMessage;
        }
        catch (Exception)
        {
            ErrorMessage = "Bir sorun oluştu. Lütfen tekrar deneyin.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateTaskAsync()
    {
        if (IsCreatingTask || !HasManagementAccess)
        {
            return;
        }

        if (UserSession.CompanyId is not Guid companyId)
        {
            ErrorMessage = "Şirket bilgisi bulunamadı. Tekrar giriş yapın.";
            return;
        }

        if (SelectedAssignedUser is null)
        {
            ErrorMessage = "Görevi atamak için bir kullanıcı seçin.";
            return;
        }

        if (SelectedPriority is null)
        {
            ErrorMessage = "Görev önceliği seçilmelidir.";
            return;
        }

        var trimmedTitle = TaskTitle.Trim();
        if (trimmedTitle.Length < 3 || trimmedTitle.Length > 120)
        {
            ErrorMessage = "Görev başlığı 3 ile 120 karakter arasında olmalıdır.";
            return;
        }

        var trimmedDescription = Description.Trim();
        if (trimmedDescription.Length < 5 || trimmedDescription.Length > 1000)
        {
            ErrorMessage = "Görev açıklaması 5 ile 1000 karakter arasında olmalıdır.";
            return;
        }

        var resolvedDeadline = DateOnly.FromDateTime(DeadlineDate.Date);
        if (resolvedDeadline < DateOnly.FromDateTime(DateTime.Today))
        {
            ErrorMessage = "Teslim tarihi bugünden önce olamaz.";
            return;
        }

        try
        {
            IsCreatingTask = true;
            ErrorMessage = string.Empty;
            CreateStatus = string.Empty;

            var assignedUserName = SelectedAssignedUser.Name;
            var assignedUserId = SelectedAssignedUser.UserId;

            await projectManagementApiClient.CreateIndividualTaskCommandRequestAsync(new
            {
                AssignedUserId = assignedUserId,
                TaskTitle = trimmedTitle,
                Description = trimmedDescription,
                Deadline = resolvedDeadline,
                CompanyId = companyId,
                TaskPriorityCategoryId = SelectedPriority.Id
            });

            TaskTitle = string.Empty;
            Description = string.Empty;
            DeadlineDate = MinimumDeadlineDate.AddDays(1);
            SelectedAssignedUser = EligibleUsers.FirstOrDefault(item => item.UserId == assignedUserId)
                ?? EligibleUsers.FirstOrDefault();
            CreateStatus = $"Görev {assignedUserName} kullanıcısına başarıyla atandı.";
            StatusText = IsCompanyUser
                ? $"Yeni bireysel görev {assignedUserName} kullanıcısına atandı."
                : $"{CurrentDepartmentName} için yeni bireysel görev atandı.";
        }
        catch (ApiException ex) when (ex.StatusCode == 400)
        {
            ErrorMessage = "Görev oluşturulamadı. Alanları ve öncelik bilgisini kontrol edin.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Görev oluşturulamadı. Lütfen tekrar deneyin.");
        }
        catch (HttpRequestException)
        {
            ErrorMessage = GenericConnectionErrorMessage;
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = GenericConnectionErrorMessage;
        }
        catch (Exception)
        {
            ErrorMessage = "Görev oluşturulurken bir sorun oluştu. Lütfen tekrar deneyin.";
        }
        finally
        {
            IsCreatingTask = false;
        }
    }

    [RelayCommand]
    private void AddGroupMember()
    {
        if (SelectedGroupUserToAdd is null)
        {
            return;
        }

        var user = SelectedGroupUserToAdd;
        user.IsSelected = true;
        SelectedGroupMembers.Add(user);
        AvailableGroupUserOptions.Remove(user);
        SelectedGroupUserToAdd = null;

        RefreshGroupMemberSubTasks();
    }

    [RelayCommand]
    private void RemoveGroupMember(SelectableGroupUserOption? user)
    {
        if (user is null)
        {
            return;
        }

        user.IsSelected = false;
        SelectedGroupMembers.Remove(user);

        var insertIndex = 0;
        foreach (var existing in AvailableGroupUserOptions)
        {
            if (string.Compare(existing.Name, user.Name, StringComparison.OrdinalIgnoreCase) < 0)
            {
                insertIndex++;
            }
            else
            {
                break;
            }
        }
        AvailableGroupUserOptions.Insert(insertIndex, user);

        RefreshGroupMemberSubTasks();
    }

    [RelayCommand]
    private async Task CreateGroupAsync()
    {
        if (IsCreatingGroup || !CanCreateGroups)
        {
            return;
        }

        if (UserSession.CompanyId is not Guid companyId)
        {
            ErrorMessage = "Şirket bilgisi bulunamadı. Tekrar giriş yapın.";
            return;
        }

        Guid? departmentId = null;
        if (ShowGroupDepartmentPicker)
        {
            if (SelectedManagedDepartment is null)
            {
                ErrorMessage = "Grup için yönettiğiniz departmanı seçin.";
                return;
            }

            departmentId = SelectedManagedDepartment.DepartmentId;
        }

        var trimmedGroupName = GroupNameInput.Trim();
        if (trimmedGroupName.Length < 3 || trimmedGroupName.Length > 100)
        {
            ErrorMessage = "Grup adı 3 ile 100 karakter arasında olmalıdır.";
            return;
        }

        var selectedUserIds = SelectedGroupMembers
            .Select(item => item.UserId)
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToList();

        if (selectedUserIds.Count == 0)
        {
            ErrorMessage = "Gruba eklemek için en az bir kullanıcı seçin.";
            return;
        }

        try
        {
            IsCreatingGroup = true;
            ErrorMessage = string.Empty;
            GroupCreationStatus = string.Empty;

            await identityApiClient.AddGroupsCommandRequestAsync(new
            {
                Name = trimmedGroupName,
                CompanyId = companyId,
                DepartmentId = departmentId,
                UserIds = selectedUserIds
            });

            foreach (var item in GroupEligibleUsers)
            {
                item.IsSelected = false;
            }

            SelectedGroupMembers.Clear();
            AvailableGroupUserOptions.Clear();
            foreach (var item in GroupEligibleUsers.OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase))
            {
                AvailableGroupUserOptions.Add(item);
            }
            SelectedGroupUserToAdd = null;

            GroupNameInput = string.Empty;
            await ReloadGroupsAsync(companyId);

            try
            {
                var trimmedGroupTaskTitle = GroupTaskTitle.Trim();
                if (trimmedGroupTaskTitle.Length < 3 || trimmedGroupTaskTitle.Length > 120)
                {
                    GroupCreationStatus = "Grup oluşturuldu ancak grup görevi oluşturulamadı: Görev başlığı 3 ile 120 karakter arasında olmalıdır.";
                    ResetGroupTaskFields();
                    return;
                }

                var trimmedGroupTaskDescription = GroupTaskDescription.Trim();
                if (trimmedGroupTaskDescription.Length < 5 || trimmedGroupTaskDescription.Length > 1000)
                {
                    GroupCreationStatus = "Grup oluşturuldu ancak grup görevi oluşturulamadı: Görev açıklaması 5 ile 1000 karakter arasında olmalıdır.";
                    ResetGroupTaskFields();
                    return;
                }

                var groupTaskDeadline = DateOnly.FromDateTime(GroupTaskDeadlineDate.Date);
                if (groupTaskDeadline < DateOnly.FromDateTime(DateTime.Today))
                {
                    GroupCreationStatus = "Grup oluşturuldu ancak grup görevi oluşturulamadı: Teslim tarihi bugünden önce olamaz.";
                    ResetGroupTaskFields();
                    return;
                }

                if (SelectedGroupTaskPriority is null)
                {
                    GroupCreationStatus = "Grup oluşturuldu ancak grup görevi oluşturulamadı: Görev önceliği seçilmelidir.";
                    ResetGroupTaskFields();
                    return;
                }

                var subTaskAssignments = GroupMemberSubTasks
                    .Select(item => new
                    {
                        AssignedUserId = item.UserId,
                        TaskTitle = string.IsNullOrWhiteSpace(item.SubTaskTitle) ? $"{trimmedGroupTaskTitle} - {item.MemberName}" : item.SubTaskTitle.Trim(),
                        Description = string.IsNullOrWhiteSpace(item.SubTaskDescription) ? trimmedGroupTaskDescription : item.SubTaskDescription.Trim()
                    })
                    .ToList();

                await projectManagementApiClient.CreateGroupTaskWithSubTasksCommandRequestAsync(new
                {
                    TaskName = trimmedGroupTaskTitle,
                    Description = trimmedGroupTaskDescription,
                    DeadlineTime = GroupTaskDeadlineDate,
                    TaskPriorityCategoryId = SelectedGroupTaskPriority.Id,
                    SubTaskAssignments = subTaskAssignments
                });

                ResetGroupTaskFields();
                GroupCreationStatus = "Grup ve grup görevi başarıyla oluşturuldu.";
                StatusText = $"{trimmedGroupName} grubu ve görevi oluşturuldu. Üye sayısı: {selectedUserIds.Count}";
            }
            catch (Exception)
            {
                GroupCreationStatus = "Grup oluşturuldu ancak grup görevi oluşturulamadı. Lütfen tekrar deneyin.";
            }
        }
        catch (ApiException ex) when (ex.StatusCode == 400)
        {
            ErrorMessage = "Grup oluşturulamadı. Grup adı ve seçilen kullanıcıları kontrol edin.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Grup oluşturulamadı. Lütfen tekrar deneyin.");
        }
        catch (HttpRequestException)
        {
            ErrorMessage = GenericConnectionErrorMessage;
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = GenericConnectionErrorMessage;
        }
        catch (Exception)
        {
            ErrorMessage = "Grup oluşturulurken bir sorun oluştu. Lütfen tekrar deneyin.";
        }
        finally
        {
            IsCreatingGroup = false;
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedGroupAsync()
    {
        if (IsDeletingGroup)
        {
            return;
        }

        if (!CanDeleteGroups)
        {
            ErrorMessage = "Grup silme yetkiniz bulunmuyor.";
            return;
        }

        if (UserSession.CompanyId is not Guid companyId)
        {
            ErrorMessage = "Şirket bilgisi bulunamadı. Tekrar giriş yapın.";
            return;
        }

        if (SelectedGroupToDelete is null || SelectedGroupToDelete.GroupId == Guid.Empty)
        {
            ErrorMessage = "Silmek için bir grup seçin.";
            return;
        }

        try
        {
            IsDeletingGroup = true;
            ErrorMessage = string.Empty;
            GroupDeletionStatus = string.Empty;

            var deletedGroupId = SelectedGroupToDelete.GroupId;
            var deletedGroupName = SelectedGroupToDelete.GroupName;

            await identityApiClient.DeleteGroupsCommandRequestAsync(new
            {
                GroupId = deletedGroupId
            });

            await ReloadGroupsAsync(companyId);
            GroupDeletionStatus = $"{deletedGroupName} grubu silindi.";
            StatusText = $"{deletedGroupName} grubu kaldirildi.";
        }
        catch (ApiException ex) when (ex.StatusCode == 400)
        {
            ErrorMessage = "Grup silinemedi. Seçimi tekrar kontrol edin.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Grup silinemedi. Lütfen tekrar deneyin.");
        }
        catch (HttpRequestException)
        {
            ErrorMessage = GenericConnectionErrorMessage;
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = GenericConnectionErrorMessage;
        }
        catch (Exception)
        {
            ErrorMessage = "Grup silinirken bir sorun oluştu. Lütfen tekrar deneyin.";
        }
        finally
        {
            IsDeletingGroup = false;
        }
    }

    private void ConfigureManagedDepartments(Guid currentUserId, WorkerReportAccessState accessState)
    {
        var preservedDepartmentId = SelectedManagedDepartment?.DepartmentId;

        ManagedDepartments.Clear();
        SelectedManagedDepartment = null;

        if (IsCompanyUser)
        {
            OnPropertyChanged(nameof(ShowGroupDepartmentPicker));
            return;
        }

        var managedDepartments = new List<GroupDepartmentOption>();
        var currentUser = allCompanyUsers.FirstOrDefault(user => user.Id == currentUserId);
        if (currentUser is not null)
        {
            managedDepartments.AddRange(currentUser.DepartmentMemberships
                .Where(membership => membership.DepartmentId != Guid.Empty && membership.DepartmentRoleId == DepartmentLeaderRoleId)
                .Select(membership => new GroupDepartmentOption
                {
                    DepartmentId = membership.DepartmentId,
                    DepartmentName = string.IsNullOrWhiteSpace(membership.DepartmentName)
                        ? "Departman"
                        : membership.DepartmentName.Trim()
                }));
        }

        if (accessState.CanAccessReportsPage && accessState.DepartmentId is Guid departmentId && departmentId != Guid.Empty)
        {
            managedDepartments.Add(new GroupDepartmentOption
            {
                DepartmentId = departmentId,
                DepartmentName = string.IsNullOrWhiteSpace(accessState.DepartmentName)
                    ? "Departman"
                    : accessState.DepartmentName.Trim()
            });
        }

        foreach (var department in managedDepartments
            .DistinctBy(item => item.DepartmentId)
            .OrderBy(item => item.DepartmentName, StringComparer.OrdinalIgnoreCase))
        {
            ManagedDepartments.Add(department);
        }

        SelectedManagedDepartment = ManagedDepartments.FirstOrDefault(item => item.DepartmentId == preservedDepartmentId)
            ?? ManagedDepartments.FirstOrDefault();

        OnPropertyChanged(nameof(ShowGroupDepartmentPicker));
    }

    private void ConfigureAccessState(WorkerReportAccessState accessState)
    {
        if (IsCompanyUser)
        {
            HasManagementAccess = true;
            CanCreateGroups = true;
            CanDeleteGroups = true;
            CanAccessReportsPage = false;
            CurrentDepartmentName = "Sirket geneli";
            AccessMessage = string.Empty;
            return;
        }

        var hasDepartmentLeadership = ManagedDepartments.Count > 0;
        var canManageAsLeader = accessState.CanAccessReportsPage || hasDepartmentLeadership;
        CanAccessReportsPage = canManageAsLeader;

        if (!canManageAsLeader)
        {
            ApplyAccessDeniedState();
            return;
        }

        HasManagementAccess = true;
        CanCreateGroups = true;
        CanDeleteGroups = true;
        CurrentDepartmentName = BuildManagedDepartmentSummary();
        AccessMessage = string.Empty;
    }

    private void RefreshTaskEligibleUsers()
    {
        var preservedUserId = SelectedAssignedUser?.UserId;

        EligibleUsers.Clear();
        SelectedAssignedUser = null;

        if (!HasManagementAccess || UserSession.UserId is not Guid currentUserId)
        {
            return;
        }

        var managedDepartmentIds = ManagedDepartments
            .Select(item => item.DepartmentId)
            .Where(departmentId => departmentId != Guid.Empty)
            .ToHashSet();

        var eligibleUsers = allCompanyUsers
            .Where(user => user.Id != Guid.Empty && user.Id != currentUserId)
            .Where(user => IsCompanyUser || user.DepartmentMemberships.Any(membership => managedDepartmentIds.Contains(membership.DepartmentId)))
            .DistinctBy(user => user.Id)
            .OrderBy(user => ResolveDisplayName(user), StringComparer.OrdinalIgnoreCase)
            .Select(user => new AssignableDepartmentUserOption
            {
                UserId = user.Id,
                Name = ResolveDisplayName(user),
                DepartmentName = ResolveDepartmentSummary(user, managedDepartmentIds)
            })
            .ToList();

        foreach (var item in eligibleUsers)
        {
            EligibleUsers.Add(item);
        }

        SelectedAssignedUser = EligibleUsers.FirstOrDefault(item => item.UserId == preservedUserId)
            ?? EligibleUsers.FirstOrDefault();
    }

    private void RefreshGroupEligibleUsers()
    {
        var selectedUserIds = SelectedGroupMembers
            .Select(item => item.UserId)
            .ToHashSet();

        GroupEligibleUsers.Clear();
        AvailableGroupUserOptions.Clear();
        SelectedGroupMembers.Clear();

        if (!CanCreateGroups)
        {
            return;
        }

        IEnumerable<CompanyUserDto> eligibleUsers = allCompanyUsers
            .Where(user => user.Id != Guid.Empty);

        if (!IsCompanyUser)
        {
            if (UserSession.UserId is not Guid currentUserId || SelectedManagedDepartment is null)
            {
                return;
            }

            var departmentId = SelectedManagedDepartment.DepartmentId;
            var departmentName = SelectedManagedDepartment.DepartmentName;

            eligibleUsers = eligibleUsers
                .Where(user => user.Id != currentUserId)
                .Where(user => user.DepartmentMemberships.Any(membership => membership.DepartmentId == departmentId));

            foreach (var user in eligibleUsers
                .DistinctBy(user => user.Id)
                .OrderBy(user => ResolveDisplayName(user), StringComparer.OrdinalIgnoreCase))
            {
                var option = new SelectableGroupUserOption
                {
                    UserId = user.Id,
                    Name = ResolveDisplayName(user),
                    DepartmentName = departmentName,
                    IsSelected = selectedUserIds.Contains(user.Id)
                };

                GroupEligibleUsers.Add(option);

                if (option.IsSelected)
                {
                    SelectedGroupMembers.Add(option);
                }
                else
                {
                    AvailableGroupUserOptions.Add(option);
                }
            }

            return;
        }

        foreach (var user in eligibleUsers
            .DistinctBy(user => user.Id)
            .OrderBy(user => ResolveDisplayName(user), StringComparer.OrdinalIgnoreCase))
        {
            var option = new SelectableGroupUserOption
            {
                UserId = user.Id,
                Name = ResolveDisplayName(user),
                DepartmentName = ResolveDepartmentSummary(user, []),
                IsSelected = selectedUserIds.Contains(user.Id)
            };

            GroupEligibleUsers.Add(option);

            if (option.IsSelected)
            {
                SelectedGroupMembers.Add(option);
            }
            else
            {
                AvailableGroupUserOptions.Add(option);
            }
        }
    }

    private void RefreshGroupMemberSubTasks()
    {
        GroupMemberSubTasks.Clear();

        var selectedUsers = SelectedGroupMembers.ToList();

        foreach (var user in selectedUsers)
        {
            var autoTitle = string.IsNullOrWhiteSpace(GroupTaskTitle)
                ? user.Name
                : $"{GroupTaskTitle.Trim()} - {user.Name}";

            GroupMemberSubTasks.Add(new GroupMemberSubTaskOption
            {
                UserId = user.UserId,
                MemberName = user.Name,
                SubTaskTitle = autoTitle,
                SubTaskDescription = string.Empty
            });
        }
    }

    private void RefreshAvailableGroups(Guid? preferredGroupId = null)
    {
        var selectedGroupId = preferredGroupId ?? SelectedGroupToDelete?.GroupId;

        AvailableGroups.Clear();

        foreach (var group in allCompanyGroups
            .Where(group => group.GroupId != Guid.Empty && !string.IsNullOrWhiteSpace(group.GroupName))
            .OrderBy(group => group.GroupName, StringComparer.OrdinalIgnoreCase))
        {
            var departmentNames = group.DepartmenName
                .Select(name => name?.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var memberNames = group.WorkerName
                .Select(name => name?.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            AvailableGroups.Add(new ManageableGroupOption
            {
                GroupId = group.GroupId,
                GroupName = group.GroupName.Trim(),
                DisplayName = group.GroupName.Trim(),
                DepartmentsSummary = departmentNames.Count == 0
                    ? "Departman bilgisi yok."
                    : string.Join(", ", departmentNames),
                MembersSummary = BuildMembersSummary(memberNames)
            });
        }

        SelectedGroupToDelete = AvailableGroups.FirstOrDefault(item => item.GroupId == selectedGroupId)
            ?? AvailableGroups.FirstOrDefault();

        RefreshGroupDeleteHelperText();
    }

    private async Task ReloadGroupsAsync(Guid companyId, Guid? preferredGroupId = null)
    {
        var groups = await identityApiClient.GetAllCompanyGroupsAsync(companyId) ?? [];
        allCompanyGroups.Clear();
        allCompanyGroups.AddRange(groups);
        RefreshAvailableGroups(preferredGroupId);
    }

    private void EnsurePriorityOptions()
    {
        if (PriorityOptions.Count > 0)
        {
            if (SelectedPriority is null)
            {
                SelectedPriority = PriorityOptions.FirstOrDefault(item => item.Id == 3) ?? PriorityOptions[0];
            }

            return;
        }

        PriorityOptions.Add(new TaskPriorityOption { Id = 1, DisplayName = "Oncelikli Gorev" });
        PriorityOptions.Add(new TaskPriorityOption { Id = 2, DisplayName = "Siradan Gorev" });
        PriorityOptions.Add(new TaskPriorityOption { Id = 3, DisplayName = "Acil Gorev" });
        SelectedPriority = PriorityOptions.FirstOrDefault(item => item.Id == 3) ?? PriorityOptions[0];
    }

    private void RefreshGroupCreateHelperText()
    {
        if (!CanCreateGroups)
        {
            GroupCreateHelperText = string.Empty;
            return;
        }

        GroupCreateHelperText = ShowGroupDepartmentPicker
            ? "Yönettiğiniz departmandan kullanıcılar seçerek grup oluşturabilirsiniz."
            : "Şirket genelinden kullanıcılar seçerek grup oluşturabilirsiniz.";
    }

    private void RefreshGroupDeleteHelperText()
    {
        if (!HasManagementAccess)
        {
            GroupDeleteHelperText = string.Empty;
            return;
        }

        if (!CanDeleteGroups)
        {
            GroupDeleteHelperText = "Grup silme sadece şirket hesabı için açıktır.";
            return;
        }

        GroupDeleteHelperText = AvailableGroups.Count == 0
            ? "Silinecek grup bulunamadı."
            : "Silmek istediğiniz grubu seçin.";
    }

    private void ResetGroupTaskFields()
    {
        GroupTaskTitle = string.Empty;
        GroupTaskDescription = string.Empty;
        GroupTaskDeadlineDate = DateTime.Today.AddDays(1);
        SelectedGroupTaskPriority = null;
        GroupMemberSubTasks.Clear();
    }

    private void ApplyAccessDeniedState()
    {
        HasManagementAccess = false;
        CanAccessReportsPage = false;
        CanCreateGroups = false;
        CanDeleteGroups = false;
        EligibleUsers.Clear();
        GroupEligibleUsers.Clear();
        AvailableGroupUserOptions.Clear();
        SelectedGroupMembers.Clear();
        AvailableGroups.Clear();
        ManagedDepartments.Clear();
        SelectedAssignedUser = null;
        SelectedManagedDepartment = null;
        SelectedGroupToDelete = null;
        CurrentDepartmentName = string.Empty;
        AccessMessage = AccessDeniedMessageText;
        StatusText = string.Empty;
        GroupCreateHelperText = string.Empty;
        RefreshGroupDeleteHelperText();
        OnPropertyChanged(nameof(ShowGroupDepartmentPicker));
    }

    private string BuildReadyStatusMessage()
    {
        return IsCompanyUser
            ? "Sirket genelinde gorev atayabilir ve gruplari yonetebilirsiniz."
            : $"{CurrentDepartmentName} için görev atayabilir ve grup oluşturabilirsiniz.";
    }

    private string BuildManagedDepartmentSummary()
    {
        var departmentNames = ManagedDepartments
            .Select(item => item.DepartmentName?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return departmentNames.Count == 0
            ? "Departman"
            : string.Join(", ", departmentNames);
    }

    private static string ResolveDepartmentSummary(CompanyUserDto user, IReadOnlyCollection<Guid> scopedDepartmentIds)
    {
        var memberships = user.DepartmentMemberships
            .Where(membership => membership.DepartmentId != Guid.Empty)
            .Where(membership => scopedDepartmentIds.Count == 0 || scopedDepartmentIds.Contains(membership.DepartmentId))
            .Select(membership => membership.DepartmentName?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return memberships.Count == 0
            ? "Departman yok"
            : string.Join(", ", memberships);
    }

    private static string BuildMembersSummary(IReadOnlyList<string> memberNames)
    {
        if (memberNames.Count == 0)
        {
            return "Uye bilgisi yok.";
        }

        var preview = memberNames.Count <= 3
            ? string.Join(", ", memberNames)
            : $"{string.Join(", ", memberNames.Take(3))}...";

        return $"{memberNames.Count} uye: {preview}";
    }


    private static string ResolveDisplayName(CompanyUserDto user)
    {
        return string.IsNullOrWhiteSpace(user.Name)
            ? "Kullanıcı"
            : user.Name.Trim();
    }
}

public sealed record AssignableDepartmentUserOption
{
    public Guid UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string DepartmentName { get; init; } = string.Empty;
}

public sealed record TaskPriorityOption
{
    public int Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
}

public sealed record GroupDepartmentOption
{
    public Guid DepartmentId { get; init; }
    public string DepartmentName { get; init; } = string.Empty;
}

public partial class SelectableGroupUserOption : ObservableObject
{
    public Guid UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string DepartmentName { get; init; } = string.Empty;

    [ObservableProperty]
    private bool isSelected;
}

public sealed record ManageableGroupOption
{
    public Guid GroupId { get; init; }
    public string GroupName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string DepartmentsSummary { get; init; } = string.Empty;
    public string MembersSummary { get; init; } = string.Empty;
}

public partial class GroupMemberSubTaskOption : ObservableObject
{
    public Guid UserId { get; init; }
    public string MemberName { get; init; } = string.Empty;

    [ObservableProperty]
    private string subTaskTitle = string.Empty;

    [ObservableProperty]
    private string subTaskDescription = string.Empty;
}
