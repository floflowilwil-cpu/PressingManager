using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.App.ViewModels;

/// <summary>Onglet « Commandes » : liste, recherche et acces a la creation d'une commande.</summary>
public partial class OrdersViewModel : ObservableObject
{
    private const int PageSize = 10;

    private readonly IOrderService _orders;
    private readonly ICurrentUserContext _session;

    private List<OrderRow> _all = new();
    private CancellationTokenSource? _searchCts;

    public OrdersViewModel(IOrderService orders, ICurrentUserContext session)
    {
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public ObservableCollection<OrderRow> PageItems { get; } = new();

    public bool CanCreate => _session.HasPermission(Permission.ManageOrders);

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

    /// <summary>Appele par la vue une fois la commande creee dans la fenetre « Nouvelle commande ».</summary>
    public async Task OnOrderCreatedAsync(OrderDetailsDto order)
    {
        await RefreshAsync(resetPage: true);
        SetMessage($"Commande {order.OrderNumber} créée pour {order.CustomerName} — total {UiFormat.Money(order.Total)}.", isError: false);
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
            var orders = await _orders.SearchAsync(SearchText);
            _all = orders.Select(o => new OrderRow(o)).ToList();

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
            _all = new List<OrderRow>();
            ShowPage();
            SetMessage(CustomersViewModel.ToUserMessage(ex), isError: true);
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
        foreach (var row in _all.Skip((CurrentPage - 1) * PageSize).Take(PageSize))
        {
            PageItems.Add(row);
        }

        IsEmpty = _all.Count == 0;
    }

    private void SetMessage(string? text, bool isError)
    {
        IsError = isError;
        Message = text;
    }
}
