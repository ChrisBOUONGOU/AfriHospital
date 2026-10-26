using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.Data.Sqlite;
using AfriHospital.src.Application.Services;
using AfriHospital.src.Domain.Entities;
using AfriHospital.src.Infrastructure.Persistence;
using AfriHospital.src.Infrastructure.Repositories;
using AfriHospital.src.Infrastructure.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AfriHospital;

public partial class MainWindow : Window
{
    private readonly HospitalDatabase _database = new();
    private readonly SqliteConnectionFactory _connectionFactory = new();
    private readonly SqliteHospitalUserRepository _users;
    private readonly AuthenticationService _authentication;
    private HospitalUser? _currentUser;
    private readonly StackPanel _content = new() { Spacing = 16 };
    private readonly TextBlock _pageTitle = new() { FontSize = 28, FontWeight = FontWeight.Bold, Foreground = Brush("#171717") };
    private readonly TextBlock _status = new() { FontSize = 12, Foreground = Brush("#6B7280") };

    public MainWindow()
    {
        InitializeComponent();
        _users = new SqliteHospitalUserRepository(_connectionFactory);
        _authentication = new AuthenticationService(_connectionFactory, _users, new PasswordHasher());
        _ = ShowAuthenticationAsync();
    }

    private async Task ShowAuthenticationAsync()
    {
        try
        {
            await _connectionFactory.InitializeAsync();
            var firstSetup = await _authentication.CountUsersAsync() == 0;
            var username = new TextBox { Watermark = "Nom d'utilisateur", MinWidth = 280, Padding = new Thickness(10) };
            var password = new PasswordBox { Watermark = "Mot de passe", MinWidth = 280, Padding = new Thickness(10) };
            var confirmation = new PasswordBox { Watermark = "Confirmer le mot de passe", MinWidth = 280, Padding = new Thickness(10), IsVisible = firstSetup };
            var message = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brush("#B91C1C") };
            var submit = ActionButton(firstSetup ? "Créer l'administrateur" : "Se connecter");
            var panel = new StackPanel { Spacing = 14, Width = 360, Children =
            {
                new TextBlock { Text = "✚ AfriHospital", FontSize = 28, FontWeight = FontWeight.Bold, Foreground = Brush("#171717"), HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = firstSetup ? "Première configuration — créez le compte administrateur." : "Connectez-vous pour accéder aux modules hospitaliers.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#4B5563") },
                username, password, confirmation, submit, message
            }};
            submit.Click += async (_, _) =>
            {
                submit.IsEnabled = false;
                try
                {
                    if (firstSetup)
                    {
                        if (password.Password != confirmation.Password)
                        {
                            message.Text = "Les mots de passe ne correspondent pas.";
                            return;
                        }
                        _currentUser = await _authentication.CreateInitialAdministratorAsync(username.Text ?? "", password.Password ?? "");
                    }
                    else
                    {
                        _currentUser = await _authentication.AuthenticateAsync(username.Text ?? "", password.Password ?? "");
                        if (_currentUser is null)
                        {
                            message.Text = "Identifiants incorrects ou compte désactivé.";
                            return;
                        }
                    }
                    BuildShell();
                    await ShowDashboardAsync();
                }
                catch (Exception ex)
                {
                    message.Text = ex.Message;
                }
                finally { submit.IsEnabled = true; }
            };

            Content = new Grid
            {
                Background = Brush("#F3F4F6"),
                Children =
                {
                    new Border { Background = Brushes.White, BorderBrush = Brush("#E5E7EB"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(32), Width = 440, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = panel }
                }
            };
        }
        catch (Exception ex)
        {
            Content = new TextBlock { Text = "Impossible d'initialiser la connexion : " + ex.Message, Margin = new Thickness(24), TextWrapping = TextWrapping.Wrap };
        }
    }

    private bool CanAccess(string module)
    {
        var role = _currentUser?.Role ?? "";
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase)) return true;
        if (module.Equals("Tableau de bord", StringComparison.OrdinalIgnoreCase)) return _currentUser is not null;
        return module switch
        {
            "Patients" => role.Equals("Receptionist", StringComparison.OrdinalIgnoreCase) || role.Equals("Doctor", StringComparison.OrdinalIgnoreCase) || role.Equals("Nurse", StringComparison.OrdinalIgnoreCase),
            "Consultations" => role.Equals("Doctor", StringComparison.OrdinalIgnoreCase) || role.Equals("Nurse", StringComparison.OrdinalIgnoreCase),
            "Rendez-vous" => role.Equals("Receptionist", StringComparison.OrdinalIgnoreCase) || role.Equals("Doctor", StringComparison.OrdinalIgnoreCase) || role.Equals("Nurse", StringComparison.OrdinalIgnoreCase),
            "Pharmacie / Stocks" => role.Equals("Pharmacist", StringComparison.OrdinalIgnoreCase),
            "Facturation" => role.Equals("Billing", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private void AddProtectedNav(StackPanel nav, string label, Func<Task> action)
    {
        if (CanAccess(label)) AddNav(nav, label, action);
    }

    private void Logout()
    {
        _currentUser = null;
        _content.Children.Clear();
        _ = ShowAuthenticationAsync();
    }

    private void BuildShell()
    {
        var nav = new StackPanel { Spacing = 8, Width = 220, Background = Brush("#171717") };
        nav.Children.Add(new StackPanel
        {
            Spacing = 2,
            Margin = new Thickness(0, 0, 0, 18),
            Children =
            {
                new Border
                {
                    Background = Brush("#A8E66B"), CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(12), Child = new TextBlock
                    {
                        Text = "✚  AfriHospital", FontSize = 20, FontWeight = FontWeight.Bold,
                        Foreground = Brush("#171717")
                    }
                },
                new TextBlock { Text = "GESTION HOSPITALIÈRE", FontSize = 10, Margin = new Thickness(4, 6), Foreground = Brush("#D1D5DB") }
            }
        });
        if (CanAccess("Tableau de bord")) AddNav(nav, "Tableau de bord", async () => await ShowDashboardAsync());
        AddProtectedNav(nav, "Patients", async () => await ShowPatientsAsync());
        AddProtectedNav(nav, "Consultations", async () => await ShowConsultationsAsync());
        AddProtectedNav(nav, "Rendez-vous", async () => await ShowAppointmentsAsync());
        AddProtectedNav(nav, "Pharmacie / Stocks", async () => await ShowMedicinesAsync());
        AddProtectedNav(nav, "Facturation", async () => await ShowInvoicesAsync());
        if (CanAccess("Utilisateurs")) AddNav(nav, "Utilisateurs", async () => await ShowUsersAsync());
        if (CanAccess("Sauvegarde")) AddNav(nav, "Sauvegarde", async () => await ShowBackupAsync());
        var logout = new Button { Content = "Se déconnecter", HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, Foreground = Brushes.White, Padding = new Thickness(12, 11), CornerRadius = new CornerRadius(7) };
        logout.Click += (_, _) => Logout();
        nav.Children.Add(logout);
        nav.Children.Add(new Border { Height = 1, Background = Brush("#404040"), Margin = new Thickness(4, 18) });
        nav.Children.Add(new TextBlock
        {
            Text = "Prototype local\nDonnées enregistrées sur cet appareil",
            Foreground = Brush("#D1D5DB"), FontSize = 11, Margin = new Thickness(8), TextWrapping = TextWrapping.Wrap
        });

        var right = new StackPanel { Spacing = 18, Margin = new Thickness(28) };
        right.Children.Add(_pageTitle);
        right.Children.Add(_status);
        right.Children.Add(new Border { Height = 1, Background = Brush("#E5E7EB") });
        right.Children.Add(_content);

        var scroll = new ScrollViewer { Content = right, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("240,*") };
        var navBorder = new Border { Padding = new Thickness(18), Background = Brush("#171717"), Child = nav };
        Grid.SetColumn(navBorder, 0);
        Grid.SetColumn(scroll, 1);
        root.Children.Add(navBorder);
        root.Children.Add(scroll);
        Content = root;
    }

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));

    private void AddNav(StackPanel nav, string label, Func<Task> action)
    {
        var button = new Button
        {
            Content = label, HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent, Foreground = Brushes.White,
            Padding = new Thickness(12, 11), CornerRadius = new CornerRadius(7)
        };
        button.Click += async (_, _) => await SafeRunAsync(action);
        nav.Children.Add(button);
    }

    private async Task SafeRunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { SetStatus("Erreur : " + ex.Message, true); }
    }

    private void BeginPage(string title, string description)
    {
        _pageTitle.Text = title;
        _status.Text = description;
        _content.Children.Clear();
    }

    private void SetStatus(string text, bool error = false)
    {
        _status.Text = text;
        _status.Foreground = Brush(error ? "#B91C1C" : "#6B7280");
    }

    private Border Card(Control child) => new()
    {
        Background = Brushes.White, BorderBrush = Brush("#E5E7EB"),
        BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12),
        Padding = new Thickness(18), Child = child
    };

    private TextBlock Heading(string text) => new()
    {
        Text = text, FontSize = 17, FontWeight = FontWeight.SemiBold,
        Foreground = Brush("#171717")
    };

    private TextBox Field(string watermark) => new()
    {
        Watermark = watermark, MinWidth = 220, Padding = new Thickness(10),
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    private Button ActionButton(string text, bool primary = true) => new()
    {
        Content = text, Padding = new Thickness(14, 10),
        Background = Brush(primary ? "#A8E66B" : "#FFD43B"),
        Foreground = Brush("#171717"), FontWeight = FontWeight.SemiBold,
        CornerRadius = new CornerRadius(7)
    };

    private async Task ShowUsersAsync()
    {
        if (!CanAccess("Utilisateurs"))
        {
            BeginPage("Accès refusé", "Seul un administrateur peut gérer les comptes utilisateurs.");
            return;
        }

        BeginPage("Utilisateurs", "Création de comptes et attribution de rôles. Utilisez des comptes nominatifs et des mots de passe uniques.");
        await _connectionFactory.InitializeAsync();
        var username = Field("Nom d'utilisateur");
        var password = new PasswordBox { Watermark = "Mot de passe (12 caractères minimum)", MinWidth = 260, Padding = new Thickness(10) };
        var role = new ComboBox { MinWidth = 260, ItemsSource = new[] { "Doctor", "Nurse", "Receptionist", "Pharmacist", "Billing" }, SelectedIndex = 0 };
        var list = new ListBox { MinHeight = 200 };
        async Task Refresh()
        {
            var users = await _users.GetAllAsync();
            list.ItemsSource = users.Select(u => $"{u.Username} · {u.Role} · {(u.IsActive ? "Actif" : "Désactivé")}").ToList();
        }
        var save = ActionButton("Créer le compte");
        save.Click += async (_, _) => await SafeRunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(username.Text) || string.IsNullOrWhiteSpace(password.Password) || role.SelectedItem is not string selectedRole)
            {
                SetStatus("Renseignez le nom d'utilisateur, le mot de passe et le rôle.", true);
                return;
            }
            await _authentication.CreateUserAsync(username.Text.Trim(), password.Password, selectedRole);
            username.Text = "";
            password.Password = "";
            await Refresh();
            SetStatus("Compte utilisateur créé.");
        });
        _content.Children.Add(Card(new StackPanel { Spacing = 12, Children =
        {
            Heading("Créer un compte utilisateur"), username, password, role, save,
            new TextBlock { Text = "Le mot de passe doit contenir au moins 12 caractères. Les comptes sont créés actifs.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#4B5563") }
        }}));
        _content.Children.Add(Card(new StackPanel { Spacing = 10, Children = { Heading("Comptes enregistrés"), list } }));
        await Refresh();
    }

    private async Task ShowDashboardAsync()
    {
        BeginPage("Tableau de bord", "Vue d’ensemble — les données affichées proviennent de la base locale.");
        await _database.InitializeAsync();
        var counts = await _database.GetCountsAsync();
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 12, RowSpacing = 12 };
        AddMetric(grid, "Patients", counts["patients"], 0, 0);
        AddMetric(grid, "Consultations", counts["consultations"], 1, 0);
        AddMetric(grid, "Rendez-vous", counts["appointments"], 2, 0);
        AddMetric(grid, "Médicaments", counts["medicines"], 0, 1);
        AddMetric(grid, "Factures", counts["invoices"], 1, 1);
        AddMetric(grid, "Alertes stock", counts["low_stock"], 2, 1);
        _content.Children.Add(Card(new StackPanel { Spacing = 8, Children =
        {
            new TextBlock { Text = "Bienvenue sur AfriHospital", FontSize = 22, FontWeight = FontWeight.Bold, Foreground = Brush("#17220F") },
            new TextBlock { Text = "Gérez les informations administratives de votre établissement depuis une interface simple.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#374151") },
            new Border { Background = Brush("#FFF7D6"), CornerRadius = new CornerRadius(7), Padding = new Thickness(10),
                Child = new TextBlock { Text = "Important : prototype non certifié. N’y saisissez pas de données médicales réelles avant validation de la sécurité et des sauvegardes.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#5B4300") } }
        }}));
        _content.Children.Add(grid);
    }

    private void AddMetric(Grid grid, string title, long value, int column, int row)
    {
        var card = Card(new StackPanel { Spacing = 8, Children =
        {
            new TextBlock { Text = title.ToUpperInvariant(), FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush("#6B7280") },
            new TextBlock { Text = value.ToString(), FontSize = 30, FontWeight = FontWeight.Bold, Foreground = Brush("#171717") }
        }});
        Grid.SetColumn(card, column); Grid.SetRow(card, row); grid.Children.Add(card);
    }

    private async Task ShowPatientsAsync()
    {
        BeginPage("Patients", "Créer, rechercher et modifier les dossiers administratifs de démonstration.");
        await _database.InitializeAsync();
        var name = Field("Nom complet *"); var phone = Field("Téléphone"); var birth = Field("Date de naissance (AAAA-MM-JJ)"); var gender = Field("Sexe (optionnel)"); var notes = Field("Notes administratives (optionnel)");
        var search = Field("Rechercher par nom ou téléphone");
        var selectedId = (long?)null;
        var list = new ListBox { MinHeight = 220 };
        async Task Refresh()
        {
            var rows = await _database.SearchPatientsAsync(search.Text ?? "");
            list.ItemsSource = rows;
        }
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is PatientRow p)
            {
                selectedId = p.Id; name.Text = p.Name; phone.Text = p.Phone;
                birth.Text = p.BirthDate; gender.Text = p.Gender; notes.Text = p.Notes;
            }
        };
        search.TextChanged += async (_, _) => await SafeRunAsync(Refresh);
        var save = ActionButton("Enregistrer le patient");
        save.Click += async (_, _) => await SafeRunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { SetStatus("Le nom du patient est obligatoire.", true); return; }
            await _database.SavePatientAsync(selectedId, name.Text.Trim(), phone.Text ?? "", birth.Text ?? "", gender.Text ?? "", notes.Text ?? "");
            selectedId = null; name.Text = phone.Text = birth.Text = gender.Text = notes.Text = "";
            await Refresh(); SetStatus("Patient enregistré dans la base locale.");
        });
        var clear = ActionButton("Nouveau / Effacer", false);
        clear.Click += async (_, _) => { selectedId = null; name.Text = phone.Text = birth.Text = gender.Text = notes.Text = ""; list.SelectedItem = null; await Task.CompletedTask; };
        _content.Children.Add(Card(new StackPanel { Spacing = 12, Children =
        {
            Heading("Fiche patient"), name, phone, birth, gender, notes,
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { save, clear } }
        }}));
        _content.Children.Add(Card(new StackPanel { Spacing = 10, Children = { Heading("Dossiers enregistrés"), search, list } }));
        await Refresh();
    }

    private async Task ShowAppointmentsAsync()
    {
        BeginPage("Rendez-vous", "Planification simple — dates et heures à saisir au format local.");
        await _database.InitializeAsync();
        var patients = new ComboBox { MinWidth = 260, PlaceholderText = "Sélectionner un patient" };
        patients.ItemsSource = await _database.GetPatientChoicesAsync();
        var date = Field("Date et heure (AAAA-MM-JJ HH:MM)"); var reason = Field("Motif du rendez-vous");
        var list = new ListBox { MinHeight = 240 };
        async Task Refresh() => list.ItemsSource = await _database.GetRowsAsync("SELECT a.id, COALESCE(p.name, a.patient_name), a.scheduled_at, a.reason, a.status FROM appointments a LEFT JOIN patients p ON p.id = a.patient_id ORDER BY a.scheduled_at DESC", r => $"#{r.GetInt64(0)} · {r.GetString(1)} · {r.GetString(2)} · {r.GetString(3)} · {r.GetString(4)}");
        var save = ActionButton("Ajouter le rendez-vous");
        save.Click += async (_, _) => await SafeRunAsync(async () =>
        {
            if (patients.SelectedItem is not PatientRow selectedPatient || string.IsNullOrWhiteSpace(date.Text)) { SetStatus("Sélectionnez un patient et indiquez la date.", true); return; }
            if (!DateTime.TryParseExact(date.Text.Trim(), new[] { "yyyy-MM-dd HH:mm", "yyyy-MM-dd H:mm" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var scheduledAt)) { SetStatus("Date invalide. Format attendu : AAAA-MM-JJ HH:MM.", true); return; }
            await _database.ExecuteAsync("INSERT INTO appointments(patient_id, patient_name, scheduled_at, reason, status) VALUES($id,$p,$d,$r,'Planifié')", ("$id", selectedPatient.Id), ("$p", selectedPatient.Name), ("$d", scheduledAt.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)), ("$r", reason.Text ?? ""));
            patients.SelectedItem = null; date.Text = reason.Text = ""; await Refresh(); SetStatus("Rendez-vous ajouté.");
        });
        _content.Children.Add(Card(new StackPanel { Spacing = 12, Children = { Heading("Nouveau rendez-vous"), patients, date, reason, save } }));
        _content.Children.Add(Card(new StackPanel { Spacing = 10, Children = { Heading("Rendez-vous enregistrés"), list } }));
        await Refresh();
    }

    private async Task ShowConsultationsAsync()
    {
        BeginPage("Consultations", "Enregistrement administratif des consultations. Les décisions médicales doivent rester sous responsabilité d’un professionnel.");
        await _database.InitializeAsync();
        var patients = new ComboBox { MinWidth = 260, PlaceholderText = "Sélectionner un patient" };
        patients.ItemsSource = await _database.GetPatientChoicesAsync();
        var date = Field("Date et heure (AAAA-MM-JJ HH:MM)"); var clinician = Field("Professionnel de santé"); var summary = Field("Résumé / motif de consultation");
        var list = new ListBox { MinHeight = 220 };
        async Task Refresh() => list.ItemsSource = await _database.GetRowsAsync("SELECT c.id, COALESCE(p.name, c.patient_name), c.consulted_at, c.clinician, c.summary FROM consultations c LEFT JOIN patients p ON p.id = c.patient_id ORDER BY c.consulted_at DESC", r => $"#{r.GetInt64(0)} · {r.GetString(1)} · {r.GetString(2)} · {r.GetString(3)} — {r.GetString(4)}");
        var save = ActionButton("Enregistrer la consultation");
        save.Click += async (_, _) => await SafeRunAsync(async () =>
        {
            if (patients.SelectedItem is not PatientRow selectedPatient || string.IsNullOrWhiteSpace(date.Text)) { SetStatus("Sélectionnez un patient et indiquez la date.", true); return; }
            if (!DateTime.TryParseExact(date.Text.Trim(), new[] { "yyyy-MM-dd HH:mm", "yyyy-MM-dd H:mm" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var consultedAt)) { SetStatus("Date invalide. Format attendu : AAAA-MM-JJ HH:MM.", true); return; }
            await _database.ExecuteAsync("INSERT INTO consultations(patient_id, patient_name, consulted_at, clinician, summary) VALUES($id,$p,$d,$c,$s)", ("$id", selectedPatient.Id), ("$p", selectedPatient.Name), ("$d", consultedAt.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)), ("$c", clinician.Text ?? ""), ("$s", summary.Text ?? ""));
            patients.SelectedItem = null; date.Text = clinician.Text = summary.Text = ""; await Refresh(); SetStatus("Consultation enregistrée.");
        });
        _content.Children.Add(Card(new StackPanel { Spacing = 12, Children = { Heading("Nouvelle consultation"), patients, date, clinician, summary, save } }));
        _content.Children.Add(Card(new StackPanel { Spacing = 10, Children = { Heading("Historique des consultations"), list } }));
        await Refresh();
    }

    private async Task ShowMedicinesAsync()
    {
        BeginPage("Pharmacie et stocks", "Catalogue minimal et suivi des quantités. La gestion des lots, péremptions et dispensations devra être ajoutée avant utilisation réelle.");
        await _database.InitializeAsync();
        var name = Field("Nom du médicament"); var quantity = Field("Quantité en stock"); var threshold = Field("Seuil d’alerte"); var unit = Field("Unité (boîte, flacon…)"); var price = Field("Prix unitaire (nombre)");
        var list = new ListBox { MinHeight = 240 };
        async Task Refresh() => list.ItemsSource = await _database.GetRowsAsync("SELECT id, name, quantity, unit, unit_price, min_quantity FROM medicines ORDER BY name", r => $"{r.GetString(1)} · stock {r.GetInt32(2)} {r.GetString(3)} · prix {r.GetDecimal(4):0.00} · seuil {r.GetInt32(5)}");
        var save = ActionButton("Ajouter le médicament");
        save.Click += async (_, _) => await SafeRunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text) || !int.TryParse(quantity.Text, out var q) || !int.TryParse(threshold.Text, out var min) || !decimal.TryParse(price.Text, out var p) || q < 0 || min < 0 || p < 0)
            { SetStatus("Renseignez le nom, des quantités positives et un prix valide.", true); return; }
            await _database.ExecuteAsync("INSERT INTO medicines(name, quantity, unit, unit_price, min_quantity) VALUES($n,$q,$u,$p,$m)", ("$n", name.Text.Trim()), ("$q", q), ("$u", unit.Text ?? "unité"), ("$p", p), ("$m", min));
            name.Text = quantity.Text = threshold.Text = unit.Text = price.Text = ""; await Refresh(); SetStatus("Médicament ajouté.");
        });
        _content.Children.Add(Card(new StackPanel { Spacing = 12, Children = { Heading("Ajouter un médicament"), name, quantity, threshold, unit, price, save } }));
        _content.Children.Add(Card(new StackPanel { Spacing = 10, Children = { Heading("Stock"), list } }));
        await Refresh();
    }

    private async Task ShowBackupAsync()
    {
        BeginPage("Sauvegarde locale", "Créez une copie de la base SQLite sur cet appareil. Cette copie n'est pas chiffrée.");
        await _database.InitializeAsync();
        var folder = new TextBlock { Text = "Les sauvegardes sont créées dans le dossier Documents/AfriHospital-Backups.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#374151") };
        var save = ActionButton("Créer une sauvegarde maintenant");
        save.Click += async (_, _) => await SafeRunAsync(async () =>
        {
            var path = await _database.CreateBackupAsync();
            _content.Children.Add(Card(new TextBlock { Text = "Sauvegarde créée : " + path, TextWrapping = TextWrapping.Wrap, Foreground = Brush("#166534") }));
            SetStatus("Sauvegarde créée. Protégez ce fichier contre tout accès non autorisé.");
        });
        _content.Children.Add(Card(new StackPanel { Spacing = 12, Children =
        {
            Heading("Sauvegarde de la base de données"),
            folder,
            new Border { Background = Brush("#FFF7D6"), CornerRadius = new CornerRadius(8), Padding = new Thickness(12),
                Child = new TextBlock { Text = "Attention : la sauvegarde contient potentiellement des données sensibles et n'est pas chiffrée. Ne l'envoyez pas par courriel et ne l'utilisez pas avec de vrais dossiers médicaux avant mise en place du chiffrement et d'une politique de conservation.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#5B4300") } },
            save
        }}));
    }

    private async Task ShowInvoicesAsync()
    {
        BeginPage("Facturation", "Suivi simple des factures. Les devises et règles fiscales doivent être configurées pour chaque pays.");
        await _database.InitializeAsync();
        var patient = Field("Nom du patient"); var description = Field("Description"); var amount = Field("Montant"); var currency = Field("Devise (ex. XOF, XAF, CDF)");
        var list = new ListBox { MinHeight = 240 };
        async Task Refresh() => list.ItemsSource = await _database.GetRowsAsync("SELECT id, patient_name, description, amount, currency, status FROM invoices ORDER BY id DESC", r => $"Facture #{r.GetInt64(0)} · {r.GetString(1)} · {r.GetString(2)} · {r.GetDecimal(3):0.00} {r.GetString(4)} · {r.GetString(5)}");
        var save = ActionButton("Créer la facture");
        save.Click += async (_, _) => await SafeRunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(patient.Text) || !decimal.TryParse(amount.Text, out var a) || a < 0 || string.IsNullOrWhiteSpace(currency.Text))
            { SetStatus("Le patient, le montant positif et la devise sont obligatoires.", true); return; }
            await _database.ExecuteAsync("INSERT INTO invoices(patient_name, description, amount, currency, status) VALUES($p,$d,$a,$c,'À payer')", ("$p", patient.Text.Trim()), ("$d", description.Text ?? ""), ("$a", a), ("$c", currency.Text.Trim().ToUpperInvariant()));
            patient.Text = description.Text = amount.Text = currency.Text = ""; await Refresh(); SetStatus("Facture créée.");
        });
        _content.Children.Add(Card(new StackPanel { Spacing = 12, Children = { Heading("Nouvelle facture"), patient, description, amount, currency, save } }));
        _content.Children.Add(Card(new StackPanel { Spacing = 10, Children = { Heading("Factures"), list } }));
        await Refresh();
    }
}

internal sealed class HospitalDatabase
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AfriHospital", "afrihospital.db");
    private string ConnectionString => new SqliteConnectionStringBuilder { DataSource = _path, Mode = SqliteOpenMode.ReadWriteCreate }.ToString();
    private bool _initialized;

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = @"
PRAGMA foreign_keys = ON;
CREATE TABLE IF NOT EXISTS patients(id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, phone TEXT NOT NULL DEFAULT '', birth_date TEXT NOT NULL DEFAULT '', gender TEXT NOT NULL DEFAULT '', notes TEXT NOT NULL DEFAULT '', created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS appointments(id INTEGER PRIMARY KEY AUTOINCREMENT, patient_id INTEGER NULL REFERENCES patients(id), patient_name TEXT NOT NULL, scheduled_at TEXT NOT NULL, reason TEXT NOT NULL DEFAULT '', status TEXT NOT NULL DEFAULT 'Planifié', created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS consultations(id INTEGER PRIMARY KEY AUTOINCREMENT, patient_id INTEGER NULL REFERENCES patients(id), patient_name TEXT NOT NULL, consulted_at TEXT NOT NULL, clinician TEXT NOT NULL DEFAULT '', summary TEXT NOT NULL DEFAULT '', created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS medicines(id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, quantity INTEGER NOT NULL DEFAULT 0, unit TEXT NOT NULL DEFAULT 'unité', unit_price NUMERIC NOT NULL DEFAULT 0, min_quantity INTEGER NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS invoices(id INTEGER PRIMARY KEY AUTOINCREMENT, patient_name TEXT NOT NULL, description TEXT NOT NULL DEFAULT '', amount NUMERIC NOT NULL, currency TEXT NOT NULL, status TEXT NOT NULL DEFAULT 'À payer', created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS audit_log(id INTEGER PRIMARY KEY AUTOINCREMENT, action TEXT NOT NULL, entity TEXT NOT NULL, created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);";
        await command.ExecuteNonQueryAsync();
        await EnsureColumnAsync(connection, "appointments", "patient_id", "INTEGER NULL REFERENCES patients(id)");
        await EnsureColumnAsync(connection, "consultations", "patient_id", "INTEGER NULL REFERENCES patients(id)");
        await using (var migrate = connection.CreateCommand())
        {
            migrate.CommandText = @"UPDATE appointments SET patient_id = (SELECT p.id FROM patients p WHERE p.name = appointments.patient_name COLLATE NOCASE LIMIT 1) WHERE patient_id IS NULL;
UPDATE consultations SET patient_id = (SELECT p.id FROM patients p WHERE p.name = consultations.patient_name COLLATE NOCASE LIMIT 1) WHERE patient_id IS NULL;";
            await migrate.ExecuteNonQueryAsync();
        }
        _initialized = true;
    }

    private static async Task EnsureColumnAsync(SqliteConnection connection, string table, string column, string definition)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table})";
        await using var reader = await check.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader.GetString(1).Equals(column, StringComparison.OrdinalIgnoreCase))
                return;
        }
        await reader.DisposeAsync();
        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
        await alter.ExecuteNonQueryAsync();
    }

    public async Task<List<PatientRow>> GetPatientChoicesAsync()
    {
        var rows = new List<PatientRow>();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, phone, birth_date, gender, notes FROM patients ORDER BY name";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add(new PatientRow(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5)));
        return rows;
    }

    public async Task<Dictionary<string,long>> GetCountsAsync()
    {
        var result = new Dictionary<string,long>();
        result["patients"] = await ScalarAsync("SELECT COUNT(*) FROM patients");
        result["consultations"] = await ScalarAsync("SELECT COUNT(*) FROM consultations");
        result["appointments"] = await ScalarAsync("SELECT COUNT(*) FROM appointments");
        result["medicines"] = await ScalarAsync("SELECT COUNT(*) FROM medicines");
        result["invoices"] = await ScalarAsync("SELECT COUNT(*) FROM invoices");
        result["low_stock"] = await ScalarAsync("SELECT COUNT(*) FROM medicines WHERE quantity <= min_quantity");
        return result;
    }

    private async Task<long> ScalarAsync(string sql)
    {
        await using var connection = new SqliteConnection(ConnectionString); await connection.OpenAsync();
        var command = connection.CreateCommand(); command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    public async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqliteConnection(ConnectionString); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<string>> GetRowsAsync(string sql, Func<SqliteDataReader,string> map)
    {
        var rows = new List<string>();
        await using var connection = new SqliteConnection(ConnectionString); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) rows.Add(map(reader));
        return rows;
    }

    public async Task<string> CreateBackupAsync()
    {
        await InitializeAsync();
        await using (var connection = new SqliteConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA wal_checkpoint(FULL);";
            await command.ExecuteNonQueryAsync();
        }
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AfriHospital-Backups");
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, $"afrihospital-{DateTime.Now:yyyyMMdd-HHmmss}.db");
        File.Copy(_path, target, overwrite: false);
        return target;
    }

    public async Task<List<PatientRow>> SearchPatientsAsync(string search)
    {
        var rows = new List<PatientRow>();
        await using var connection = new SqliteConnection(ConnectionString); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, phone, birth_date, gender, notes FROM patients WHERE name LIKE $q OR phone LIKE $q ORDER BY name";
        command.Parameters.AddWithValue("$q", "%" + search + "%");
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add(new PatientRow(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5)));
        return rows;
    }

    public async Task SavePatientAsync(long? id, string name, string phone, string birthDate, string gender, string notes)
    {
        if (id is null)
            await ExecuteAsync("INSERT INTO patients(name, phone, birth_date, gender, notes) VALUES($n,$p,$b,$g,$notes)", ("$n", name), ("$p", phone), ("$b", birthDate), ("$g", gender), ("$notes", notes));
        else
            await ExecuteAsync("UPDATE patients SET name=$n, phone=$p, birth_date=$b, gender=$g, notes=$notes WHERE id=$id", ("$n", name), ("$p", phone), ("$b", birthDate), ("$g", gender), ("$notes", notes), ("$id", id.Value));
    }
}

internal sealed record PatientRow(long Id, string Name, string Phone, string BirthDate, string Gender, string Notes)
{
    public override string ToString() => $"#{Id} · {Name} · {Phone}";
}
