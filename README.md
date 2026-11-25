# Pressing Manager
Application Windows de gestion d'un pressing. C#, WPF, PostgreSQL (base partagée), EF Core. Voir docs/README-postgresql.md pour la configuration.

## Interface (thème commun)
- Tous les styles (barres de défilement fines et arrondies, boutons, champs, listes déroulantes, tableaux, icônes) sont dans `src/PressingManager.App/Resources/Theme.xaml`, chargé par `App.xaml`. Ils s'appliquent automatiquement à tous les écrans.
- Boutons : `Style="{StaticResource BtnPrimary}"` (bleu), `BtnLight` (blanc), `BtnDanger` (rouge). Cartes : `CardPanel`. Étiquettes de champ : `FieldLabel`.
- Modèle d'écran : l'onglet Statistiques (pastille marine + icône turquoise, cartes blanches arrondies). Pour ajouter un onglet : une icône `Geometry` dans `Theme.xaml`, un `RadioButton` dans `DashboardWindow.xaml` (style `NavItem`) et une zone `ContentControl`.
- Onglet Paramètres : `Views/SettingsView.xaml` (compte, langue, mot de passe, déconnexion).
