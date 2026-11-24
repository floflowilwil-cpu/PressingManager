using System.Windows;
using PressingManager.Core.DTOs;

namespace PressingManager.App.Views;

/// <summary>
/// Formulaire client (creation ou modification). Le callback <c>save</c> renvoie null si l'enregistrement
/// a reussi, sinon le message d'erreur a afficher sans fermer la fenetre.
/// </summary>
public partial class CustomerEditDialog : Window
{
    private readonly Func<string, string, string?, string?, Task<string?>> _save;

    public CustomerEditDialog(
        CustomerDto? existing,
        Func<string, string, string?, string?, Task<string?>> save)
    {
        InitializeComponent();

        _save = save ?? throw new ArgumentNullException(nameof(save));

        if (existing is not null)
        {
            Title = "Modifier le client";
            TitleText.Text = $"Modifier {existing.CustomerNumber}";
            NameBox.Text = existing.FullName;
            PhoneBox.Text = existing.Phone;
            AddressBox.Text = existing.Address ?? string.Empty;
            NotesBox.Text = existing.Notes ?? string.Empty;
        }

        Loaded += (_, _) => NameBox.Focus();
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        var name = NameBox.Text.Trim();
        var phone = PhoneBox.Text.Trim();

        if (name.Length == 0 || phone.Length == 0)
        {
            ShowError("Le nom et le téléphone sont obligatoires.");
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            var error = await _save(name, phone, NullIfEmpty(AddressBox.Text), NullIfEmpty(NotesBox.Text));
            if (error is null)
            {
                DialogResult = true;
            }
            else
            {
                ShowError(error);
            }
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private static string? NullIfEmpty(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
