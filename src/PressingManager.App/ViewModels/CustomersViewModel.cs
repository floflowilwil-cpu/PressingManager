using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.App.ViewModels;

/// <summary>Onglet « Clients » : liste, recherche, creation, modification, desactivation.</summary>
public partial class CustomersViewModel : ObservableObject
{
    private const int PageSize = 10;

    private readonly ICustomerService _customers;
    private readonly IOrderService _orders;
    private readonly ICurrentUserContext _session;

    private List<CustomerDto> _all = new();
    private CancellationTokenSource? _searchCts;

    public CustomersViewModel(ICustomerService customers, IOrderService orders, ICurrentUserContext session)
    {
        _customers = customers ?? throw new ArgumentNullException(nameof(customers));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public ObservableCollection<CustomerDto> PageItems { get; } = new();

    /// <summary>Confort visuel : le vrai controle est fait dans CustomerService.</summary>
    public bool CanManage => _session.HasPermission(Permission.ManageCustomers);

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageInfo), nameof(HasPrevious), nameof(HasNext))]
    private int _currentPage = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageInfo), nameof(HasNext))]
    private int _totalPages = 1;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isError;

    [ObservableProperty]
    private bool _isEmpty;

    public string PageInfo => $"Page {CurrentPage} / {TotalPages}";

    public bool HasPrevious => CurrentPage > 1;

    public bool HasNext => CurrentPage < TotalPages;

    partial void OnSearchTextChanged(string value) => _ = DebouncedSearchAsync();

    [RelayCommand]
    private Task LoadAsync() => RefreshAsync(resetPage: true);

    [RelayCommand]
    private void PreviousPage()
    {
        if (HasPrevious)
        {
            CurrentPage--;
            ShowPage();
        }
    }

    [RelayCommand]
    private void NextPage()
    {
        if (HasNext)
        {
            CurrentPage++;
            ShowPage();
        }
    }

    /// <summary>Renvoie null si tout va bien, sinon le message d'erreur a afficher dans la fenetre.</summary>
    public async Task<string?> CreateAsync(string fullName, string phone, string? address, string? notes)
    {
        try
        {
            var created = await _customers.CreateAsync(new CreateCustomerDto(fullName, phone, address, notes));
            await RefreshAsync(resetPage: true);
            SetMessage($"Client {created.CustomerNumber} créé.", isError: false);
            return null;
        }
        catch (Exception ex)
        {
            return ToUserMessage(ex);
        }
    }

    public async Task<string?> UpdateAsync(int id, string fullName, string phone, string? address, string? notes)
    {
        try
        {
            await _customers.UpdateAsync(new UpdateCustomerDto(id, fullName, phone, address, notes));
            await RefreshAsync(resetPage: false);
            SetMessage("Client modifié.", isError: false);
            return null;
        }
        catch (Exception ex)
        {
            return ToUserMessage(ex);
        }
    }

    public async Task DeactivateAsync(CustomerDto customer)
    {
        try
        {
            await _customers.DeactivateAsync(customer.Id);
            await RefreshAsync(resetPage: false);
            SetMessage($"Client {customer.FullName} désactivé (son historique est conservé).", isError: false);
        }
        catch (Exception ex)
        {
            SetMessage(ToUserMessage(ex), isError: true);
        }
    }

    /// <summary>Fiche + historique des commandes d'un client (l'historique est vide sans droit de lecture).</summary>
    public async Task<CustomerDetailsModel> GetDetailsAsync(CustomerDto customer)
    {
        IReadOnlyList<OrderRow> rows = Array.Empty<OrderRow>();

        try
        {
            var orders = await _orders.GetByCustomerAsync(customer.Id);
            rows = orders.Select(o => new OrderRow(o)).ToList();
        }
        catch (PermissionDeniedException)
        {
            // Pas le droit de voir les commandes : on affiche juste la fiche.
        }
        catch (Exception ex)
        {
            SetMessage(ToUserMessage(ex), isError: true);
        }

        return new CustomerDetailsModel(customer, rows);
    }

    private async Task DebouncedSearchAsync()
    {
        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;

        try
        {
            await Task.Delay(300, cts.Token);
            await RefreshAsync(resetPage: true);
        }
        catch (OperationCanceledException)
        {
            // Une frappe plus recente a pris le relais.
        }
    }

    private async Task RefreshAsync(bool resetPage)
    {
        try
        {
            _all = (await _customers.SearchAsync(SearchText)).ToList();

            if (resetPage)
            {
                CurrentPage = 1;
            }

            ShowPage();
            if (Message is not null && IsError)
            {
                Message = null;
            }
        }
        catch (Exception ex)
        {
            _all = new List<CustomerDto>();
            ShowPage();
            SetMessage(ToUserMessage(ex), isError: true);
        }
    }

    private void ShowPage()
    {
        TotalPages = Math.Max(1, (int)Math.Ceiling(_all.Count / (double)PageSize));
        if (CurrentPage > TotalPages)
        {
            CurrentPage = TotalPages;
        }

        PageItems.Clear();
        foreach (var customer in _all.Skip((CurrentPage - 1) * PageSize).Take(PageSize))
        {
            PageItems.Add(customer);
        }

        IsEmpty = _all.Count == 0;
    }

    private void SetMessage(string? text, bool isError)
    {
        IsError = isError;
        Message = text;
    }

    internal static string ToUserMessage(Exception ex) => ex switch
    {
        BusinessRuleException or PermissionDeniedException => ex.Message,
        _ => $"Une erreur inattendue est survenue : {ex.Message}"
    };
}
