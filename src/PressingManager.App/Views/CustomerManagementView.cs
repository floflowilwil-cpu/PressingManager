using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using PressingManager.App.Localization;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.App.Views;

/// <summary>
/// Onglet Clients (personne 2) : recherche, création, modification, désactivation / réactivation,
/// et historique des commandes du client sélectionné. Écran construit en C# (aucun fichier XAML).
/// </summary>
public sealed class CustomerManagementView : UserControl
{
    /// <summary>Une ligne de l'historique des commandes (textes déjà formatés pour l'affichage).</summary>
    public sealed record OrderRow(string Number, string Date, string Status, string Total);

    private readonly ICustomerService _customers;
    private readonly IOrderService _orders;
    private readonly ICurrentUserContext _session;

    private readonly Grid _content = new();
    private readonly TextBox _search = Input();
    private readonly CheckBox _showInactive = new() { Content = "Clients désactivés", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0) };
    private readonly DataGrid _customersGrid = CreateGrid();
    private readonly DataGrid _ordersGrid = CreateGrid();

    private readonly TextBlock _formTitle = new() { FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = Solid("#12365D") };
    private readonly TextBox _name = Input(150);
    private readonly TextBox _phone = Input(30);
    private readonly TextBox _address = Input(300);
    private readonly TextBox _notes = Input(1000, multiline: true);
    private readonly Button _saveButton = ActionButton("Créer le client", "#0864D9", "White");
    private readonly Button _newButton = ActionButton("Nouveau", "#E2E8F0", "#12365D");
    private readonly Button _toggleButton = ActionButton("Désactiver", "#E2E8F0", "#9B2C2C");
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0) };

    private CustomerDto? _current;

    public CustomerManagementView(ICustomerService customers, IOrderService orders, ICurrentUserContext session)
    {
        _customers = customers;
        _orders = orders;
        _session = session;

        Content = BuildLayout();
        ClearForm();

        _customersGrid.SelectionChanged += async (_, _) => await OnSelectionChangedAsync();
        _search.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                await RunAsync(() => LoadListAsync(_current?.Id));
            }
        };
        _showInactive.Click += async (_, _) => await RunAsync(() => LoadListAsync(null));
        _saveButton.Click += async (_, _) => await RunAsync(SaveAsync);
        _toggleButton.Click += async (_, _) => await RunAsync(ToggleActiveAsync);
        _newButton.Click += (_, _) =>
        {
            _customersGrid.UnselectAll();
            ClearForm();
            ShowMessage(null, false);
            _name.Focus();
        };
    }

    /// <summary>À appeler à chaque ouverture de l'onglet : relit les droits puis recharge la liste.</summary>
    public async Task RefreshAsync()
    {
        if (!_session.HasPermission(Permission.ManageCustomers))
        {
            _content.IsEnabled = false;
            ShowMessage("Vous n'avez pas le droit de gérer les clients.", true);
            return;
        }

        _content.IsEnabled = true;
        await RunAsync(() => LoadListAsync(_current?.Id));
    }

    // ---------- Actions ----------

    private async Task LoadListAsync(int? keepId)
    {
        var text = _search.Text;
        var list = _showInactive.IsChecked == true
            ? await _customers.SearchInactiveAsync(text)
            : await _customers.SearchAsync(text);

        _customersGrid.ItemsSource = list;

        var keep = keepId is null ? null : list.FirstOrDefault(c => c.Id == keepId);
        _customersGrid.SelectedItem = keep;
        if (keep is null)
        {
            ClearForm();
        }
    }

    private async Task SaveAsync()
    {
        if (_current is null)
        {
            var created = await _customers.CreateAsync(
                new CreateCustomerDto(_name.Text, _phone.Text, _address.Text, _notes.Text));

            // On affiche la liste complète des clients actifs pour retrouver le nouveau client.
            _search.Text = string.Empty;
            _showInactive.IsChecked = false;
            await LoadListAsync(created.Id);
            ShowMessage($"Client {created.CustomerNumber} créé.", false);
        }
        else
        {
            var updated = await _customers.UpdateAsync(
                new UpdateCustomerDto(_current.Id, _name.Text, _phone.Text, _address.Text, _notes.Text));

            await LoadListAsync(updated.Id);
            ShowMessage("Client modifié.", false);
        }
    }

    private async Task ToggleActiveAsync()
    {
        if (_current is null)
        {
            return;
        }

        if (_current.IsActive)
        {
            var answer = MessageBox.Show(
                $"Désactiver le client « {_current.FullName} » ?\nSes commandes sont conservées.",
                "Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            await _customers.DeactivateAsync(_current.Id);
            await LoadListAsync(null);
            ShowMessage("Client désactivé.", false);
        }
        else
        {
            await _customers.ReactivateAsync(_current.Id);
            await LoadListAsync(null);
            ShowMessage("Client réactivé.", false);
        }
    }

    private async Task OnSelectionChangedAsync()
    {
        if (_customersGrid.SelectedItem is not CustomerDto customer)
        {
            ClearForm();
            return;
        }

        _current = customer;
        _name.Text = customer.FullName;
        _phone.Text = customer.Phone;
        _address.Text = customer.Address ?? string.Empty;
        _notes.Text = customer.Notes ?? string.Empty;

        _formTitle.Text = $"Client {customer.CustomerNumber}";
        _saveButton.Content = "Enregistrer les modifications";
        _toggleButton.Content = customer.IsActive ? "Désactiver" : "Réactiver";
        _toggleButton.Visibility = Visibility.Visible;

        await LoadOrdersAsync(customer.Id);
    }

    private async Task LoadOrdersAsync(int customerId)
    {
        if (!_session.HasPermission(Permission.ViewOrders))
        {
            _ordersGrid.ItemsSource = null;
            return;
        }

        try
        {
            var orders = await _orders.GetByCustomerAsync(customerId);

            // Le client sélectionné a pu changer pendant le chargement.
            if (_current?.Id != customerId)
            {
                return;
            }

            _ordersGrid.ItemsSource = orders
                .Select(o => new OrderRow(
                    o.OrderNumber,
                    DateTime.SpecifyKind(o.CreatedAt, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                    Loc.Instance.Status(o.Status),
                    o.Total.ToString("N2")))
                .ToList();
        }
        catch (Exception ex) when (ex is BusinessRuleException or PermissionDeniedException)
        {
            ShowMessage(ex.Message, true);
        }
    }

    private void ClearForm()
    {
        _current = null;
        _name.Text = string.Empty;
        _phone.Text = string.Empty;
        _address.Text = string.Empty;
        _notes.Text = string.Empty;
        _ordersGrid.ItemsSource = null;

        _formTitle.Text = "Nouveau client";
        _saveButton.Content = "Créer le client";
        _toggleButton.Visibility = Visibility.Collapsed;
    }

    private async Task RunAsync(Func<Task> action)
    {
        ShowMessage(null, false);

        try
        {
            await action();
        }
        catch (Exception ex) when (ex is BusinessRuleException or PermissionDeniedException)
        {
            ShowMessage(ex.Message, true);
        }
        catch (Exception ex)
        {
            ShowMessage("Une erreur inattendue est survenue : " + ex.Message, true);
        }
    }

    private void ShowMessage(string? text, bool isError)
    {
        _message.Text = text ?? string.Empty;
        _message.Foreground = Solid(isError ? "#C53030" : "#2F855A");
    }

    // ---------- Construction de l'écran ----------

    private UIElement BuildLayout()
    {
        _content.Margin = new Thickness(32);
        _content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // En-tête
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
        header.Children.Add(new TextBlock { Text = "Clients", FontSize = 28, FontWeight = FontWeights.Bold, Foreground = Solid("#12365D") });
        header.Children.Add(new TextBlock { Text = "Recherchez, créez et modifiez les fiches clients.", FontSize = 14, Foreground = Solid("#718096"), Margin = new Thickness(0, 8, 0, 0) });
        Grid.SetRow(header, 0);
        _content.Children.Add(header);

        // Corps : liste à gauche, formulaire à droite
        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
        Grid.SetRow(body, 1);
        _content.Children.Add(body);

        var left = BuildLeftPanel();
        Grid.SetColumn(left, 0);
        body.Children.Add(left);

        var right = BuildFormPanel();
        Grid.SetColumn(right, 2);
        body.Children.Add(right);

        return _content;
    }

    private UIElement BuildLeftPanel()
    {
        var left = new Grid();
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });

        // Barre de recherche
        var searchButton = ActionButton("Rechercher", "#0864D9", "White");
        searchButton.Click += async (_, _) => await RunAsync(() => LoadListAsync(_current?.Id));

        var searchBar = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(searchButton, Dock.Right);
        DockPanel.SetDock(_showInactive, Dock.Right);
        searchBar.Children.Add(searchButton);
        searchBar.Children.Add(_showInactive);
        _search.ToolTip = "Nom, téléphone ou numéro client (Entrée pour rechercher)";
        searchBar.Children.Add(_search);
        Grid.SetRow(searchBar, 0);
        left.Children.Add(searchBar);

        // Liste des clients
        _customersGrid.Columns.Add(Column("Numéro", "CustomerNumber", 110));
        _customersGrid.Columns.Add(Column("Nom", "FullName", 0));
        _customersGrid.Columns.Add(Column("Téléphone", "Phone", 150));
        Grid.SetRow(_customersGrid, 1);
        left.Children.Add(_customersGrid);

        // Historique des commandes du client sélectionné
        var historyTitle = new TextBlock
        {
            Text = "Commandes du client sélectionné",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = Solid("#12365D"),
            Margin = new Thickness(0, 16, 0, 8)
        };
        Grid.SetRow(historyTitle, 2);
        left.Children.Add(historyTitle);

        _ordersGrid.Columns.Add(Column("N° commande", "Number", 170));
        _ordersGrid.Columns.Add(Column("Date", "Date", 140));
        _ordersGrid.Columns.Add(Column("Statut", "Status", 0));
        _ordersGrid.Columns.Add(Column("Total", "Total", 110));
        Grid.SetRow(_ordersGrid, 3);
        left.Children.Add(_ordersGrid);

        return left;
    }

    private UIElement BuildFormPanel()
    {
        var form = new StackPanel();
        form.Children.Add(_formTitle);

        form.Children.Add(Caption("Nom complet *"));
        form.Children.Add(_name);
        form.Children.Add(Caption("Téléphone *"));
        form.Children.Add(_phone);
        form.Children.Add(Caption("Adresse"));
        form.Children.Add(_address);
        form.Children.Add(Caption("Notes"));
        form.Children.Add(_notes);

        var buttons = new WrapPanel { Margin = new Thickness(0, 18, 0, 0) };
        buttons.Children.Add(_saveButton);
        buttons.Children.Add(_newButton);
        buttons.Children.Add(_toggleButton);
        form.Children.Add(buttons);

        form.Children.Add(_message);

        return new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(20),
            VerticalAlignment = VerticalAlignment.Top,
            Child = form
        };
    }

    // ---------- Petits utilitaires d'affichage ----------

    private static SolidColorBrush Solid(string hex) =>
        (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        Foreground = Solid("#718096"),
        FontSize = 12,
        Margin = new Thickness(0, 14, 0, 4)
    };

    private static TextBox Input(int maxLength = 0, bool multiline = false)
    {
        var box = new TextBox
        {
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 14,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        if (maxLength > 0)
        {
            box.MaxLength = maxLength;
        }

        if (multiline)
        {
            box.AcceptsReturn = true;
            box.TextWrapping = TextWrapping.Wrap;
            box.Height = 80;
            box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            box.VerticalContentAlignment = VerticalAlignment.Top;
        }

        return box;
    }

    private static Button ActionButton(string text, string background, string foreground) => new()
    {
        Content = text,
        Padding = new Thickness(16, 9, 16, 9),
        Margin = new Thickness(0, 0, 8, 8),
        Background = Solid(background),
        Foreground = Solid(foreground),
        BorderThickness = new Thickness(0),
        FontWeight = FontWeights.SemiBold,
        Cursor = Cursors.Hand
    };

    private static DataGrid CreateGrid() => new()
    {
        AutoGenerateColumns = false,
        IsReadOnly = true,
        CanUserAddRows = false,
        CanUserDeleteRows = false,
        SelectionMode = DataGridSelectionMode.Single,
        HeadersVisibility = DataGridHeadersVisibility.Column,
        GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
        HorizontalGridLinesBrush = Solid("#E2E8F0"),
        Background = Brushes.White,
        RowBackground = Brushes.White,
        BorderThickness = new Thickness(0),
        FontSize = 13
    };

    private static DataGridTextColumn Column(string header, string path, double width) => new()
    {
        Header = header,
        Binding = new Binding(path),
        Width = width > 0
            ? new DataGridLength(width)
            : new DataGridLength(1, DataGridLengthUnitType.Star)
    };
}