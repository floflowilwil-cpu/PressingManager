# Base de données PostgreSQL partagée

L'application n'utilise plus SQLite : tous les postes se connectent à **une même base PostgreSQL**.

## 1. Choisir où héberger la base (une seule fois pour toute l'équipe)

- **Hébergé en ligne** (Neon, Supabase, Railway… offres gratuites) : accessible de partout, le plus simple pour une équipe.
- **Sur un PC de l'équipe** : `docker compose up -d` à la racine du projet (voir `docker-compose.yml`), puis les autres se connectent à l'adresse IP de ce PC (port 5432 ouvert sur le réseau).

## 2. Configurer la connexion (chaque développeur)

1. Dans `src/PressingManager.App/`, copier `appsettings.local.json.example` en `appsettings.local.json`.
2. Y mettre la vraie chaîne de connexion (hôte, base, utilisateur, mot de passe).
3. Ce fichier est ignoré par git : **ne jamais le commiter**, ne pas mettre le mot de passe dans `appsettings.json`.

Alternative : variable d'environnement `ConnectionStrings__Pressing`.

## 3. Créer les tables (une seule fois, par UNE personne)

Les anciennes migrations SQLite ont été supprimées. Il faut en générer une nouvelle pour PostgreSQL :

```bash
dotnet tool install --global dotnet-ef
dotnet restore
dotnet ef migrations add InitialCreate --project src/PressingManager.Infrastructure --startup-project src/PressingManager.App --output-dir Data/Migrations
```

Commiter le dossier `Data/Migrations` généré : les autres ne font que `git pull`.
Au lancement, l'application applique les migrations toute seule (`Database.Migrate()`).

## 4. Comptes créés automatiquement

Au premier lancement, 10 comptes sont créés (s'ils n'existent pas déjà). Identifiant = prénom en minuscules, mot de passe = Prénom + `2026` :

| Identifiant | Mot de passe | Rôle |
|---|---|---|
| florent | Florent2026 | Administrateur |
| azra | Azra2026 | Administrateur |
| iness | Iness2026 | Agent d'accueil |
| john | John2026 | Agent d'accueil |
| ange | Ange2026 | Caissier |
| rose | Rose2026 | Caissier |
| wilfried | Wilfried2026 | Agent de traitement |
| angel | Angel2026 | Agent de traitement |
| ruan | Ruan2026 | Responsable stock |
| daphne | Daphne2026 | Responsable stock |

- Le mot de passe doit faire au moins 8 caractères (règle `PasswordPolicy`), d'où `2026` plutôt que `02`.
- Les rôles sont modifiables dans `Data/Seed/UserSeed.cs` ou depuis l'écran Utilisateurs.
- Ce sont des mots de passe de démonstration connus de tous : avant une vraie utilisation, mettre `"SeedDemoUsers": false` dans `appsettings.json` et les changer.

## 5. Tests

Les tests continuent d'utiliser SQLite en mémoire (rapides, sans serveur) : `dotnet test` n'a pas besoin de PostgreSQL.
