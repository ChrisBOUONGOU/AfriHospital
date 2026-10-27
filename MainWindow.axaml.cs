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
            var password = new TextBox { Watermark = "Mot de passe", PasswordChar = '●', MinWidth = 280, Padding = new Thickness(10) };
            var confirmation = new TextBox { Watermark = "Confirmer le mot de passe", PasswordChar = '●', MinWidth = 280, Padding = new Thickness(10), IsVisible = firstSetup };
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
                        if (password.Text != confirmation.Text)
                        {
                            message.Text = "Les mots de passe ne correspondent pas.";
                            return;
                        }
                        _currentUser = await _authentication.CreateInitialAdministratorAsync(username.Text ?? "", password.Text ?? "");
                    }
                    else
                    {
                        _currentUser = await _authentication.AuthenticateAsync(username.Text ?? "", password.Text ?? "");
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
        var password = new TextBox { Watermark = "Mot de passe (12 caractères minimum)", PasswordChar = '●', MinWidth = 260, Padding = new Thickness(10) };
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
            if (string.IsNullOrWhiteSpace(username.Text) || string.IsNullOrWhiteSpace(password.Text) || role.SelectedItem is not string selectedRole)
            {
                SetStatus("Renseignez le nom d'utilisateur, le mot de passe et le rôle.", true);
                return;
            }
            await _authentication.CreateUserAsync(username.Text.Trim(), password.Text, selectedRole, createdBy: _currentUser?.Username);
            username.Text = "";
            password.Text = "";
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
        if (!CanAccess("Tableau de bord")) { BeginPage("Accès refusé", "Veuillez vous connecter."); return; }
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
        if (!CanAccess("Patients")) { BeginPage("Accès refusé", "Votre rôle ne permet pas de gérer les patients."); return; }
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
            if (string.IsNullOrWhiteSpace(name.Text) || name.Text.Trim().Length > 150)
            { SetStatus("Le nom est obligatoire et ne doit pas dépasser 150 caractères.", true); return; }
            if (!string.IsNullOrWhiteSpace(phone.Text) && phone.Text.Trim().Length > 30)
            { SetStatus("Le numéro de téléphone ne doit pas dépasser 30 caractères.", true); return; }
            if (!string.IsNullOrWhiteSpace(birth.Text) &&
                (!DateOnly.TryParseExact(birth.Text.Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var birthDate) ||
                 birthDate > DateOnly.FromDateTime(DateTime.Today)))
            { SetStatus("Date de naissance invalide. Utilisez AAAA-MM-JJ et une date non future.", true); return; }
            await _database.SavePatientAsync(selectedId, name.Text.Trim(), phone.Text ?? "", birth.Text ?? "", gender.Text ?? "", notes.Text ?? "");
            await _database.WriteAuditAsync(selectedId is null ? "PATIENT_CREATED" : "PATIENT_UPDATED", "patient");
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
        if (!CanAccess("Rendez-vous"))
        {
            BeginPage("Accès refusé", "Votre rôle ne permet pas de gérer les rendez-vous.");
            return;
        }

        BeginPage("Rendez-vous", "Planification, modification, suivi du statut et annulation des rendez-vous.");
        await _database.InitializeAsync();
        var patients = new ComboBox { MinWidth = 260, PlaceholderText = "Sélectionner un patient" };
        patients.ItemsSource = await _database.GetPatientChoicesAsync();
        var date = Field("Date et heure (AAAA-MM-JJ HH:MM)");
        var reason = Field("Motif du rendez-vous (250 caractères maximum)");
        var status = new ComboBox { MinWidth = 260, ItemsSource = new[] { "Planifié", "Confirmé", "Arrivé", "Terminé", "Annulé" }, SelectedIndex = 0 };
        var list = new ListBox { MinHeight = 240 };
        long? selectedId = null;

        async Task Refresh()
        {
            list.ItemsSource = await _database.GetAppointmentsAsync();
        }

        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is not AppointmentRow appointment) return;
            selectedId = appointment.Id;
            date.Text = appointment.ScheduledAt;
            reason.Text = appointment.Reason;
            status.SelectedItem = appointment.Status;
            if (patients.ItemsSource is IEnumerable<PatientRow> patientRows)
                patients.SelectedItem = patientRows.FirstOrDefault(p => p.Id == appointment.PatientId);
        };

        bool TryReadAppointment(out PatientRow selectedPatient, out string formattedDate)
        {
            selectedPatient = patients.SelectedItem as PatientRow ?? null!;
            formattedDate = "";
            if (selectedPatient is null || string.IsNullOrWhiteSpace(date.Text))
            {
                SetStatus("Sélectionnez un patient et indiquez la date.", true);
                return false;
            }
            if (!DateTime.TryParseExact(date.Text.Trim(), new[] { "yyyy-MM-dd HH:mm", "yyyy-MM-dd H:mm" },
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var scheduledAt))
            {
                SetStatus("Date invalide. Format attendu : AAAA-MM-JJ HH:MM.", true);
                return false;
            }
            if ((reason.Text?.Trim().Length ?? 0) > 250)
            {
                SetStatus("Le motif ne doit pas dépasser 250 caractères.", true);
                return false;
            }
            formattedDate = scheduledAt.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        var save = ActionButton("Ajouter le rendez-vous");
        save.Click += async (_, _) => await SafeRunAsync(async () =>
        {
            if (!TryReadAppointment(out var selectedPatient, out var formattedDate)) return;
            if (await _database.HasAppointmentConflictAsync(selectedPatient.Id, formattedDate))
            {
                SetStatus("Ce patient possède déjà un rendez-vous actif à cette date et heure.", true);
                return;
            }
            await _database.ExecuteAsync(
                "INSERT INTO appointments(patient_id, patient_name, scheduled_at, reason, status) VALUES($id,$p,$d,$r,'Planifié')",
                ("$id", selectedPatient.Id), ("$p", selectedPatient.Name), ("$d", formattedDate), ("$r", reason.Text?.Trim() ?? ""));
            await _database.WriteAuditAsync("APPOINTMENT_CREATED", "appointment");
            selectedId = null; patients.SelectedItem = null; date.Text = reason.Text = ""; status.SelectedIndex = 0;
            await Refresh();
            SetStatus("Rendez-vous ajouté.");
        });

        var update = ActionButton("Enregistrer les modifications", false);
        update.Click += async (_, _) => await SafeRunAsync(async () =>
        {
            if (selectedId is null)
            {
                SetStatus("Sélectionnez un rendez-vous dans la liste avant de le modifier.", true);
                return;
            }
            if (!TryReadAppointment(out var selectedPatient, out var formattedDate)) return;
            if (await _database.HasAppointmentConflictAsync(selectedPatient.Id, formattedDate, selectedId))
            {
                SetStatus("Un autre rendez-vous actif existe déjà pour ce patient à cette date et heure.", true);
                return;
            }
            var selectedStatus = status.SelectedItem as string ?? "Planifié";
            await _database.ExecuteAsync(
                "UPDATE appointments SET patient_id=$patientId, patient_name=$patientName, scheduled_at=$date, reason=$reason, status=$status WHERE id=$id",
                ("$patientId", selectedPatient.Id), ("$patientName", selectedPatient.Name), ("$date", formattedDate),
                ("$reason", reason.Text?.Trim() ?? ""), ("$status", selectedStatus), ("$id", selectedId.Value));
            await _database.WriteAuditAsync("APPOINTMENT_UPDATED", "appointment");
            await Refresh();
            SetStatus("Rendez-vous modifié.");
        });

        var cancel = ActionButton("Annuler le rendez-vous", false);
        cancel.Click += async (_, _) => await SafeRunAsync(async () =>
        {
            if (selectedId is null)
            {
                SetStatus("Sélectionnez un rendez-vous à annuler.", true);
                return;
            }
            await _database.ExecuteAsync("UPDATE appointments SET status='Annulé' WHERE id=$id AND status <> 'Annulé'", ("$id", selectedId.Value));
            await _database.WriteAuditAsync("APPOINTMENT_CANCELLED", "appointment");
            await Refresh();
            SetStatus("Rendez-vous annulé. Il reste dans l'historique.");
        });

        var clear = ActionButton("Nouveau / Effacer", false);
        clear.Click += (_, _) =>
        {
            selectedId = null; patients.SelectedItem = null; date.Text = reason.Text = "";
            status.SelectedIndex = 0; list.SelectedItem = null;
        };

        _content.Children.Add(Card(new StackPanel { Spacing = 12, Children =
        {
            Heading("Rendez-vous"), patients, date, reason, status,
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { save, update } },
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { cancel, clear } }
        }}));
        _content.Children.Add(Card(new StackPanel { Spacing = 10, Children = { Heading("Rendez-vous enregistrés"), list } }));
        await Refresh();
    }

    private async Task ShowConsultationsAsync()
    {
        if (!CanAccess("Consultations")) { BeginPage("Accès refusé", "Votre rôle ne permet pas de gérer les consultations."); return; }
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
            if (string.IsNullOrWhiteSpace(clinician.Text) || string.IsNullOrWhiteSpace(summary.Text))
            { SetStatus("Le professionnel de santé et le résumé sont obligatoires.", true); return; }
            await _database.ExecuteAsync("INSERT INTO consultations(patient_id, patient_name, consulted_at, clinician, summary) VALUES($id,$p,$d,$c,$s)", ("$id", selectedPatient.Id), ("$p", selectedPatient.Name), ("$d", consultedAt.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)), ("$c", clinician.Text.Trim()), ("$s", summary.Text.Trim()));
            await _database.WriteAuditAsync("CONSULTATION_CREATED", "consultation");
            patients.SelectedItem = null; date.Text = clinician.Text = summary.Text = ""; await Refresh(); SetStatus("Consultation enregistrée.");
        });
        _content.Children.Add(Card(new StackPanel { Spacing = 12, Children = { Heading("Nouvelle consultation"), patients, date, clinician, summary, save } }));
        _content.Children.Add(Card(new StackPanel { Spacing = 10, Children = { Heading("Historique des consultations"), list } }));
        await Refresh();
    }

    private async Task ShowMedicinesAsync()
    {
        if (!CanAccess("Pharmacie / Stocks")) { BeginPage("Accès refusé", "Votre rôle ne permet pas de gérer la pharmacie."); return; }
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
            if (name.Text.Trim().Length > 150 || (unit.Text?.Trim().Length ?? 0) > 40)
            { SetStatus("Le nom du médicament ou l'unité est trop long.", true); return; }
            await _database.ExecuteAsync("INSERT INTO medicines(name, quantity, unit, unit_price, min_quantity) VALUES($n,$q,$u,$p,$m)", ("$n", name.Text.Trim()), ("$q", q), ("$u", string.IsNullOrWhiteSpace(unit.Text) ? "unité" : unit.Text.Trim()), ("$p", p), ("$m", min));
            await _database.WriteAuditAsync("MEDICINE_CREATED", "medicine");
            name.Text = quantity.Text = threshold.Text = unit.Text = price.Text = ""; await Refresh(); SetStatus("Médicament ajouté.");
        });
        _content.Children.Add(Card(new StackPanel { Spacing = 12, Children = { Heading("Ajouter un médicament"), name, quantity, threshold, unit, price, save } }));
        _content.Children.Add(Card(new StackPanel { Spacing = 10, Children = { Heading("Stock"), list } }));
        await Refresh();
    }

    private async Task ShowBackupAsync()
    {
        if (!CanAccess("Sauvegarde"))
        {
            BeginPage("Accès refusé", "Seul un administrateur peut sauvegarder ou restaurer la base.");
            return;
        }

        BeginPage("Sauvegarde locale", "Créez une copie ou restaurez une sauvegarde SQLite. Une sauvegarde contient des données sensibles.");
        await _database.InitializeAsync();
        var folder = new TextBlock { Text = "Les sauvegardes sont créées dans Documents/AfriHospital-Backups.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#374151") };
        var save = ActionButton("Créer une sauvegarde maintenant");
        save.Click += async (_, _) => await SafeRunAsync(async () =>
        {
            var path = await _database.CreateBackupAsync();
            _content.Children.Add(Card(new TextBlock { Text = "Sauvegarde créée : " + path, TextWrapping = TextWrapping.Wrap, Foreground = Brush("#166534") }));
            SetStatus("Sauvegarde créée. Protégez ce fichier contre tout accès non autorisé.");
        });

        var restorePath = Field("Chemin complet vers un fichier de sauvegarde .db");
        var restore = ActionButton("Valider et restaurer cette sauvegarde", false);
        restore.Click += async (_, _) => await SafeRunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(restorePath.Text))
            {
                SetStatus("Indiquez le chemin du fichier de sauvegarde.", true);
                return;
            }
            var safetyCopy = await _database.RestoreBackupAsync(restorePath.Text.Trim());
            await ShowDashboardAsync();
            SetStatus("Restauration terminée. Une copie de sécurité de l'ancienne base a été créée : " + safetyCopy);
        });

        _content.Children.Add(Card(new StackPanel { Spacing = 12, Children =
        {
            Heading("Créer une sauvegarde"),
            folder,
            new Border { Background = Brush("#FFF7D6"), CornerRadius = new CornerRadius(8), Padding = new Thickness(12),
                Child = new TextBlock { Text = "Les sauvegardes ne sont pas chiffrées. Ne les transmettez pas et n'utilisez pas de vrais dossiers médicaux avant la mise en place des protections nécessaires.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#5B4300") } },
            save
        }}));
        _content.Children.Add(Card(new StackPanel { Spacing = 12, Children =
        {
            Heading("Restaurer une sauvegarde"),
            new TextBlock { Text = "La base choisie sera vérifiée avant restauration. L'application créera d'abord une copie de sécurité de la base actuelle.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#374151") },
            restorePath, restore
        }}));
    }

    private async Task ShowInvoicesAsync()
    {
        if (!CanAccess("Facturation")) { BeginPage("Accès refusé", "Votre rôle ne permet pas de gérer la facturation."); return; }
        BeginPage("Facturation", "Suivi simple des factures et enregistrement des paiements. Les règles fiscales doivent être configurées pour chaque pays.");
        await _database.InitializeAsync();
        var patient = Field("Nom du patient");
        var description = Field("Description");
        var amount = Field("Montant total");
        var currency = Field("Devise (ex. XOF, XAF, CDF)");
        var list = new ListBox { MinHeight = 220 };
        var invoiceChoice = new ComboBox { MinWidth = 260, PlaceholderText = "Choisir une facture à régler" };
        var paymentAmount = Field("Montant du paiement");
        var paymentMethod = new ComboBox { MinWidth = 260, ItemsSource = new[] { "Espèces", "Carte", "Virement", "Mobile money" }, SelectedIndex = 0 };

        async Task Refresh()
        {
            list.ItemsSource = await _database.GetRowsAsync(
                "SELECT i.id, i.patient_name, i.description, i.amount, i.currency, i.status, COALESCE(SUM(p.amount),0) FROM invoices i LEFT JOIN payments p ON p.invoice_id=i.id GROUP BY i.id ORDER BY i.id DESC",
                r => $"Facture #{r.GetInt64(0)} · {r.GetString(1)} · {r.GetString(2)} · Total {r.GetDecimal(3):0.00} {r.GetString(4)} · Payé {r.GetDecimal(6):0.00} · {r.GetString(5)}");
            invoiceChoice.ItemsSource = await _database.GetInvoiceChoicesAsync();
        }

        var save = ActionButton("Créer la facture");
        save.Click += async (_, _) => await SafeRunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(patient.Text) ||
                !decimal.TryParse(amount.Text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var a) ||
                a <= 0 || string.IsNullOrWhiteSpace(currency.Text))
            { SetStatus("Le patient, un montant positif et la devise sont obligatoires. Utilisez le point pour les décimales.", true); return; }
            var currencyCode = currency.Text.Trim().ToUpperInvariant();
            if (currencyCode.Length != 3 || currencyCode.Any(ch => ch < 'A' || ch > 'Z'))
            { SetStatus("La devise doit être un code de trois lettres, par exemple XOF, XAF ou CDF.", true); return; }
            if (patient.Text.Trim().Length > 150 || (description.Text?.Length ?? 0) > 500)
            { SetStatus("Le nom du patient ou la description est trop long.", true); return; }
            await _database.ExecuteAsync("INSERT INTO invoices(patient_name, description, amount, currency, status) VALUES($p,$d,$a,$c,'À payer')", ("$p", patient.Text.Trim()), ("$d", description.Text ?? ""), ("$a", a), ("$c", currencyCode));
            await _database.WriteAuditAsync("INVOICE_CREATED", "invoice");
            patient.Text = description.Text = amount.Text = currency.Text = "";
            await Refresh();
            SetStatus("Facture créée.");
        });

        var pay = ActionButton("Enregistrer le paiement", false);
        pay.Click += async (_, _) => await SafeRunAsync(async () =>
        {
            if (invoiceChoice.SelectedItem is not InvoiceChoice selected ||
                !decimal.TryParse(paymentAmount.Text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var paid) ||
                paid <= 0 || paymentMethod.SelectedItem is not string method)
            { SetStatus("Sélectionnez une facture, un montant positif et un moyen de paiement.", true); return; }
            var paymentId = await _database.RecordPaymentAsync(selected.Id, paid, method);
            invoiceChoice.SelectedItem = null;
            paymentAmount.Text = "";
            await Refresh();
            try
            {
                var receiptPath = await _database.CreateReceiptAsync(paymentId);
                SetStatus("Paiement enregistré. Reçu créé : " + receiptPath);
            }
            catch (Exception receiptError)
            {
                SetStatus("Paiement enregistré, mais le reçu n'a pas pu être créé : " + receiptError.Message, true);
            }
        });

        _content.Children.Add(Card(new StackPanel { Spacing = 12, Children = { Heading("Nouvelle facture"), patient, description, amount, currency, save } }));
        _content.Children.Add(Card(new StackPanel { Spacing = 10, Children = { Heading("Enregistrer un paiement"), invoiceChoice, paymentAmount, paymentMethod, pay } }));
        _content.Children.Add(Card(new StackPanel { Spacing = 10, Children = { Heading("Factures et montants encaissés"), list } }));
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
CREATE TABLE IF NOT EXISTS payments(id INTEGER PRIMARY KEY AUTOINCREMENT, invoice_id INTEGER NOT NULL REFERENCES invoices(id), amount NUMERIC NOT NULL CHECK(amount > 0), method TEXT NOT NULL, paid_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE INDEX IF NOT EXISTS ix_payments_invoice ON payments(invoice_id);
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

    public async Task<bool> HasAppointmentConflictAsync(long patientId, string scheduledAt, long? excludingAppointmentId = null)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS(
                SELECT 1 FROM appointments
                WHERE patient_id=$patientId AND scheduled_at=$scheduledAt
                  AND status <> 'Annulé'
                  AND ($excludeId IS NULL OR id <> $excludeId)
            );
            """;
        command.Parameters.AddWithValue("$patientId", patientId);
        command.Parameters.AddWithValue("$scheduledAt", scheduledAt);
        command.Parameters.AddWithValue("$excludeId", (object?)excludingAppointmentId ?? DBNull.Value);
        return Convert.ToInt64(await command.ExecuteScalarAsync()) != 0;
    }

    public async Task<List<AppointmentRow>> GetAppointmentsAsync()
    {
        var rows = new List<AppointmentRow>();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT a.id, COALESCE(a.patient_id, 0), COALESCE(p.name, a.patient_name),
                   a.scheduled_at, a.reason, a.status
            FROM appointments a LEFT JOIN patients p ON p.id=a.patient_id
            ORDER BY a.scheduled_at DESC, a.id DESC;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add(new AppointmentRow(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4), reader.GetString(5)));
        return rows;
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

    public async Task<List<InvoiceChoice>> GetInvoiceChoicesAsync()
    {
        var invoices = new List<InvoiceChoice>();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT i.id, i.patient_name, i.description, i.amount, i.currency, COALESCE(SUM(p.amount), 0)
            FROM invoices i LEFT JOIN payments p ON p.invoice_id=i.id
            GROUP BY i.id HAVING i.amount > COALESCE(SUM(p.amount), 0)
            ORDER BY i.id DESC;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            invoices.Add(new InvoiceChoice(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), reader.GetString(4), reader.GetDecimal(5)));
        return invoices;
    }

    public async Task<long> RecordPaymentAsync(long invoiceId, decimal amount, string method)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Le paiement doit être positif.");
        if (string.IsNullOrWhiteSpace(method)) throw new ArgumentException("Le moyen de paiement est obligatoire.", nameof(method));

        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        decimal total;
        decimal alreadyPaid;
        await using (var invoice = connection.CreateCommand())
        {
            invoice.Transaction = transaction;
            invoice.CommandText = "SELECT amount FROM invoices WHERE id=$id;";
            invoice.Parameters.AddWithValue("$id", invoiceId);
            var result = await invoice.ExecuteScalarAsync();
            if (result is null || result is DBNull) throw new KeyNotFoundException("Facture introuvable.");
            total = Convert.ToDecimal(result, System.Globalization.CultureInfo.InvariantCulture);
        }
        await using (var sum = connection.CreateCommand())
        {
            sum.Transaction = transaction;
            sum.CommandText = "SELECT COALESCE(SUM(amount),0) FROM payments WHERE invoice_id=$id;";
            sum.Parameters.AddWithValue("$id", invoiceId);
            alreadyPaid = Convert.ToDecimal(await sum.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        }
        if (amount > total - alreadyPaid)
            throw new InvalidOperationException("Le paiement dépasse le solde restant de la facture.");

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO payments(invoice_id, amount, method) VALUES($id,$amount,$method);";
            insert.Parameters.AddWithValue("$id", invoiceId);
            insert.Parameters.AddWithValue("$amount", amount);
            insert.Parameters.AddWithValue("$method", method.Trim());
            await insert.ExecuteNonQueryAsync();
        }
        long paymentId;
        await using (var lastId = connection.CreateCommand())
        {
            lastId.Transaction = transaction;
            lastId.CommandText = "SELECT last_insert_rowid();";
            paymentId = Convert.ToInt64(await lastId.ExecuteScalarAsync());
        }
        var newStatus = alreadyPaid + amount >= total ? "Payée" : "Partiellement payée";
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = "UPDATE invoices SET status=$status WHERE id=$id;";
            update.Parameters.AddWithValue("$status", newStatus);
            update.Parameters.AddWithValue("$id", invoiceId);
            await update.ExecuteNonQueryAsync();
        }
        await using (var audit = connection.CreateCommand())
        {
            audit.Transaction = transaction;
            audit.CommandText = "INSERT INTO audit_log(action, entity) VALUES('PAYMENT_RECORDED','invoice');";
            await audit.ExecuteNonQueryAsync();
        }
        transaction.Commit();
        return paymentId;
    }

    public async Task<string> CreateReceiptAsync(long paymentId)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.id, p.invoice_id, i.patient_name, i.description, p.amount, i.currency, p.method, p.paid_at
            FROM payments p JOIN invoices i ON i.id=p.invoice_id WHERE p.id=$id;
            """;
        command.Parameters.AddWithValue("$id", paymentId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new KeyNotFoundException("Paiement introuvable pour générer le reçu.");
        var receipt = string.Join(Environment.NewLine, new[]
        {
            "AFRIHOSPITAL — REÇU DE PAIEMENT",
            "================================",
            $"Reçu n° : {reader.GetInt64(0)}",
            $"Facture n° : {reader.GetInt64(1)}",
            $"Patient : {reader.GetString(2)}",
            $"Description : {reader.GetString(3)}",
            $"Montant reçu : {reader.GetDecimal(4).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} {reader.GetString(5)}",
            $"Moyen de paiement : {reader.GetString(6)}",
            $"Date : {reader.GetString(7)}",
            "",
            "Document généré localement. Conservez-le de manière sécurisée."
        });
        await reader.DisposeAsync();
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AfriHospital-Receipts");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"receipt-{paymentId}-{DateTime.UtcNow:yyyyMMddHHmmss}.txt");
        await File.WriteAllTextAsync(path, receipt, new System.Text.UTF8Encoding(false));
        return path;
    }

    public async Task<bool> ExistsAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return Convert.ToInt64(await command.ExecuteScalarAsync()) != 0;
    }

    public async Task WriteAuditAsync(string action, string entity)
    {
        await ExecuteAsync("INSERT INTO audit_log(action, entity) VALUES($action, $entity)", ("$action", action), ("$entity", entity));
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

    public async Task<string> RestoreBackupAsync(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Le chemin de sauvegarde est obligatoire.", nameof(sourcePath));
        sourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Le fichier de sauvegarde est introuvable.", sourcePath);
        if (string.Equals(sourcePath, Path.GetFullPath(_path), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Choisissez un fichier de sauvegarde différent de la base active.");

        var sourceBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = sourcePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        await using (var source = new SqliteConnection(sourceBuilder.ToString()))
        {
            await source.OpenAsync();
            await using var integrity = source.CreateCommand();
            integrity.CommandText = "PRAGMA integrity_check;";
            var result = Convert.ToString(await integrity.ExecuteScalarAsync());
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("La sauvegarde SQLite est invalide ou endommagée.");

            await using var tables = source.CreateCommand();
            tables.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='patients';";
            if (Convert.ToInt32(await tables.ExecuteScalarAsync()) != 1)
                throw new InvalidDataException("Ce fichier ne contient pas le schéma AfriHospital attendu.");
        }

        var safetyCopy = await CreateBackupAsync();
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var sidecar = _path + suffix;
            if (File.Exists(sidecar)) File.Delete(sidecar);
        }
        File.Copy(sourcePath, _path, overwrite: true);
        _initialized = false;
        await InitializeAsync();
        return safetyCopy;
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

internal sealed record AppointmentRow(long Id, long PatientId, string PatientName, string ScheduledAt, string Reason, string Status)
{
    public override string ToString() => $"#{Id} · {PatientName} · {ScheduledAt} · {Reason} · {Status}";
}

internal sealed record PatientRow(long Id, string Name, string Phone, string BirthDate, string Gender, string Notes)
{
    public override string ToString() => $"#{Id} · {Name} · {Phone}";
}

internal sealed record InvoiceChoice(long Id, string PatientName, string Description, decimal Total, string Currency, decimal Paid)
{
    public override string ToString() => $"Facture #{Id} · {PatientName} · Solde {(Total - Paid):0.00} {Currency}";
}
