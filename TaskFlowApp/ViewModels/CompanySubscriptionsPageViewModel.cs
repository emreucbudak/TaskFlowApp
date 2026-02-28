using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Payments;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Tenant;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Services.Realtime;

namespace TaskFlowApp.ViewModels;

public partial class CompanySubscriptionsPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    TenantApiClient tenantApiClient)
    : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
{
    private const string StartupStripePaymentLink = "https://buy.stripe.com/test_3cIcN61Gn6ip73CdPP2wU07";
    private const string BusinessStripePaymentLink = "https://buy.stripe.com/test_bJe5kEcl19uBew42772wU03";
    private const string EnterpriseStripePaymentLink = "https://buy.stripe.com/test_bJebJ2bgX7mt9bKaDD2wU08";

    private const int MaxPaymentPollingAttemptCount = 30;
    private static readonly TimeSpan PaymentPollingInterval = TimeSpan.FromSeconds(6);

    private PlanSelectionRow? _pendingPlanSelection;
    private string? _pendingStripeSessionId;
    private CancellationTokenSource? _paymentPollingCancellationTokenSource;

    public ObservableCollection<PlanUsageRow> PlanUsageRows { get; } = [];
    public ObservableCollection<PlanSelectionRow> PlanSelectionRows { get; } = [];

    [ObservableProperty]
    private string activePlanName = "Bilinmiyor";

    [ObservableProperty]
    private string activePlanPriceText = "-";

    [ObservableProperty]
    private string activePlanStartDateText = "-";

    [ObservableProperty]
    private bool isSubscriptionCancelled;

    [ObservableProperty]
    private string subscriptionActionTitle = "Abonelik İptali";

    [ObservableProperty]
    private string subscriptionActionButtonText = "Aboneliği İptal Et";

    [ObservableProperty]
    private string subscriptionActionButtonColor = "#B91C1C";

    [ObservableProperty]
    private bool isAwaitingPaymentConfirmation;

    [ObservableProperty]
    private string pendingPaymentPlanName = string.Empty;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (UserSession.CompanyId is null)
        {
            ErrorMessage = "Şirket bilgisi bulunamadı. Tekrar giriş yapın.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            StatusText = string.Empty;

            var companyId = UserSession.CompanyId.Value;
            var paymentConfirmationMessage = await TryConfirmPaymentFromDeepLinkAsync(companyId, CancellationToken.None);
            var plansTask = tenantApiClient.GetCompanyPlansAsync();
            var snapshotTask = tenantApiClient.GetCompanySubscriptionSnapshotAsync(companyId);

            await Task.WhenAll(plansTask, snapshotTask);

            var plans = (await plansTask ?? [])
                .Where(plan => !string.IsNullOrWhiteSpace(plan.PlanName))
                .ToList();
            var snapshot = await snapshotTask;
            var orderedPlans = OrderPlans(plans);

            var usedUserCount = Math.Max(0, snapshot.CurrentUserCount);
            var usedTeamCount = Math.Max(0, snapshot.CurrentGroupCount);
            var usedIndividualTaskCount = Math.Max(0, snapshot.CurrentIndividualTaskCount);

            var activePlan = ResolveActivePlanFromSnapshot(snapshot, orderedPlans, usedUserCount, usedTeamCount, usedIndividualTaskCount);
            if (orderedPlans.All(plan =>
                    !string.Equals(
                        NormalizePlanName(plan.PlanName),
                        NormalizePlanName(activePlan.PlanName),
                        StringComparison.OrdinalIgnoreCase)))
            {
                orderedPlans.Insert(0, activePlan);
            }

            BuildPlanSummary(activePlan, orderedPlans, usedUserCount, usedTeamCount, usedIndividualTaskCount, snapshot.StartDateUtc);
            if (!string.IsNullOrWhiteSpace(paymentConfirmationMessage))
            {
                StatusText = paymentConfirmationMessage;
                return;
            }

            if (MaybeCompletePendingPaymentFromActivePlan())
            {
                StatusText = $"{ActivePlanName} planı aktif edildi.";
                return;
            }

            StatusText = "Abonelik plan ve kullanım bilgileri güncellendi.";
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
    private Task CancelSubscriptionAsync()
    {
        ErrorMessage = string.Empty;

        if (!IsSubscriptionCancelled)
        {
            IsSubscriptionCancelled = true;
            StatusText = "Abonelik iptal talebiniz alındı. İşlem için yönetici onayı gereklidir.";
        }
        else
        {
            IsSubscriptionCancelled = false;
            StatusText = "Aboneliğiniz devam edecek şekilde güncellendi.";
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SelectPlanAsync(PlanSelectionRow? selectedPlan)
    {
        if (selectedPlan is null || IsBusy)
        {
            return;
        }

        ErrorMessage = string.Empty;

        if (!selectedPlan.IsSelectable)
        {
            StatusText = $"{selectedPlan.PlanName} zaten mevcut planınız.";
            return;
        }

        if (UserSession.CompanyId is null)
        {
            ErrorMessage = "Şirket bilgisi bulunamadı. Tekrar giriş yapın.";
            return;
        }

        ResetPendingPaymentState();

        try
        {
            IsBusy = true;

            var paymentLink = ResolveStripePaymentLink(selectedPlan.PlanSlug, selectedPlan.RawPlanName);
            if (string.IsNullOrWhiteSpace(paymentLink))
            {
                ErrorMessage = $"{selectedPlan.PlanName} için Stripe ödeme bağlantısı tanımlı değil.";
                return;
            }

            var paymentUrl = BuildStripePaymentUrl(paymentLink, UserSession.CompanyId.Value, selectedPlan.PlanSlug);
            await Launcher.Default.OpenAsync(new Uri(paymentUrl));

            _pendingPlanSelection = selectedPlan;
            _pendingStripeSessionId = null;
            PendingPaymentPlanName = selectedPlan.PlanName;
            IsAwaitingPaymentConfirmation = true;
            StatusText = $"{selectedPlan.PlanName} için ödeme ekranına yönlendirildiniz. Ödeme tamamlanınca plan otomatik güncellenecek.";
            StartPaymentPolling(selectedPlan);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Stripe ödeme bağlantısı oluşturulurken bir sorun oluştu.");
        }
        catch (HttpRequestException)
        {
            ErrorMessage = GenericConnectionErrorMessage;
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = GenericConnectionErrorMessage;
        }
        catch
        {
            ErrorMessage = "Ödeme bağlantısı açılamadı. Lütfen tekrar deneyin.";
            ResetPendingPaymentState();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<string?> TryConfirmPaymentFromDeepLinkAsync(Guid currentCompanyId, CancellationToken cancellationToken)
    {
        var pendingPayment = PaymentReturnState.TryGetPending();
        if (pendingPayment is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(pendingPayment.SessionId))
        {
            PaymentReturnState.ClearPending();
            ErrorMessage = "Odeme dogrulamasi icin session bilgisi bulunamadi.";
            return null;
        }

        var resolvedCompanyId = pendingPayment.CompanyId ?? currentCompanyId;
        if (resolvedCompanyId != currentCompanyId)
        {
            PaymentReturnState.ClearPending();
            ErrorMessage = "Odeme dogrulama baglantisi farkli bir sirket icin olusturulmus.";
            return null;
        }

        try
        {
            var response = await tenantApiClient.ConfirmStripePaymentAndActivateRequestAsync(
                new ConfirmStripePaymentAndActivateRequestDto
                {
                    CompanyId = resolvedCompanyId,
                    PlanSlug = pendingPayment.PlanSlug,
                    PlanName = pendingPayment.PlanName,
                    SessionId = pendingPayment.SessionId
                },
                cancellationToken);

            PaymentReturnState.ClearPending();
            ResetPendingPaymentState();

            var normalizedPlanName = NormalizePlanName(response.PlanName);
            return string.IsNullOrWhiteSpace(response.Message)
                ? $"{normalizedPlanName} plani odeme sonrasi aktif edildi."
                : response.Message;
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Odeme dogrulamasi tamamlanamadi. Lutfen tekrar deneyin.");
            return null;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = GenericConnectionErrorMessage;
            return null;
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = GenericConnectionErrorMessage;
            return null;
        }
    }

    private bool MaybeCompletePendingPaymentFromActivePlan()
    {
        if (!IsAwaitingPaymentConfirmation || _pendingPlanSelection is null)
        {
            return false;
        }

        var activePlanMatches = string.Equals(
            NormalizePlanName(ActivePlanName),
            NormalizePlanName(_pendingPlanSelection.PlanName),
            StringComparison.OrdinalIgnoreCase);

        if (!activePlanMatches)
        {
            return false;
        }

        ResetPendingPaymentState();
        return true;
    }

    private void StartPaymentPolling(PlanSelectionRow selectedPlan)
    {
        _paymentPollingCancellationTokenSource?.Cancel();
        _paymentPollingCancellationTokenSource?.Dispose();
        _paymentPollingCancellationTokenSource = new CancellationTokenSource();

        var cancellationToken = _paymentPollingCancellationTokenSource.Token;
        _ = MonitorPendingPaymentAsync(selectedPlan, cancellationToken);
    }

    private async Task MonitorPendingPaymentAsync(PlanSelectionRow selectedPlan, CancellationToken cancellationToken)
    {
        if (UserSession.CompanyId is null)
        {
            return;
        }

        for (var attempt = 0; attempt < MaxPaymentPollingAttemptCount; attempt++)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var snapshot = await tenantApiClient.GetCompanySubscriptionSnapshotAsync(UserSession.CompanyId.Value, cancellationToken);
                var activePlanMatches = string.Equals(
                    NormalizePlanName(snapshot.PlanName),
                    NormalizePlanName(selectedPlan.PlanName),
                    StringComparison.OrdinalIgnoreCase);

                if (snapshot.HasActiveSubscription && activePlanMatches)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        ResetPendingPaymentState();
                        await LoadAsync();
                        if (string.IsNullOrWhiteSpace(ErrorMessage))
                        {
                            StatusText = $"{selectedPlan.PlanName} planı ödeme sonrası otomatik aktif edildi.";
                        }
                    });

                    return;
                }
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Geçici timeout hatalarında polling devam eder.
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ApiException)
            {
                // Geçici API hatalarında polling devam eder.
            }
            catch (HttpRequestException)
            {
                // Geçici bağlantı hatalarında polling devam eder.
            }
            catch
            {
                // Beklenmeyen hatalarda da polling akışını bozma.
            }

            try
            {
                await Task.Delay(PaymentPollingInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (!IsAwaitingPaymentConfirmation)
            {
                return;
            }

            StatusText = string.IsNullOrWhiteSpace(_pendingStripeSessionId)
                ? "Ödeme doğrulaması bekleniyor. Sayfayı yenilediğinizde plan bilgisi otomatik güncellenecektir."
                : "Ödeme doğrulaması bekleniyor. Stripe session sonucu işlenince plan bilgisi otomatik güncellenecektir.";
        });
    }

    private void ResetPendingPaymentState()
    {
        _paymentPollingCancellationTokenSource?.Cancel();
        _paymentPollingCancellationTokenSource?.Dispose();
        _paymentPollingCancellationTokenSource = null;

        IsAwaitingPaymentConfirmation = false;
        PendingPaymentPlanName = string.Empty;
        _pendingPlanSelection = null;
        _pendingStripeSessionId = null;
    }

    private void BuildPlanSummary(
        CompanyPlanDto activePlan,
        IReadOnlyCollection<CompanyPlanDto> orderedPlans,
        int usedUserCount,
        int usedTeamCount,
        int usedIndividualTaskCount,
        DateTime? activeSubscriptionStartDateUtc)
    {
        ActivePlanName = NormalizePlanName(activePlan.PlanName);
        ActivePlanPriceText = activePlan.PlanPrice <= 0
            ? "Ücretsiz"
            : string.Format(CultureInfo.GetCultureInfo("tr-TR"), "{0:N0} TL / ay", activePlan.PlanPrice);
        ActivePlanStartDateText = FormatDateTime(activeSubscriptionStartDateUtc);

        PlanUsageRows.Clear();
        PlanUsageRows.Add(new PlanUsageRow(
            "Kullanıcı Limiti",
            activePlan.PlanProperties.PeopleAddedLimit.ToString(CultureInfo.InvariantCulture),
            usedUserCount.ToString(CultureInfo.InvariantCulture),
            FormatRemaining(activePlan.PlanProperties.PeopleAddedLimit, usedUserCount)));
        PlanUsageRows.Add(new PlanUsageRow(
            "Takım Limiti",
            activePlan.PlanProperties.TeamLimit.ToString(CultureInfo.InvariantCulture),
            usedTeamCount.ToString(CultureInfo.InvariantCulture),
            FormatRemaining(activePlan.PlanProperties.TeamLimit, usedTeamCount)));
        PlanUsageRows.Add(new PlanUsageRow(
            "Bireysel Görev Limiti",
            activePlan.PlanProperties.IndividualTaskLimit.ToString(CultureInfo.InvariantCulture),
            usedIndividualTaskCount.ToString(CultureInfo.InvariantCulture),
            FormatRemaining(activePlan.PlanProperties.IndividualTaskLimit, usedIndividualTaskCount)));

        var internalReportingMark = activePlan.PlanProperties.IsInternalReportingEnabled ? "✓" : "-";
        PlanUsageRows.Add(new PlanUsageRow(
            "İç Raporlama",
            internalReportingMark,
            internalReportingMark,
            internalReportingMark));

        PlanSelectionRows.Clear();
        foreach (var plan in orderedPlans)
        {
            var isCurrentPlan = IsSamePlan(plan, activePlan);
            var isSelectable = !isCurrentPlan;
            var priceText = plan.PlanPrice <= 0
                ? "Ücretsiz"
                : string.Format(CultureInfo.GetCultureInfo("tr-TR"), "{0:N0} TL", plan.PlanPrice);
            var planSlug = GetPlanSlug(plan.PlanName);

            PlanSelectionRows.Add(new PlanSelectionRow(
                NormalizePlanName(plan.PlanName),
                NormalizePlanName(plan.PlanName),
                planSlug,
                priceText,
                plan.PlanProperties.PeopleAddedLimit.ToString(CultureInfo.InvariantCulture),
                plan.PlanProperties.TeamLimit.ToString(CultureInfo.InvariantCulture),
                plan.PlanProperties.IndividualTaskLimit.ToString(CultureInfo.InvariantCulture),
                plan.PlanProperties.IsInternalReportingEnabled ? "✓" : "-",
                isSelectable ? "Plan Seç" : "Mevcut Plan",
                isSelectable,
                isSelectable ? "#2563EB" : "#334155"));
        }
    }

    private static CompanyPlanDto ResolveActivePlanFromSnapshot(
        CompanySubscriptionSnapshotDto snapshot,
        List<CompanyPlanDto> orderedPlans,
        int usedUserCount,
        int usedTeamCount,
        int usedIndividualTaskCount)
    {
        if (snapshot.HasActiveSubscription && !string.IsNullOrWhiteSpace(snapshot.PlanName))
        {
            var snapshotPlan = orderedPlans.FirstOrDefault(plan =>
                string.Equals(
                    NormalizePlanName(plan.PlanName),
                    NormalizePlanName(snapshot.PlanName),
                    StringComparison.OrdinalIgnoreCase));

            if (snapshotPlan is not null)
            {
                return snapshotPlan;
            }

            return new CompanyPlanDto
            {
                PlanName = snapshot.PlanName,
                PlanPrice = snapshot.PlanPrice,
                PlanProperties = new PlanPropertiesDto
                {
                    PeopleAddedLimit = snapshot.UserLimit,
                    TeamLimit = snapshot.TeamLimit,
                    IndividualTaskLimit = snapshot.IndividualTaskLimit,
                    IsInternalReportingEnabled = snapshot.IsInternalReportingEnabled
                }
            };
        }

        return ResolveActivePlan(orderedPlans, usedUserCount, usedTeamCount, usedIndividualTaskCount);
    }

    private static List<CompanyPlanDto> OrderPlans(IEnumerable<CompanyPlanDto> plans)
    {
        return plans
            .OrderBy(plan => plan.PlanPrice)
            .ThenBy(plan => plan.PlanProperties.PeopleAddedLimit)
            .ThenBy(plan => plan.PlanProperties.TeamLimit)
            .ThenBy(plan => plan.PlanProperties.IndividualTaskLimit)
            .ThenBy(plan => plan.PlanName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static CompanyPlanDto ResolveActivePlan(
        IEnumerable<CompanyPlanDto> plans,
        int usedUserCount,
        int usedTeamCount,
        int usedIndividualTaskCount)
    {
        var orderedPlans = plans
            .OrderBy(plan => plan.PlanPrice)
            .ThenBy(plan => plan.PlanProperties.PeopleAddedLimit)
            .ThenBy(plan => plan.PlanProperties.TeamLimit)
            .ThenBy(plan => plan.PlanProperties.IndividualTaskLimit)
            .ToList();

        if (orderedPlans.Count == 0)
        {
            return new CompanyPlanDto();
        }

        foreach (var plan in orderedPlans)
        {
            if (usedUserCount <= plan.PlanProperties.PeopleAddedLimit
                && usedTeamCount <= plan.PlanProperties.TeamLimit
                && usedIndividualTaskCount <= plan.PlanProperties.IndividualTaskLimit)
            {
                return plan;
            }
        }

        return orderedPlans[^1];
    }

    private static string NormalizePlanName(string? planName)
    {
        return string.IsNullOrWhiteSpace(planName) ? "Bilinmiyor" : planName.Trim();
    }

    private static bool IsSamePlan(CompanyPlanDto left, CompanyPlanDto right)
    {
        return string.Equals(NormalizePlanName(left.PlanName), NormalizePlanName(right.PlanName), StringComparison.OrdinalIgnoreCase)
            && left.PlanPrice == right.PlanPrice
            && left.PlanProperties.PeopleAddedLimit == right.PlanProperties.PeopleAddedLimit
            && left.PlanProperties.TeamLimit == right.PlanProperties.TeamLimit
            && left.PlanProperties.IndividualTaskLimit == right.PlanProperties.IndividualTaskLimit
            && left.PlanProperties.IsInternalReportingEnabled == right.PlanProperties.IsInternalReportingEnabled;
    }

    private static string FormatRemaining(int limit, int usage)
    {
        var remaining = limit - usage;
        return remaining >= 0
            ? remaining.ToString(CultureInfo.InvariantCulture)
            : $"-{Math.Abs(remaining).ToString(CultureInfo.InvariantCulture)}";
    }

    private static string GetPlanSlug(string? planName)
    {
        var normalized = NormalizePlanKey(planName);

        if (normalized.Contains("startup") || normalized.Contains("baslangic"))
        {
            return "startup";
        }

        if (normalized.Contains("business") || normalized.Contains("profesyonel"))
        {
            return "business";
        }

        if (normalized.Contains("enterprise") || normalized.Contains("kurumsal"))
        {
            return "enterprise";
        }

        return normalized;
    }

    private static string? ResolveStripePaymentLink(string? planSlug, string? planName)
    {
        var normalized = NormalizePlanKey(string.IsNullOrWhiteSpace(planSlug) ? planName : planSlug);

        if (normalized.Contains("startup") || normalized.Contains("baslangic"))
        {
            return StartupStripePaymentLink;
        }

        if (normalized.Contains("business") || normalized.Contains("profesyonel"))
        {
            return BusinessStripePaymentLink;
        }

        if (normalized.Contains("enterprise") || normalized.Contains("kurumsal"))
        {
            return EnterpriseStripePaymentLink;
        }

        return null;
    }

    private static string BuildStripePaymentUrl(string basePaymentUrl, Guid companyId, string? planSlug)
    {
        var normalizedPlanSlug = NormalizePlanKey(planSlug);
        var clientReferenceId = string.IsNullOrWhiteSpace(normalizedPlanSlug)
            ? companyId.ToString("D")
            : $"{companyId:D}__{normalizedPlanSlug}";

        var paymentUrl = AppendQueryParameter(basePaymentUrl, "client_reference_id", clientReferenceId);
        paymentUrl = AppendQueryParameter(paymentUrl, "utm_source", "taskflowapp");
        paymentUrl = AppendQueryParameter(paymentUrl, "utm_medium", "desktop");
        paymentUrl = AppendQueryParameter(paymentUrl, "utm_content", companyId.ToString("D"));
        return paymentUrl;
    }

    private static string AppendQueryParameter(string url, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(url)
            || string.IsNullOrWhiteSpace(key)
            || string.IsNullOrWhiteSpace(value))
        {
            return url;
        }

        var hashIndex = url.IndexOf('#');
        var fragment = hashIndex >= 0 ? url[hashIndex..] : string.Empty;
        var mainUrl = hashIndex >= 0 ? url[..hashIndex] : url;
        var separator = mainUrl.Contains('?') ? "&" : "?";
        return $"{mainUrl}{separator}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}{fragment}";
    }

    private static string NormalizePlanKey(string? value)
    {
        return (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace("ş", "s")
            .Replace("ı", "i")
            .Replace("ğ", "g")
            .Replace("ü", "u")
            .Replace("ö", "o")
            .Replace("ç", "c")
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty);
    }

    private static string FormatDateTime(DateTime? dateTimeUtc)
    {
        if (!dateTimeUtc.HasValue)
        {
            return "-";
        }

        return dateTimeUtc.Value
            .ToLocalTime()
            .ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("tr-TR"));
    }

    partial void OnIsSubscriptionCancelledChanged(bool value)
    {
        SubscriptionActionTitle = value ? "Aboneliği Devam Ettir" : "Abonelik İptali";
        SubscriptionActionButtonText = value ? "Aboneliğinizi devam ettirin" : "Aboneliği İptal Et";
        SubscriptionActionButtonColor = value ? "#16A34A" : "#B91C1C";
    }
}

public sealed record PlanUsageRow(string Feature, string Limit, string Usage, string Remaining);
public sealed record PlanSelectionRow(
    string PlanName,
    string RawPlanName,
    string PlanSlug,
    string Price,
    string UserLimit,
    string TeamLimit,
    string IndividualTaskLimit,
    string InternalReporting,
    string ActionButtonText,
    bool IsSelectable,
    string ActionButtonColor);
