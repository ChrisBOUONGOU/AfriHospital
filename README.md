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

### 1. Cloner le dépôt

```bash
git clone https://github.com/ChrisBOUONGOU/AfriHospital.git
cd AfriHospital
```

### 2. Restaurer les dépendances et compiler

Depuis le répertoire contenant la solution :

```bash
dotnet restore
dotnet build
```

### 3. Lancer l'application

Si le projet contient déjà une application exécutable :

```bash
dotnet run --project chemin/vers/AfriHospital.Desktop.csproj
```

Le chemin exact dépendra de l'organisation réelle du dépôt.

## 🔐 Sécurité et confidentialité

La protection des données médicales est une priorité de conception.

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
- [ ] Structurer la solution .NET.
- [ ] Créer l'interface principale Avalonia.
- [ ] Mettre en place MVVM.
- [ ] Configurer la persistance locale.
- [ ] Définir les premières entités métier.

### Phase 2 — Patients et consultations
- [ ] Enregistrer et rechercher les patients.
- [ ] Gérer les dossiers patients.
- [ ] Enregistrer les consultations.
- [ ] Ajouter les validations et les tests.

### Phase 3 — Gestion hospitalière
- [ ] Ajouter les rendez-vous et admissions.
- [ ] Développer l'hospitalisation.
- [ ] Ajouter la pharmacie et les stocks.
- [ ] Intégrer le laboratoire et la facturation.

### Phase 4 — Sécurité et déploiement
- [ ] Mettre en place les rôles et permissions.
- [ ] Développer l'API si nécessaire.
- [ ] Intégrer PostgreSQL pour le mode multi-utilisateurs.
- [ ] Tester les sauvegardes et la restauration.
- [ ] Étudier la synchronisation hors ligne.

Les cases doivent être cochées uniquement lorsque les fonctionnalités correspondantes sont développées et vérifiées.

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
| Entity Framework Core | Data access |
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
dotnet run --project path/to/AfriHospital.Desktop.csproj
```

The exact path depends on the repository's actual structure.

## 🔐 Security and Privacy

Protecting medical data is a core design priority.

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
- [ ] Structure the .NET solution.
- [ ] Create the main Avalonia interface.
- [ ] Implement MVVM.
- [ ] Configure local persistence.
- [ ] Define the initial business entities.

### Phase 2 — Patients and Consultations
- [ ] Register and search for patients.
- [ ] Manage patient records.
- [ ] Record medical consultations.
- [ ] Add validation and tests.

### Phase 3 — Hospital Management
- [ ] Add appointments and admissions.
- [ ] Implement hospitalization management.
- [ ] Add pharmacy and inventory features.
- [ ] Integrate laboratory and billing modules.

### Phase 4 — Security and Deployment
- [ ] Implement roles and permissions.
- [ ] Develop the API if required.
- [ ] Integrate PostgreSQL for multi-user operation.
- [ ] Test backup and recovery procedures.
- [ ] Evaluate offline synchronization.

Check items only after the corresponding features have been implemented and verified.

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
