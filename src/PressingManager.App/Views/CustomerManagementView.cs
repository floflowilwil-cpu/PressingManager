using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Shapes = System.Windows.Shapes;
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
    private readonly CheckBox _showInactive = new() { Content = "Clients désactivés", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 14, 0) };
    private readonly DataGrid _customersGrid = CreateGrid();
    private readonly DataGrid _ordersGrid = CreateGrid();

    private readonly TextBlock _formTitle = new() { FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Solid("#12365D"), Margin = new Thickness(0, 0, 0, 2) };
    private readonly TextBox _name = Input(150);
    private readonly TextBox _phone = Input(30);
    private readonly TextBox _email = Input(200);
    private readonly TextBox _address = Input(300);
    private readonly TextBox _notes = Input(1000, multiline: true);
    private readonly Button _saveButton = ActionButton("Créer le client", "BtnPrimary");
    private readonly Button _newButton = ActionButton("Nouveau", "BtnLight");
    private readonly Button _toggleButton = ActionButton("Désactiver", "BtnDanger");
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
                new CreateCustomerDto(_name.Text, _phone.Text, _address.Text, _notes.Text, _email.Text));

            // On affiche la liste complète des clients actifs pour retrouver le nouveau client.
            _search.Text = string.Empty;
            _showInactive.IsChecked = false;
            await LoadListAsync(created.Id);
            ShowMessage($"Client {created.CustomerNumber} créé.", false);
        }
        else
        {
            var updated = await _customers.UpdateAsync(
                new UpdateCustomerDto(_current.Id, _name.Text, _phone.Text, _address.Text, _notes.Text, _email.Text));

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
        _email.Text = customer.Email ?? string.Empty;
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
        _email.Text = string.Empty;
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

    // ---------- Construction de l'écran (même modèle que Statistiques) ----------

    private UIElement BuildLayout()
    {
        _content.Margin = new Thickness(32);
        _content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // En-tête : pastille marine + pictogramme turquoise, comme l'onglet Statistiques
        var header = BuildHeader("Clients", "Recherchez, créez et modifiez les fiches clients.", "IconClients");
        Grid.SetRow(header, 0);
        _content.Children.Add(header);

        // Corps : liste à gauche, formulaire à droite
        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
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

    private static UIElement BuildHeader(string title, string subtitle, string iconKey)
    {
        var icon = new Shapes.Path
        {
            Data = (Geometry)Application.Current.FindResource(iconKey),
            Stroke = Solid("#14B8A6"),
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        var canvas = new Canvas { Width = 24, Height = 24 };
        canvas.Children.Add(icon);

        var badge = new Border
        {
            Width = 38,
            Height = 38,
            CornerRadius = new CornerRadius(10),
            Background = Solid("#0F2A4A"),
            Margin = new Thickness(0, 0, 12, 0),
            Child = new Viewbox { Width = 22, Height = 22, Child = canvas }
        };

        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(new TextBlock { Text = title, FontSize = 22, FontWeight = FontWeights.Bold, Foreground = Solid("#0F2A4A") });
        texts.Children.Add(new TextBlock { Text = subtitle, FontSize = 12, Foreground = Solid("#64748B") });

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 22)
        };
        header.Children.Add(badge);
        header.Children.Add(texts);
        return header;
    }

    private UIElement BuildLeftPanel()
    {
        var left = new Grid();
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });

        // ----- Carte 1 : recherche + liste des clients -----
        var searchButton = ActionButton("Rechercher", "BtnPrimary");
        searchButton.Margin = new Thickness(12, 0, 0, 0);
        searchButton.Click += async (_, _) => await RunAsync(() => LoadListAsync(_current?.Id));

        var searchIcon = new Shapes.Path
        {
            Data = (Geometry)Application.Current.FindResource("IconSearch"),
            Stroke = Solid("#94A3B8"),
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Stretch = Stretch.Uniform,
            Width = 16,
            Height = 16,
            Margin = new Thickness(13, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };

        _search.Padding = new Thickness(38, 7, 10, 7);
        _search.ToolTip = "Nom, téléphone ou numéro client (Entrée pour rechercher)";

        var searchBox = new Grid();
        searchBox.Children.Add(_search);
        searchBox.Children.Add(searchIcon);

        var searchBar = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(searchButton, Dock.Right);
        DockPanel.SetDock(_showInactive, Dock.Right);
        searchBar.Children.Add(searchButton);
        searchBar.Children.Add(_showInactive);
        searchBar.Children.Add(searchBox);

        _customersGrid.Columns.Add(Column("Numéro", "CustomerNumber", 110));
        _customersGrid.Columns.Add(Column("Nom", "FullName", 0));
        _customersGrid.Columns.Add(Column("Téléphone", "Phone", 130));
        _customersGrid.Columns.Add(Column("E-mail", "Email", 190));

        var listBody = new Grid();
        listBody.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        listBody.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(searchBar, 0);
        Grid.SetRow(_customersGrid, 1);
        listBody.Children.Add(searchBar);
        listBody.Children.Add(_customersGrid);

        var listCard = Card(listBody);
        Grid.SetRow(listCard, 0);
        left.Children.Add(listCard);

        // ----- Carte 2 : commandes du client sélectionné -----
        var historyTitle = new TextBlock
        {
            Text = "Commandes du client sélectionné",
            Margin = new Thickness(0, 0, 0, 10)
        };
        historyTitle.SetResourceReference(FrameworkElement.StyleProperty, "SectionTitle");

        _ordersGrid.Columns.Add(Column("N° commande", "Number", 170));
        _ordersGrid.Columns.Add(Column("Date", "Date", 140));
        _ordersGrid.Columns.Add(Column("Statut", "Status", 0));
        _ordersGrid.Columns.Add(Column("Total", "Total", 110));

        var historyBody = new Grid();
        historyBody.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        historyBody.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(historyTitle, 0);
        Grid.SetRow(_ordersGrid, 1);
        historyBody.Children.Add(historyTitle);
        historyBody.Children.Add(_ordersGrid);

        var historyCard = Card(historyBody);
        historyCard.Margin = new Thickness(0, 16, 0, 0);
        Grid.SetRow(historyCard, 1);
        left.Children.Add(historyCard);

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
        form.Children.Add(Caption("E-mail (reçus & suivi)"));
        form.Children.Add(_email);
        form.Children.Add(Caption("Adresse"));
        form.Children.Add(_address);
        form.Children.Add(Caption("Notes"));
        form.Children.Add(_notes);

        var buttons = new WrapPanel { Margin = new Thickness(0, 20, 0, 0) };
        buttons.Children.Add(_saveButton);
        buttons.Children.Add(_newButton);
        buttons.Children.Add(_toggleButton);
        form.Children.Add(buttons);

        form.Children.Add(_message);

        var card = Card(form);
        card.VerticalAlignment = VerticalAlignment.Top;
        return card;
    }

    // ---------- Petits utilitaires d'affichage ----------

    private static SolidColorBrush Solid(string hex) =>
        (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;

    /// <summary>Carte blanche arrondie (style « CardPanel » du thème commun).</summary>
    private static Border Card(UIElement child)
    {
        var card = new Border { Child = child };
        card.SetResourceReference(FrameworkElement.StyleProperty, "CardPanel");
        return card;
    }

    private static TextBlock Caption(string text)
    {
        var caption = new TextBlock { Text = text };
        caption.SetResourceReference(FrameworkElement.StyleProperty, "FieldLabel");
        return caption;
    }

    // L'aspect des champs, boutons et tableaux vient du thème (Resources/Theme.xaml).
    private static TextBox Input(int maxLength = 0, bool multiline = false)
    {
        var box = new TextBox();

        if (maxLength > 0)
        {
            box.MaxLength = maxLength;
        }

        if (multiline)
        {
            box.AcceptsReturn = true;
            box.TextWrapping = TextWrapping.Wrap;
            box.Height = 90;
            box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            box.VerticalContentAlignment = VerticalAlignment.Top;
        }

        return box;
    }

    private static Button ActionButton(string text, string styleKey)
    {
        var button = new Button
        {
            Content = text,
            Margin = new Thickness(0, 0, 8, 8)
        };
        button.SetResourceReference(FrameworkElement.StyleProperty, styleKey);
        return button;
    }

    private static DataGrid CreateGrid() => new()
    {
        AutoGenerateColumns = false,
        IsReadOnly = true,
        CanUserAddRows = false,
        CanUserDeleteRows = false,
        SelectionMode = DataGridSelectionMode.Single
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
