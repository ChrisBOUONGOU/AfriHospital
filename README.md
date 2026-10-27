# 🏥 AfriHospital

**FR : Une solution numérique pour moderniser la gestion des établissements de santé en Afrique.**

**EN: A digital solution to modernize healthcare facility management across Africa.**

[🇫🇷 Français](#-français) | [🇬🇧 English](#-english)

---

# 🇫🇷 Français

## 🌍 Présentation

AfriHospital est un projet d'application de gestion hospitalière conçu pour faciliter le travail quotidien des professionnels de santé, améliorer l'organisation des établissements médicaux et centraliser les informations administratives et médicales des patients.

Développé avec **C#, .NET et Avalonia UI**, le projet vise à proposer une application multiplateforme, évolutive et adaptée aux réalités des établissements de santé en Afrique, notamment aux connexions Internet instables et aux ressources informatiques limitées.

> **Statut :** en développement. Les fonctionnalités décrites ci-dessous sont des objectifs du projet et ne sont pas nécessairement toutes implémentées.

## 🎯 Objectifs

- Numériser et organiser les dossiers des patients.
- Simplifier la gestion des consultations, des admissions et des hospitalisations.
- Faciliter le suivi des médicaments et des stocks.
- Améliorer la gestion administrative et la facturation.
- Préparer un fonctionnement adapté aux environnements où Internet est limité.
- Protéger les informations médicales et contrôler les accès.
- Permettre une évolution vers une architecture multi-utilisateurs.

## 🩺 Fonctionnalités prévues

### Gestion des patients
- Enregistrement et recherche des patients.
- Attribution d'un identifiant unique.
- Gestion des informations administratives.
- Historique des consultations et des hospitalisations.

### Consultations médicales
- Enregistrement des consultations.
- Saisie des symptômes, observations et diagnostics.
- Gestion des prescriptions.
- Suivi de l'évolution médicale.

### Rendez-vous et admissions
- Gestion des rendez-vous.
- Enregistrement des arrivées et des admissions.
- Organisation des files d'attente.
- Orientation vers les services concernés.

### Hospitalisation
- Gestion des admissions et des sorties.
- Suivi des séjours.
- Affectation des chambres et des lits.

### Pharmacie et stocks
- Gestion des médicaments.
- Suivi des entrées et sorties de stock.
- Alertes de stock faible.
- Suivi des dates de péremption.

### Laboratoire
- Gestion des demandes d'analyses.
- Suivi des examens.
- Enregistrement des résultats par le personnel autorisé.

### Facturation
- Création des factures.
- Gestion des prestations.
- Enregistrement des paiements.
- Suivi des montants restant à payer.

### Personnel et sécurité
- Gestion des comptes utilisateurs.
- Attribution de rôles et de permissions.
- Contrôle des accès.
- Journalisation des opérations sensibles.

### Tableau de bord et statistiques
- Statistiques sur les patients et les consultations.
- Suivi de l'occupation des lits.
- État des stocks.
- Rapports administratifs et financiers.

## 🛠️ Technologies

| Technologie | Utilisation |
|---|---|
| C# | Langage principal |
| .NET | Plateforme de développement |
| Avalonia UI | Interface graphique multiplateforme |
| XAML | Définition des interfaces |
| MVVM | Organisation de l'interface et de sa logique |
| CommunityToolkit.Mvvm | Outils pour MVVM |
| Entity Framework Core | Accès aux données |
| SQLite | Stockage local envisagé |
| PostgreSQL | Base de données serveur envisagée |
| ASP.NET Core | API pour les installations multi-utilisateurs |
| Git et GitHub | Gestion du code source |

Les technologies et versions définitives dépendront de l'évolution du projet.

## 🏗️ Architecture envisagée

```text
AfriHospital/
├── src/
│   ├── AfriHospital.Desktop/
│   ├── AfriHospital.Domain/
│   ├── AfriHospital.Application/
│   ├── AfriHospital.Infrastructure/
│   └── AfriHospital.API/
├── tests/
│   ├── AfriHospital.UnitTests/
│   └── AfriHospital.IntegrationTests/
├── docs/
├── README.md
└── AfriHospital.sln
```

Cette arborescence est une proposition et pourra être adaptée à la structure réelle du dépôt.

- **Desktop :** interface Avalonia.
- **Domain :** entités et règles métier.
- **Application :** services et cas d'utilisation.
- **Infrastructure :** persistance et accès aux données.
- **API :** communication entre les clients et le serveur.
- **Tests :** tests unitaires et d'intégration.

### Fonctionnement local et multi-utilisateurs

Le mode local pourrait utiliser SQLite pour un poste isolé ou un prototype.

Pour plusieurs postes, une architecture reposant sur une API ASP.NET Core et une base PostgreSQL centralisée est envisagée.

Le fonctionnement hors ligne avec synchronisation entre plusieurs appareils nécessitera une conception spécifique afin de gérer les conflits, les doublons et la cohérence des données.

## 💻 Installation

### Prérequis

- SDK .NET adapté au projet.
- Git.
- Visual Studio, Visual Studio Code ou JetBrains Rider.
- Les dépendances Avalonia nécessaires.

Documentation : [Avalonia UI](https://docs.avaloniaui.net/docs/get-started).

Le dépôt utilise encore un seul fichier projet, AfriHospital.csproj. Une suite de tests xUnit est présente et exécutée dans GitHub Actions ; le dernier résultat vérifié pour le code était une compilation réussie et 11 tests réussis.

### 1. Cloner le dépôt

```bash
git clone https://github.com/ChrisBOUONGOU/AfriHospital.git
cd AfriHospital
```

### 2. Restaurer les dépendances et compiler

Depuis le répertoire contenant la solution :

```bash
dotnet restore AfriHospital.csproj
dotnet build AfriHospital.csproj --configuration Release
```

### 3. Lancer l'application

Si le projet contient déjà une application exécutable :

```bash
dotnet run --project AfriHospital.csproj
```

Le chemin exact dépendra de l'organisation réelle du dépôt.

## 🔐 Sécurité et confidentialité

La protection des données médicales est une priorité de conception. Un écran de connexion, le parcours de création du premier administrateur et un écran administrateur de création de comptes sont maintenant connectés au service d'authentification. Les menus sont filtrés selon les rôles, et les événements de connexion/création de compte sont journalisés. Cette protection reste un premier niveau : les autorisations doivent encore être renforcées et testées. La base contient toujours deux schémas SQLite distincts.

Les mesures à prévoir comprennent :

- Authentification des utilisateurs.
- Gestion des rôles et des permissions.
- Protection des communications réseau.
- Protection des données stockées.
- Journalisation des opérations sensibles.
- Sauvegardes régulières et tests de restauration.
- Respect des lois applicables en matière de données personnelles et de santé.

AfriHospital ne doit pas être considéré comme un logiciel prêt pour une utilisation clinique en production avant les tests, validations et contrôles de sécurité nécessaires.

## 🗺️ Feuille de route

### Phase 1 — Fondations
- [ ] Structurer le code en projets .NET séparés (l'application utilise encore un seul projet).
- [x] Créer l'interface principale Avalonia avec tableau de bord et menu des modules.
- [ ] Mettre en place MVVM (la logique de l'interface reste principalement dans le code-behind).
- [x] Configurer une persistance SQLite locale pour les modules actuellement disponibles.
- [x] Définir les premières entités métier Patient, Consultation et HospitalUser.
- [ ] Vérifier le build complet et le lancement de l'application.

### Phase 2 — Patients et consultations
- [x] Enregistrer, rechercher et modifier les dossiers patients de base.
- [x] Enregistrer et afficher les consultations.
- [x] Relier les consultations et rendez-vous aux patients sélectionnés dans le schéma historique.
- [x] Valider le format de date et d'heure des rendez-vous et consultations.
- [x] Gérer les statuts des rendez-vous, leur modification, leur annulation et les conflits horaires exacts par patient.
- [ ] Compléter les validations métier des champs et des dates.
- [x] Ajouter et exécuter les premiers tests automatisés d'authentification.
- [ ] Étendre la couverture aux modules métier.

### Phase 3 — Gestion hospitalière
- [x] Créer et afficher des rendez-vous simples associés à un patient.
- [ ] Ajouter les admissions, files d'attente et orientation vers les services.
- [ ] Développer les admissions/sorties, séjours, chambres et lits.
- [x] Ajouter un catalogue simple de médicaments, les quantités et les alertes de stock faible.
- [x] Gérer les lots, numéros de lot, fournisseurs, dates de péremption et mouvements d'entrée/sortie du stock, avec conservation du stock historique sans date inventée.
- [x] Tester les mouvements de stock, les quantités de lots et le rejet des péremptions passées avec xUnit.
- [ ] Ajouter les demandes d'analyses et les résultats de laboratoire.
- [x] Créer et afficher des factures simples avec devise configurable.
- [x] Enregistrer des paiements partiels ou complets et calculer le solde restant.
- [x] Générer un reçu texte local après chaque paiement enregistré.
- [x] Enregistrer les remboursements partiels ou complets, recalculer le solde et le statut de facture, et générer des reçus texte et HTML imprimables pour paiements et remboursements.
- [ ] Valider les règles fiscales et monétaires du pays cible.

### Phase 4 — Sécurité et déploiement
- [x] Ajouter une fondation SQLite pour les nouvelles entités GUID et les comptes utilisateurs.
- [x] Ajouter les interfaces de dépôt et dépôts SQLite pour les nouvelles entités.
- [x] Ajouter le hachage PBKDF2-SHA256 et un service applicatif de vérification des identifiants.
- [ ] Unifier les nouvelles tables domain_patients/domain_consultations avec les tables historiques utilisées par l'interface.
- [x] Brancher la connexion à l'interface et créer un parcours de premier administrateur avec création atomique du premier compte.
- [ ] Renforcer et tester les rôles et permissions dans chaque module (le filtrage initial des menus est en place).
- [ ] Ajouter l'API ASP.NET Core si le mode multi-postes est retenu.
- [ ] Ajouter PostgreSQL pour le mode multi-utilisateurs si nécessaire.
- [x] Ajouter une sauvegarde locale manuelle de la base SQLite historique.
- [x] Ajouter une restauration SQLite avec vérification d'intégrité et copie de sécurité préalable.
- [ ] Tester la restauration sur plusieurs scénarios et automatiser les sauvegardes périodiques.
- [ ] Chiffrer les données sensibles et les sauvegardes, sécuriser les secrets et les accès.
- [x] Journaliser les connexions réussies/échouées et la création de comptes.
- [ ] Étendre et tester le journal d'audit sur toutes les opérations sensibles.
- [ ] Tester le fonctionnement hors ligne et la synchronisation multi-postes.
- [x] Ajouter des tests automatisés xUnit et les exécuter dans GitHub Actions.
- [x] Confirmer qu'une compilation et les tests automatisés réussissent dans GitHub Actions (8 tests réussis sur le commit de code vérifié).

> Les cases cochées indiquent uniquement que la fonctionnalité existe dans le dépôt ; elles ne signifient pas qu'elle est entièrement testée ou validée pour un usage clinique. L'authentification est reliée à l'interface principale. En revanche, les nouvelles entités métier GUID et leurs dépôts SQLite ne sont pas encore unifiés avec les tables historiques utilisées par les modules existants.

## 🤝 Contribution

Les contributions et suggestions sont les bienvenues.

1. Consultez les issues existantes.
2. Créez une branche pour votre modification.
3. Effectuez des changements ciblés.
4. Ajoutez les tests nécessaires.
5. Vérifiez que le projet compile.
6. Ouvrez une pull request décrivant vos changements.

Ne publiez jamais de données réelles de patients dans les issues ou les exemples.

## 📄 Licence

La licence reste à définir. Tant qu'un fichier `LICENSE` n'est pas ajouté au dépôt, les droits de réutilisation et de redistribution doivent être considérés comme non précisés.

---

# 🇬🇧 English

## 🌍 Overview

AfriHospital is a hospital management application project designed to support healthcare professionals, improve healthcare facility operations, and centralize patients' administrative and medical information.

Built with **C#, .NET, and Avalonia UI**, the project aims to provide a cross-platform and extensible application adapted to healthcare facilities across Africa, including environments with unreliable Internet connectivity and limited computing resources.

> **Status:** In development. The features described below are project goals and may not all be implemented yet.

## 🎯 Objectives

- Digitize and organize patient records.
- Simplify consultation, admission, and hospitalization management.
- Improve medication and inventory tracking.
- Streamline administrative operations and billing.
- Support environments where Internet access is limited.
- Protect medical information and control access.
- Provide a path toward a multi-user architecture.

## 🩺 Planned Features

### Patient Management
- Patient registration and search.
- Unique patient identifiers.
- Administrative information management.
- Consultation and hospitalization history.

### Medical Consultations
- Consultation records.
- Symptom, clinical observation, and diagnosis entry.
- Prescription management.
- Medical progress tracking.

### Appointments and Admissions
- Appointment scheduling.
- Patient arrival and admission registration.
- Waiting-list management.
- Patient routing to the appropriate departments.

### Hospitalization
- Admission and discharge management.
- Inpatient stay tracking.
- Room and bed allocation.

### Pharmacy and Inventory
- Medication catalog management.
- Stock-in and stock-out tracking.
- Low-stock alerts.
- Expiration-date monitoring.

### Laboratory
- Test request management.
- Examination status tracking.
- Test result entry by authorized staff.

### Billing
- Invoice creation.
- Service and procedure billing.
- Payment recording.
- Outstanding balance tracking.

### Staff and Security
- User account management.
- Role and permission assignment.
- Access control.
- Audit logging for sensitive operations.

### Dashboard and Statistics
- Patient and consultation statistics.
- Bed occupancy monitoring.
- Inventory overview.
- Administrative and financial reports.

## 🛠️ Technology Stack

| Technology | Purpose |
|---|---|
| C# | Main programming language |
| .NET | Development platform |
| Avalonia UI | Cross-platform graphical interface |
| XAML | User interface definitions |
| MVVM | Separation of UI and presentation logic |
| CommunityToolkit.Mvvm | MVVM development utilities |
| Microsoft.Data.Sqlite | Direct SQLite data access (currently used) |
| SQLite | Planned local storage |
| PostgreSQL | Planned server database |
| ASP.NET Core | API for multi-user deployments |
| Git and GitHub | Source control and collaboration |

The final technology choices and versions will depend on the project's development.

## 🏗️ Proposed Architecture

```text
AfriHospital/
├── src/
│   ├── AfriHospital.Desktop/
│   ├── AfriHospital.Domain/
│   ├── AfriHospital.Application/
│   ├── AfriHospital.Infrastructure/
│   └── AfriHospital.API/
├── tests/
│   ├── AfriHospital.UnitTests/
│   └── AfriHospital.IntegrationTests/
├── docs/
├── README.md
└── AfriHospital.sln
```

This is a proposed structure and may be adapted to match the repository's actual organization.

- **Desktop:** Avalonia user interface.
- **Domain:** Business entities and core rules.
- **Application:** Services and use cases.
- **Infrastructure:** Persistence and data access.
- **API:** Communication between clients and the server.
- **Tests:** Unit and integration tests.

### Local and Multi-User Operation

The local mode could use SQLite for a standalone workstation or an initial prototype.

For multiple workstations, the proposed architecture uses an ASP.NET Core API and a centralized PostgreSQL database.

Offline operation with synchronization across devices will require a dedicated strategy to handle conflicts, duplicate records, and data consistency.

## 💻 Installation

### Prerequisites

- A .NET SDK compatible with the project.
- Git.
- Visual Studio, Visual Studio Code, or JetBrains Rider.
- The required Avalonia dependencies.

Documentation: [Avalonia UI](https://docs.avaloniaui.net/docs/get-started).

### 1. Clone the repository

```bash
git clone https://github.com/ChrisBOUONGOU/AfriHospital.git
cd AfriHospital
```

### 2. Restore dependencies and build

From the directory containing the solution:

```bash
dotnet restore
dotnet build
```

### 3. Run the application

If an executable application project already exists:

```bash
dotnet run --project AfriHospital.csproj
```

The exact path depends on the repository's actual structure.

## 🔐 Security and Privacy

Protecting medical data is a core design priority. A login screen, first-administrator setup, and an administrator user-creation screen are now connected to the authentication service. Navigation is filtered by role, and login/account-creation events are audited. This is an initial protection layer: permissions still need stronger enforcement and testing. The database still contains two separate SQLite schemas.

Planned safeguards include:

- User authentication.
- Role-based permissions.
- Secure network communications.
- Protection of stored data.
- Audit logging for sensitive operations.
- Regular backups and restoration tests.
- Compliance with applicable healthcare and personal data protection laws.

AfriHospital must not be considered production-ready for clinical use until the necessary testing, validation, and security reviews have been completed.

## 🗺️ Roadmap

### Phase 1 — Foundations
- [ ] Split the code into separate .NET projects (the application still uses a single project).
- [x] Create the main Avalonia interface with a dashboard and module navigation.
- [ ] Implement MVVM (UI logic is still mainly in code-behind).
- [x] Configure local SQLite persistence for the currently available modules.
- [x] Define the initial Patient, Consultation, and HospitalUser domain entities.
- [ ] Verify a complete build and launch the application.

### Phase 2 — Patients and Consultations
- [x] Register, search, and edit basic patient records.
- [x] Record and display consultations.
- [x] Link consultations and appointments to selected patients in the legacy schema.
- [x] Validate appointment and consultation date/time formats.
- [x] Manage appointment statuses, editing, cancellation, and exact-time conflicts for the same patient.
- [ ] Complete business validation for fields and dates.
- [x] Add and run initial automated authentication tests.
- [ ] Extend test coverage to business modules.

### Phase 3 — Hospital Management
- [x] Create and display basic appointments linked to a patient.
- [ ] Add admissions, waiting lists, and routing to departments.
- [ ] Implement inpatient admission/discharge, stays, rooms, and beds.
- [x] Add a basic medication catalog, quantities, and low-stock alerts.
- [x] Track batches, batch numbers, suppliers, expiry dates, and stock-in/stock-out movements while preserving legacy stock without inventing expiry dates.
- [x] Test stock movements, batch quantities, and rejection of past expiry dates with xUnit.
- [ ] Add laboratory test requests and results.
- [x] Create and display basic invoices with a configurable currency.
- [x] Record partial/full payments and calculate outstanding balances.
- [x] Generate a local text receipt after each recorded payment.
- [x] Record partial/full refunds, recalculate invoice balances and statuses, and generate text plus print-ready HTML receipts for payments and refunds.
- [ ] Validate tax and currency rules for the target country.

### Phase 4 — Security and Deployment
- [x] Add SQLite foundations for the new GUID entities and user accounts.
- [x] Add repository interfaces and SQLite repositories for the new entities.
- [x] Add PBKDF2-SHA256 password hashing and an application service for credential verification.
- [ ] Unify the new domain_patients/domain_consultations tables with the legacy tables used by the UI.
- [x] Connect login to the UI and create a first-administrator setup flow that only works when no user account exists.
- [ ] Strengthen and test role permissions in every module (initial role-based navigation filtering is in place).
- [x] Add an administrator page to create accounts with predefined roles.
- [ ] Add an ASP.NET Core API if multi-workstation operation is selected.
- [ ] Add PostgreSQL for multi-user operation if needed.
- [x] Add a manual local backup action for the legacy SQLite database.
- [x] Add SQLite restore with integrity checking and a safety copy of the current database.
- [ ] Test restoration scenarios and automate periodic backups.
- [ ] Encrypt sensitive data and backups, and secure secrets and access.
- [x] Audit successful/failed logins and user-account creation.
- [ ] Extend and test the audit log for all sensitive operations.
- [ ] Test offline operation and multi-workstation synchronization.
- [x] Add xUnit automated tests and run them in GitHub Actions.
- [x] Confirm a successful build and automated test run in GitHub Actions (8 tests pass on the verified code commit).

> Checked items mean only that the functionality exists in the repository; they do not mean it is fully tested or approved for clinical use. Authentication is connected to the main UI, but the new GUID-based domain entities and repositories are not yet unified with the legacy tables used by existing modules.

## 🤝 Contributing

Contributions and suggestions are welcome.

1. Review existing issues.
2. Create a branch for your changes.
3. Make focused, documented changes.
4. Add the necessary tests.
5. Ensure the project builds successfully.
6. Open a pull request explaining your changes.

Never publish real patient data in issues, examples, or test fixtures.

## 📄 License

The project's license has not yet been defined. Until a `LICENSE` file is added to the repository, reuse and redistribution rights should be considered unspecified.

---

## 🔗 Project Repository

[AfriHospital on GitHub](https://github.com/ChrisBOUONGOU/AfriHospital)

**AfriHospital — Toward more accessible, organized, and context-aware healthcare management across Africa.**
