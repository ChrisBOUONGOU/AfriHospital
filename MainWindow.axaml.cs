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
        if (CanAccess("Audit")) AddNav(nav, "Journal d'audit", async () => await ShowAuditAsync());
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

    private async Task ShowAuditAsync()
    {
        if (!CanAccess("Audit"))
        {
            BeginPage("Accès refusé", "Seul un administrateur peut consulter le journal d'audit.");
            return;
        }

        BeginPage("Journal d'audit", "Historique local des opérations métier et des événements de sécurité. Les informations médicales détaillées ne sont pas copiées dans ce journal.");
        await _database.InitializeAsync();
        var list = new ListBox { MinHeight = 420 };
        list.ItemsSource = await _database.GetRowsAsync("""
            SELECT created_at, actor, action, entity, details
            FROM audit_log
            UNION ALL
            SELECT created_at_utc, actor, action, 'security', details
            FROM security_audit_log
            ORDER BY 1 DESC
            LIMIT 500;
            """,
            r => $"{r.GetString(0)} · {r.GetString(1)} · {r.GetString(2)} · {r.GetString(3)} · {r.GetString(4)}");
        _content.Children.Add(Card(new StackPanel { Spacing = 10, Children =
        {
            Heading("Dernières opérations"), list,
            new TextBlock { Text = "Le journal est local. Il ne remplace pas une stratégie de conservation, de sauvegarde et de protection contre la falsification.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#6B7280") }
        }}));
    }

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
            await _database.WriteAuditAsync(selectedId is null ? "PATIENT_CREATED" : "PATIENT_UPDATED", "patient", _currentUser?.Username);
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
            await _database.WriteAuditAsync("APPOINTMENT_CREATED", "appointment", _currentUser?.Username);
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
            await _database.WriteAuditAsync("APPOINTMENT_UPDATED", "appointment", _currentUser?.Username);
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
            await _database.WriteAuditAsync("APPOINTMENT_CANCELLED", "appointment", _currentUser?.Username);
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
            await _database.WriteAuditAsync("CONSULTATION_CREATED", "consultation", _currentUser?.Username);
            patients.SelectedItem = null; date.Text = clinician.Text = summary.Text = ""; await Refresh(); SetStatus("Consultation enregistrée.");
        });
        _content.Children.Add(Card(new StackPanel { Spacing = 12, Children = { Heading("Nouvelle consultation"), patients, date, clinician, summary, save } }));
        _content.Children.Add(Card(new StackPanel { Spacing = 10, Children = { Heading("Historique des consultations"), list } }));
        await Refresh();
    }

    private async Task ShowMedicinesAsync()
    {
        if (!CanAccess("Pharmacie / Stocks")) { BeginPage("Accès refusé", "Votre rôle ne permet pas de gérer la pharmacie."); return; }
        BeginPage("Pharmacie et stocks", "Suivi des lots, entrées/sorties, fournisseurs et péremptions. Les lots anciens sans date connue sont signalés comme inconnus.");
        await _database.InitializeAsync();

        var name = Field("Nom du médicament");
        var quantity = Field("Stock initial (entier positif ou nul)");
        var threshold = Field("Seuil d’alerte");
        var unit = Field("Unité (boîte, flacon…)");
        var price = Field("Prix unitaire de vente (ex. 2.50)");
        var medicines = new ComboBox { MinWidth = 280, PlaceholderText = "Sélectionner un médicament" };
        var operation = new ComboBox { MinWidth = 240, ItemsSource = new[] { "Entrée de stock", "Sortie de stock" }, SelectedIndex = 0 };
        var batchNumber = Field("Numéro de lot (obligatoire pour une entrée)");
        var expiry = Field("Date de péremption AAAA-MM-JJ (entrée)");
        var supplier = Field("Fournisseur (entrée)");
        var movementQuantity = Field("Quantité du mouvement");
        var unitCost = Field("Coût unitaire d’achat (ex. 1.20)");
        var note = Field("Note / motif (facultatif)");
        var batches = new ComboBox { MinWidth = 280, PlaceholderText = "Choisir un lot pour la sortie", IsVisible = false };
        batchNumber.IsVisible = expiry.IsVisible = supplier.IsVisible = unitCost.IsVisible = true;
        var list = new ListBox { MinHeight = 240 };
        var movementList = new ListBox { MinHeight = 180 };

        async Task RefreshMedicines(long? keepSelected = null)
        {
            var choices = await _database.GetMedicineChoicesAsync();
            medicines.ItemsSource = choices;
            if (keepSelected.HasValue)
                medicines.SelectedItem = choices.FirstOrDefault(m => m.Id == keepSelected.Value);
            else if (choices.Count > 0 && medicines.SelectedItem is null)
                medicines.SelectedIndex = 0;
            await RefreshStockList();
            await RefreshBatches();
        }

        async Task RefreshStockList()
        {
            list.ItemsSource = await _database.GetRowsAsync("""
                SELECT m.name, m.quantity, m.unit, m.unit_price, m.min_quantity,
                       (SELECT COUNT(*) FROM medicine_batches b WHERE b.medicine_id=m.id),
                       (SELECT COUNT(*) FROM medicine_batches b WHERE b.medicine_id=m.id
                         AND b.expiry_date IS NOT NULL AND b.expiry_date < $today AND b.quantity > 0)
                FROM medicines m ORDER BY m.name
                """, r => $"{r.GetString(0)} · stock {r.GetInt32(1)} {r.GetString(2)} · prix {r.GetDecimal(3):0.00} · seuil {r.GetInt32(4)} · lots {r.GetInt32(5)} · lots périmés {r.GetInt32(6)}",
                ("$today", DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)));
            movementList.ItemsSource = await _database.GetStockMovementRowsAsync();
        }

        async Task RefreshBatches()
        {
            if (medicines.SelectedItem is not MedicineChoice selected)
            {
                batches.ItemsSource = Array.Empty<MedicineBatchChoice>();
                return;
            }
            var items = await _database.GetBatchChoicesAsync(selected.Id);
            batches.ItemsSource = items;
            if (items.Count > 0) batches.SelectedIndex = 0;
        }

        medicines.SelectionChanged += async (_, _) => await SafeRunAsync(RefreshBatches);
        operation.SelectionChanged += (_, _) =>
        {
            var isInbound = operation.SelectedItem as string == "Entrée de stock";
            batchNumber.IsVisible = expiry.IsVisible = supplier.IsVisible = unitCost.IsVisible = isInbound;
            batches.IsVisible = !isInbound;
        };

        var create = ActionButton("Ajouter le médicament");
        create.Click += async (_, _) => await SafeRunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text) ||
                !int.TryParse(quantity.Text, out var initialQuantity) ||
                !int.TryParse(threshold.Text, out var min) ||
                !decimal.TryParse(price.Text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var sellPrice) ||
                initialQuantity < 0 || min < 0 || sellPrice < 0)
            { SetStatus("Renseignez le nom, des quantités positives et un prix valide (point décimal).", true); return; }
            if (name.Text.Trim().Length > 150 || (unit.Text?.Trim().Length ?? 0) > 40)
            { SetStatus("Le nom du médicament ou l'unité est trop long.", true); return; }
            var newId = await _database.AddMedicineAsync(name.Text.Trim(), initialQuantity,
                string.IsNullOrWhiteSpace(unit.Text) ? "unité" : unit.Text.Trim(), sellPrice, min);
            await _database.WriteAuditAsync("MEDICINE_CREATED", "medicine", _currentUser?.Username);
            name.Text = quantity.Text = threshold.Text = unit.Text = price.Text = "";
            await RefreshMedicines(newId);
            SetStatus(initialQuantity > 0
                ? "Médicament ajouté. Le stock initial a été conservé dans un lot historique sans date de péremption connue."
                : "Médicament ajouté.");
        });

        var recordMovement = ActionButton("Enregistrer le mouvement", false);
        recordMovement.Click += async (_, _) => await SafeRunAsync(async () =>
        {
            if (medicines.SelectedItem is not MedicineChoice selected ||
                !int.TryParse(movementQuantity.Text, out var movedQuantity) || movedQuantity <= 0)
            { SetStatus("Sélectionnez un médicament et une quantité entière positive.", true); return; }
            if ((note.Text?.Trim().Length ?? 0) > 250)
            { SetStatus("La note ne doit pas dépasser 250 caractères.", true); return; }

            var inbound = operation.SelectedItem as string == "Entrée de stock";
            string? batch = null;
            string? expiryDate = null;
            string? supplierName = null;
            decimal cost = 0;
            long? batchId = null;
            if (inbound)
            {
                if (string.IsNullOrWhiteSpace(batchNumber.Text) || batchNumber.Text.Trim().Length > 100 ||
                    string.IsNullOrWhiteSpace(expiry.Text) ||
                    !DateOnly.TryParseExact(expiry.Text.Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedExpiry) ||
                    parsedExpiry < DateOnly.FromDateTime(DateTime.Today))
                { SetStatus("Pour une entrée, indiquez un numéro de lot (100 caractères maximum) et une péremption valide non passée (AAAA-MM-JJ).", true); return; }
                if ((supplier.Text?.Trim().Length ?? 0) > 150 ||
                    !decimal.TryParse(unitCost.Text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out cost) || cost < 0)
                { SetStatus("Le fournisseur est limité à 150 caractères et le coût doit être un nombre positif ou nul (point décimal).", true); return; }
                batch = batchNumber.Text.Trim();
                expiryDate = parsedExpiry.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                supplierName = supplier.Text?.Trim() ?? "";
            }
            else
            {
                if (batches.SelectedItem is not MedicineBatchChoice selectedBatch)
                { SetStatus("Sélectionnez le lot dont vous souhaitez retirer du stock.", true); return; }
                batchId = selectedBatch.Id;
            }

            await _database.RecordStockMovementAsync(selected.Id, inbound, movedQuantity, batch, expiryDate,
                supplierName, cost, batchId, note.Text?.Trim() ?? "", _currentUser?.Username);
            movementQuantity.Text = note.Text = "";
            batchNumber.Text = expiry.Text = supplier.Text = unitCost.Text = "";
            await RefreshMedicines(selected.Id);
            SetStatus(inbound ? "Entrée de stock enregistrée." : "Sortie de stock enregistrée.");
        });

        _content.Children.Add(Card(new StackPanel { Spacing = 12, Children =
        {
            Heading("Ajouter un médicament"), name, quantity, threshold, unit, price, create,
            new TextBlock { Text = "Un stock initial déjà connu est conservé, mais sa date de péremption reste inconnue jusqu'à un inventaire physique.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#6B7280") }
        }}));
        _content.Children.Add(Card(new StackPanel { Spacing = 12, Children =
        {
            Heading("Entrées et sorties de stock"), medicines, operation, batchNumber, expiry, supplier, batches,
            movementQuantity, unitCost, note, recordMovement
        }}));
        _content.Children.Add(Card(new StackPanel { Spacing = 10, Children = { Heading("Stock, seuils et lots périmés"), list } }));
        _content.Children.Add(Card(new StackPanel { Spacing = 10, Children = { Heading("Historique des mouvements de stock"), movementList } }));
        operation.SelectedIndex = 0;
        await RefreshMedicines();
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
            await _database.WriteAuditAsync("DATABASE_BACKUP_CREATED", "database", _currentUser?.Username, "Local SQLite backup created.");
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
            await _database.WriteAuditAsync("DATABASE_RESTORED", "database", _currentUser?.Username, "Backup restored after integrity check; a safety copy was created.");
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
        var refundablePayment = new ComboBox { MinWidth = 280, PlaceholderText = "Choisir un paiement à rembourser" };
        var refundAmount = Field("Montant du remboursement");
        var refundMethod = new ComboBox { MinWidth = 260, ItemsSource = new[] { "Espèces", "Virement", "Mobile money", "Carte" }, SelectedIndex = 0 };
        var refundReason = Field("Motif du remboursement (obligatoire)");

        async Task Refresh()
        {
            list.ItemsSource = await _database.GetRowsAsync(
                """
                SELECT i.id, i.patient_name, i.description, i.amount, i.currency, i.status,
                       COALESCE((SELECT SUM(p.amount) FROM payments p WHERE p.invoice_id=i.id),0)
                       - COALESCE((SELECT SUM(r.amount) FROM refunds r JOIN payments p ON p.id=r.payment_id WHERE p.invoice_id=i.id),0)
                FROM invoices i ORDER BY i.id DESC
                """,
                r => $"Facture #{r.GetInt64(0)} · {r.GetString(1)} · {r.GetString(2)} · Total {r.GetDecimal(3):0.00} {r.GetString(4)} · Net encaissé {r.GetDecimal(6):0.00} · {r.GetString(5)}");
            invoiceChoice.ItemsSource = await _database.GetInvoiceChoicesAsync();
            refundablePayment.ItemsSource = await _database.GetRefundablePaymentsAsync();
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
            await _database.WriteAuditAsync("INVOICE_CREATED", "invoice", _currentUser?.Username);
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
            var paymentId = await _database.RecordPaymentAsync(selected.Id, paid, method, _currentUser?.Username);
            invoiceChoice.SelectedItem = null;
            paymentAmount.Text = "";
            await Refresh();
            try
            {
                var receiptPath = await _database.CreateReceiptAsync(paymentId);
                try
                {
                    var printablePath = await _database.CreatePrintableReceiptAsync(paymentId);
                    SetStatus("Paiement enregistré. Reçu texte : " + receiptPath + " · Reçu imprimable : " + printablePath);
                }
                catch (Exception printableError)
                {
                    SetStatus("Paiement enregistré. Reçu texte : " + receiptPath + " · Reçu imprimable indisponible : " + printableError.Message, true);
                }
            }
            catch (Exception receiptError)
            {
                SetStatus("Paiement enregistré, mais le reçu n'a pas pu être créé : " + receiptError.Message, true);
            }
        });

        var refund = ActionButton("Enregistrer le remboursement", false);
        refund.Click += async (_, _) => await SafeRunAsync(async () =>
        {
            if (refundablePayment.SelectedItem is not RefundChoice selected ||
                !decimal.TryParse(refundAmount.Text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var refunded) ||
                refunded <= 0 || refundMethod.SelectedItem is not string method || string.IsNullOrWhiteSpace(refundReason.Text))
            { SetStatus("Sélectionnez un paiement, un montant positif, un moyen et un motif de remboursement.", true); return; }
            if (refundReason.Text.Trim().Length > 250)
            { SetStatus("Le motif du remboursement ne doit pas dépasser 250 caractères.", true); return; }
            var refundId = await _database.RecordRefundAsync(selected.PaymentId, refunded, method, refundReason.Text.Trim(), _currentUser?.Username);
            refundAmount.Text = refundReason.Text = "";
            await Refresh();
            try
            {
                var receiptPath = await _database.CreateRefundReceiptAsync(refundId);
                try
                {
                    var printablePath = await _database.CreatePrintableRefundReceiptAsync(refundId);
                    SetStatus("Remboursement enregistré. Reçu texte : " + receiptPath + " · Reçu imprimable : " + printablePath);
                }
                catch (Exception printableError)
                {
                    SetStatus("Remboursement enregistré. Reçu texte : " + receiptPath + " · Reçu imprimable indisponible : " + printableError.Message, true);
                }
            }
            catch (Exception receiptError)
            {
                SetStatus("Remboursement enregistré, mais le reçu n'a pas pu être créé : " + receiptError.Message, true);
            }
        });

        _content.Children.Add(Card(new StackPanel { Spacing = 12, Children = { Heading("Nouvelle facture"), patient, description, amount, currency, save } }));
        _content.Children.Add(Card(new StackPanel { Spacing = 10, Children = { Heading("Enregistrer un paiement"), invoiceChoice, paymentAmount, paymentMethod, pay } }));
        _content.Children.Add(Card(new StackPanel { Spacing = 10, Children = { Heading("Rembourser un paiement"), refundablePayment, refundAmount, refundMethod, refundReason, refund } }));
        _content.Children.Add(Card(new StackPanel { Spacing = 10, Children = { Heading("Factures et montants nets encaissés"), list } }));
        await Refresh();
    }

}

internal sealed class HospitalDatabase
{
    private readonly string _path;
    private string ConnectionString => new SqliteConnectionStringBuilder { DataSource = _path, Mode = SqliteOpenMode.ReadWriteCreate }.ToString();
    private bool _initialized;

    internal HospitalDatabase(string? databasePath = null)
    {
        _path = databasePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AfriHospital", "afrihospital.db");
    }

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
CREATE TABLE IF NOT EXISTS medicine_batches(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 medicine_id INTEGER NOT NULL REFERENCES medicines(id) ON DELETE CASCADE,
 batch_number TEXT NOT NULL,
 supplier TEXT NOT NULL DEFAULT '',
 quantity INTEGER NOT NULL CHECK(quantity >= 0),
 expiry_date TEXT NULL,
 unit_cost NUMERIC NOT NULL DEFAULT 0 CHECK(unit_cost >= 0),
 created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
 UNIQUE(medicine_id, batch_number)
);
CREATE INDEX IF NOT EXISTS ix_medicine_batches_expiry ON medicine_batches(expiry_date, quantity);
CREATE TABLE IF NOT EXISTS medicine_stock_movements(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 medicine_id INTEGER NOT NULL REFERENCES medicines(id),
 batch_id INTEGER NULL REFERENCES medicine_batches(id),
 movement_type TEXT NOT NULL CHECK(movement_type IN ('INITIAL','IN','OUT')),
 quantity INTEGER NOT NULL CHECK(quantity > 0),
 note TEXT NOT NULL DEFAULT '',
 created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS ix_medicine_movements_medicine_date ON medicine_stock_movements(medicine_id, created_at);
CREATE TABLE IF NOT EXISTS invoices(id INTEGER PRIMARY KEY AUTOINCREMENT, patient_name TEXT NOT NULL, description TEXT NOT NULL DEFAULT '', amount NUMERIC NOT NULL, currency TEXT NOT NULL, status TEXT NOT NULL DEFAULT 'À payer', created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS payments(id INTEGER PRIMARY KEY AUTOINCREMENT, invoice_id INTEGER NOT NULL REFERENCES invoices(id), amount NUMERIC NOT NULL CHECK(amount > 0), method TEXT NOT NULL, paid_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE INDEX IF NOT EXISTS ix_payments_invoice ON payments(invoice_id);
CREATE TABLE IF NOT EXISTS refunds(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 payment_id INTEGER NOT NULL REFERENCES payments(id),
 amount NUMERIC NOT NULL CHECK(amount > 0),
 method TEXT NOT NULL,
 reason TEXT NOT NULL,
 refunded_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS ix_refunds_payment ON refunds(payment_id);
CREATE TABLE IF NOT EXISTS audit_log(id INTEGER PRIMARY KEY AUTOINCREMENT, action TEXT NOT NULL, entity TEXT NOT NULL, actor TEXT NOT NULL DEFAULT 'unknown', details TEXT NOT NULL DEFAULT '', created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);";
        await command.ExecuteNonQueryAsync();
        await EnsureColumnAsync(connection, "audit_log", "actor", "TEXT NOT NULL DEFAULT 'unknown'");
        await EnsureColumnAsync(connection, "audit_log", "details", "TEXT NOT NULL DEFAULT ''");
        await using (var auditProtection = connection.CreateCommand())
        {
            auditProtection.CommandText = """
                CREATE TRIGGER IF NOT EXISTS trg_audit_log_no_update
                BEFORE UPDATE ON audit_log BEGIN SELECT RAISE(ABORT, 'audit log is append-only'); END;
                CREATE TRIGGER IF NOT EXISTS trg_audit_log_no_delete
                BEFORE DELETE ON audit_log BEGIN SELECT RAISE(ABORT, 'audit log is append-only'); END;
                """;
            await auditProtection.ExecuteNonQueryAsync();
        }
        // Preserve pre-existing quantities as a legacy batch; do not invent an expiry date.
        await using (var legacyBatches = connection.CreateCommand())
        {
            legacyBatches.CommandText = """
                INSERT OR IGNORE INTO medicine_batches(medicine_id, batch_number, supplier, quantity, expiry_date, unit_cost)
                SELECT m.id, 'LEGACY-' || m.id, 'Stock initial existant', m.quantity, NULL, m.unit_price
                FROM medicines m
                WHERE m.quantity > 0
                  AND NOT EXISTS (SELECT 1 FROM medicine_batches b WHERE b.medicine_id=m.id);
                INSERT INTO medicine_stock_movements(medicine_id, batch_id, movement_type, quantity, note)
                SELECT b.medicine_id, b.id, 'INITIAL', b.quantity, 'Stock préexistant migré; péremption inconnue'
                FROM medicine_batches b
                WHERE b.batch_number = 'LEGACY-' || b.medicine_id
                  AND NOT EXISTS (SELECT 1 FROM medicine_stock_movements sm WHERE sm.batch_id=b.id);
                """;
            await legacyBatches.ExecuteNonQueryAsync();
        }
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

    public async Task<List<MedicineChoice>> GetMedicineChoicesAsync()
    {
        var items = new List<MedicineChoice>();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, quantity, unit, min_quantity FROM medicines ORDER BY name;";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(new MedicineChoice(reader.GetInt64(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3), reader.GetInt32(4)));
        return items;
    }

    public async Task<List<string>> GetStockMovementRowsAsync()
    {
        var rows = new List<string>();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT sm.id, m.name, COALESCE(b.batch_number, '—'), sm.movement_type,
                   sm.quantity, sm.note, sm.created_at
            FROM medicine_stock_movements sm
            JOIN medicines m ON m.id=sm.medicine_id
            LEFT JOIN medicine_batches b ON b.id=sm.batch_id
            ORDER BY sm.id DESC LIMIT 200;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add($"#{reader.GetInt64(0)} · {reader.GetString(1)} · lot {reader.GetString(2)} · {reader.GetString(3)} {reader.GetInt32(4)} · {reader.GetString(5)} · {reader.GetString(6)}");
        return rows;
    }

    public async Task<List<MedicineBatchChoice>> GetBatchChoicesAsync(long medicineId)
    {
        var items = new List<MedicineBatchChoice>();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, medicine_id, batch_number, quantity, expiry_date, supplier
            FROM medicine_batches
            WHERE medicine_id=$medicineId AND quantity > 0
            ORDER BY CASE WHEN expiry_date IS NULL THEN 1 ELSE 0 END, expiry_date, batch_number;
            """;
        command.Parameters.AddWithValue("$medicineId", medicineId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(new MedicineBatchChoice(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2),
                reader.GetInt32(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5)));
        return items;
    }

    public async Task<long> AddMedicineAsync(string name, int initialQuantity, string unit, decimal unitPrice, int minimumQuantity)
    {
        if (string.IsNullOrWhiteSpace(name) || initialQuantity < 0 || minimumQuantity < 0 || unitPrice < 0)
            throw new ArgumentException("Les données du médicament sont invalides.");
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO medicines(name, quantity, unit, unit_price, min_quantity) VALUES($name,$quantity,$unit,$price,$minimum);";
            insert.Parameters.AddWithValue("$name", name.Trim());
            insert.Parameters.AddWithValue("$quantity", initialQuantity);
            insert.Parameters.AddWithValue("$unit", unit.Trim());
            insert.Parameters.AddWithValue("$price", unitPrice);
            insert.Parameters.AddWithValue("$minimum", minimumQuantity);
            await insert.ExecuteNonQueryAsync();
        }
        await using var idCommand = connection.CreateCommand();
        idCommand.Transaction = transaction;
        idCommand.CommandText = "SELECT last_insert_rowid();";
        var id = Convert.ToInt64(await idCommand.ExecuteScalarAsync());
        if (initialQuantity > 0)
        {
            long batchId;
            await using (var batch = connection.CreateCommand())
            {
                batch.Transaction = transaction;
                batch.CommandText = "INSERT INTO medicine_batches(medicine_id,batch_number,supplier,quantity,expiry_date,unit_cost) VALUES($id,$number,$supplier,$quantity,NULL,$cost);";
                batch.Parameters.AddWithValue("$id", id);
                batch.Parameters.AddWithValue("$number", "LEGACY-" + id);
                batch.Parameters.AddWithValue("$supplier", "Stock initial existant");
                batch.Parameters.AddWithValue("$quantity", initialQuantity);
                batch.Parameters.AddWithValue("$cost", unitPrice);
                await batch.ExecuteNonQueryAsync();
            }
            await using (var batchIdCommand = connection.CreateCommand())
            {
                batchIdCommand.Transaction = transaction;
                batchIdCommand.CommandText = "SELECT last_insert_rowid();";
                batchId = Convert.ToInt64(await batchIdCommand.ExecuteScalarAsync());
            }
            await using var movement = connection.CreateCommand();
            movement.Transaction = transaction;
            movement.CommandText = "INSERT INTO medicine_stock_movements(medicine_id,batch_id,movement_type,quantity,note) VALUES($id,$batch,'INITIAL',$quantity,'Stock initial; péremption à vérifier');";
            movement.Parameters.AddWithValue("$id", id);
            movement.Parameters.AddWithValue("$batch", batchId);
            movement.Parameters.AddWithValue("$quantity", initialQuantity);
            await movement.ExecuteNonQueryAsync();
        }
        transaction.Commit();
        return id;
    }

    public async Task RecordStockMovementAsync(long medicineId, bool inbound, int quantity, string? batchNumber,
        string? expiryDate, string? supplier, decimal unitCost, long? selectedBatchId, string note, string? actor = null)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "La quantité du mouvement doit être positive.");
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        long batchId;
        if (inbound)
        {
            if (string.IsNullOrWhiteSpace(batchNumber) || string.IsNullOrWhiteSpace(expiryDate))
                throw new ArgumentException("Le numéro de lot et la date de péremption sont obligatoires pour une entrée.");
            if (!DateOnly.TryParseExact(expiryDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var parsedExpiry) ||
                parsedExpiry < DateOnly.FromDateTime(DateTime.Today))
                throw new ArgumentException("La date de péremption doit être valide et ne pas être passée.");
            long? existingId = null;
            int existingQuantity = 0;
            string? existingExpiry = null;
            string? existingSupplier = null;
            decimal existingCost = 0;
            await using (var find = connection.CreateCommand())
            {
                find.Transaction = transaction;
                find.CommandText = "SELECT id,quantity,expiry_date,supplier,unit_cost FROM medicine_batches WHERE medicine_id=$medicine AND batch_number=$number;";
                find.Parameters.AddWithValue("$medicine", medicineId);
                find.Parameters.AddWithValue("$number", batchNumber.Trim());
                await using var reader = await find.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    existingId = reader.GetInt64(0);
                    existingQuantity = reader.GetInt32(1);
                    existingExpiry = reader.IsDBNull(2) ? null : reader.GetString(2);
                    existingSupplier = reader.GetString(3);
                    existingCost = reader.GetDecimal(4);
                }
            }
            if (existingId.HasValue)
            {
                if (existingExpiry != expiryDate || !string.Equals(existingSupplier, supplier ?? "", StringComparison.Ordinal) || existingCost != unitCost)
                    throw new InvalidOperationException("Ce numéro de lot existe déjà avec une autre péremption, un autre fournisseur ou un autre coût.");
                batchId = existingId.Value;
                await using var updateBatch = connection.CreateCommand();
                updateBatch.Transaction = transaction;
                updateBatch.CommandText = "UPDATE medicine_batches SET quantity=$quantity WHERE id=$id;";
                updateBatch.Parameters.AddWithValue("$quantity", checked(existingQuantity + quantity));
                updateBatch.Parameters.AddWithValue("$id", batchId);
                await updateBatch.ExecuteNonQueryAsync();
            }
            else
            {
                await using var insertBatch = connection.CreateCommand();
                insertBatch.Transaction = transaction;
                insertBatch.CommandText = "INSERT INTO medicine_batches(medicine_id,batch_number,supplier,quantity,expiry_date,unit_cost) VALUES($medicine,$number,$supplier,$quantity,$expiry,$cost);";
                insertBatch.Parameters.AddWithValue("$medicine", medicineId);
                insertBatch.Parameters.AddWithValue("$number", batchNumber.Trim());
                insertBatch.Parameters.AddWithValue("$supplier", supplier ?? "");
                insertBatch.Parameters.AddWithValue("$quantity", quantity);
                insertBatch.Parameters.AddWithValue("$expiry", expiryDate);
                insertBatch.Parameters.AddWithValue("$cost", unitCost);
                await insertBatch.ExecuteNonQueryAsync();
                await using var idCommand = connection.CreateCommand();
                idCommand.Transaction = transaction;
                idCommand.CommandText = "SELECT last_insert_rowid();";
                batchId = Convert.ToInt64(await idCommand.ExecuteScalarAsync());
            }
        }
        else
        {
            if (!selectedBatchId.HasValue) throw new ArgumentException("Sélectionnez un lot pour la sortie.");
            batchId = selectedBatchId.Value;
            await using var find = connection.CreateCommand();
            find.Transaction = transaction;
            find.CommandText = "SELECT quantity,expiry_date FROM medicine_batches WHERE id=$batch AND medicine_id=$medicine;";
            find.Parameters.AddWithValue("$batch", batchId);
            find.Parameters.AddWithValue("$medicine", medicineId);
            await using var reader = await find.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) throw new KeyNotFoundException("Lot introuvable pour ce médicament.");
            var available = reader.GetInt32(0);
            var expiry = reader.IsDBNull(1) ? null : reader.GetString(1);
            if (expiry is not null && string.CompareOrdinal(expiry, DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)) < 0)
                throw new InvalidOperationException("Impossible de sortir un lot périmé.");
            if (quantity > available) throw new InvalidOperationException("La sortie dépasse la quantité disponible dans ce lot.");
            await reader.DisposeAsync();
            await using var updateBatch = connection.CreateCommand();
            updateBatch.Transaction = transaction;
            updateBatch.CommandText = "UPDATE medicine_batches SET quantity=quantity-$quantity WHERE id=$batch;";
            updateBatch.Parameters.AddWithValue("$quantity", quantity);
            updateBatch.Parameters.AddWithValue("$batch", batchId);
            await updateBatch.ExecuteNonQueryAsync();
        }

        await using (var updateMedicine = connection.CreateCommand())
        {
            updateMedicine.Transaction = transaction;
            updateMedicine.CommandText = inbound
                ? "UPDATE medicines SET quantity=quantity+$quantity WHERE id=$id;"
                : "UPDATE medicines SET quantity=quantity-$quantity WHERE id=$id AND quantity >= $quantity;";
            updateMedicine.Parameters.AddWithValue("$quantity", quantity);
            updateMedicine.Parameters.AddWithValue("$id", medicineId);
            if (await updateMedicine.ExecuteNonQueryAsync() == 0)
                throw new InvalidOperationException("Le médicament est introuvable ou le stock est insuffisant.");
        }
        await using (var movement = connection.CreateCommand())
        {
            movement.Transaction = transaction;
            movement.CommandText = "INSERT INTO medicine_stock_movements(medicine_id,batch_id,movement_type,quantity,note) VALUES($medicine,$batch,$type,$quantity,$note);";
            movement.Parameters.AddWithValue("$medicine", medicineId);
            movement.Parameters.AddWithValue("$batch", batchId);
            movement.Parameters.AddWithValue("$type", inbound ? "IN" : "OUT");
            movement.Parameters.AddWithValue("$quantity", quantity);
            movement.Parameters.AddWithValue("$note", note);
            await movement.ExecuteNonQueryAsync();
        }
        await using (var audit = connection.CreateCommand())
        {
            audit.Transaction = transaction;
            audit.CommandText = "INSERT INTO audit_log(action,entity,actor,details) VALUES($action,'medicine',$actor,$details);";
            audit.Parameters.AddWithValue("$action", inbound ? "STOCK_IN" : "STOCK_OUT");
            audit.Parameters.AddWithValue("$actor", string.IsNullOrWhiteSpace(actor) ? "unknown" : actor.Trim());
            audit.Parameters.AddWithValue("$details", $"medicine_id={medicineId}; batch_id={batchId}; quantity={quantity}");
            await audit.ExecuteNonQueryAsync();
        }
        transaction.Commit();
    }

    public async Task<List<InvoiceChoice>> GetInvoiceChoicesAsync()
    {
        var invoices = new List<InvoiceChoice>();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT i.id, i.patient_name, i.description, i.amount, i.currency,
                   COALESCE((SELECT SUM(p.amount) FROM payments p WHERE p.invoice_id=i.id),0)
                   - COALESCE((SELECT SUM(r.amount) FROM refunds r JOIN payments p ON p.id=r.payment_id WHERE p.invoice_id=i.id),0)
            FROM invoices i
            WHERE i.amount >
                  COALESCE((SELECT SUM(p.amount) FROM payments p WHERE p.invoice_id=i.id),0)
                  - COALESCE((SELECT SUM(r.amount) FROM refunds r JOIN payments p ON p.id=r.payment_id WHERE p.invoice_id=i.id),0)
            ORDER BY i.id DESC;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            invoices.Add(new InvoiceChoice(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), reader.GetString(4), reader.GetDecimal(5)));
        return invoices;
    }

    public async Task<long> RecordPaymentAsync(long invoiceId, decimal amount, string method, string? actor = null)
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
            sum.CommandText = """
                SELECT COALESCE(SUM(p.amount),0)
                     - COALESCE((SELECT SUM(r.amount) FROM refunds r JOIN payments p2 ON p2.id=r.payment_id WHERE p2.invoice_id=$id),0)
                FROM payments p WHERE p.invoice_id=$id;
                """;
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
            audit.CommandText = "INSERT INTO audit_log(action, entity, actor, details) VALUES('PAYMENT_RECORDED','invoice',$actor,$details);";
            audit.Parameters.AddWithValue("$actor", string.IsNullOrWhiteSpace(actor) ? "unknown" : actor.Trim());
            audit.Parameters.AddWithValue("$details", $"invoice_id={invoiceId}; payment_id={paymentId}; amount={amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}; method={method.Trim()}");
            await audit.ExecuteNonQueryAsync();
        }
        transaction.Commit();
        return paymentId;
    }

    public async Task<List<RefundChoice>> GetRefundablePaymentsAsync()
    {
        var items = new List<RefundChoice>();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.id, p.invoice_id, i.patient_name, p.amount, COALESCE(SUM(r.amount),0), p.method
            FROM payments p
            JOIN invoices i ON i.id=p.invoice_id
            LEFT JOIN refunds r ON r.payment_id=p.id
            GROUP BY p.id
            HAVING p.amount > COALESCE(SUM(r.amount),0)
            ORDER BY p.id DESC;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(new RefundChoice(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2),
                reader.GetDecimal(3), reader.GetDecimal(4), reader.GetString(5)));
        return items;
    }

    public async Task<long> RecordRefundAsync(long paymentId, decimal amount, string method, string reason, string? actor = null)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Le remboursement doit être positif.");
        if (string.IsNullOrWhiteSpace(method)) throw new ArgumentException("Le moyen de remboursement est obligatoire.", nameof(method));
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 250)
            throw new ArgumentException("Le motif du remboursement est obligatoire et limité à 250 caractères.", nameof(reason));

        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        long invoiceId;
        decimal originalAmount;
        decimal alreadyRefunded;
        await using (var payment = connection.CreateCommand())
        {
            payment.Transaction = transaction;
            payment.CommandText = "SELECT invoice_id, amount FROM payments WHERE id=$id;";
            payment.Parameters.AddWithValue("$id", paymentId);
            await using var reader = await payment.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) throw new KeyNotFoundException("Paiement introuvable.");
            invoiceId = reader.GetInt64(0);
            originalAmount = reader.GetDecimal(1);
        }
        await using (var totalRefunded = connection.CreateCommand())
        {
            totalRefunded.Transaction = transaction;
            totalRefunded.CommandText = "SELECT COALESCE(SUM(amount),0) FROM refunds WHERE payment_id=$id;";
            totalRefunded.Parameters.AddWithValue("$id", paymentId);
            alreadyRefunded = Convert.ToDecimal(await totalRefunded.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        }
        if (amount > originalAmount - alreadyRefunded)
            throw new InvalidOperationException("Le remboursement dépasse le montant encore remboursable de ce paiement.");

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO refunds(payment_id,amount,method,reason) VALUES($payment,$amount,$method,$reason);";
            insert.Parameters.AddWithValue("$payment", paymentId);
            insert.Parameters.AddWithValue("$amount", amount);
            insert.Parameters.AddWithValue("$method", method.Trim());
            insert.Parameters.AddWithValue("$reason", reason.Trim());
            await insert.ExecuteNonQueryAsync();
        }
        await using var idCommand = connection.CreateCommand();
        idCommand.Transaction = transaction;
        idCommand.CommandText = "SELECT last_insert_rowid();";
        var refundId = Convert.ToInt64(await idCommand.ExecuteScalarAsync());

        decimal netPaid;
        decimal invoiceTotal;
        await using (var invoice = connection.CreateCommand())
        {
            invoice.Transaction = transaction;
            invoice.CommandText = "SELECT amount FROM invoices WHERE id=$id;";
            invoice.Parameters.AddWithValue("$id", invoiceId);
            invoiceTotal = Convert.ToDecimal(await invoice.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        }
        await using (var net = connection.CreateCommand())
        {
            net.Transaction = transaction;
            net.CommandText = """
                SELECT COALESCE((SELECT SUM(amount) FROM payments WHERE invoice_id=$id),0)
                     - COALESCE((SELECT SUM(r.amount) FROM refunds r JOIN payments p ON p.id=r.payment_id WHERE p.invoice_id=$id),0);
                """;
            net.Parameters.AddWithValue("$id", invoiceId);
            netPaid = Convert.ToDecimal(await net.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        }
        var status = netPaid <= 0 ? "À payer" : netPaid >= invoiceTotal ? "Payée" : "Partiellement payée";
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = "UPDATE invoices SET status=$status WHERE id=$id;";
            update.Parameters.AddWithValue("$status", status);
            update.Parameters.AddWithValue("$id", invoiceId);
            await update.ExecuteNonQueryAsync();
        }
        await using (var audit = connection.CreateCommand())
        {
            audit.Transaction = transaction;
            audit.CommandText = "INSERT INTO audit_log(action,entity,actor,details) VALUES('REFUND_RECORDED','invoice',$actor,$details);";
            audit.Parameters.AddWithValue("$actor", string.IsNullOrWhiteSpace(actor) ? "unknown" : actor.Trim());
            audit.Parameters.AddWithValue("$details", $"payment_id={paymentId}; refund_id={refundId}; amount={amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}; method={method.Trim()}");
            await audit.ExecuteNonQueryAsync();
        }
        transaction.Commit();
        return refundId;
    }

    public async Task<string> CreateRefundReceiptAsync(long refundId)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT r.id, p.id, i.patient_name, i.description, r.amount, i.currency, r.method, r.reason, r.refunded_at
            FROM refunds r JOIN payments p ON p.id=r.payment_id JOIN invoices i ON i.id=p.invoice_id
            WHERE r.id=$id;
            """;
        command.Parameters.AddWithValue("$id", refundId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new KeyNotFoundException("Remboursement introuvable pour générer le reçu.");
        var receipt = string.Join(Environment.NewLine, new[]
        {
            "AFRIHOSPITAL — REÇU DE REMBOURSEMENT",
            "===================================",
            $"Remboursement n° : {reader.GetInt64(0)}",
            $"Paiement d'origine n° : {reader.GetInt64(1)}",
            $"Patient : {reader.GetString(2)}",
            $"Description : {reader.GetString(3)}",
            $"Montant remboursé : {reader.GetDecimal(4).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} {reader.GetString(5)}",
            $"Moyen : {reader.GetString(6)}",
            $"Motif : {reader.GetString(7)}",
            $"Date : {reader.GetString(8)}",
            "",
            "Document généré localement. Conservez-le de manière sécurisée."
        });
        await reader.DisposeAsync();
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AfriHospital-Receipts");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"refund-{refundId}-{DateTime.UtcNow:yyyyMMddHHmmss}.txt");
        await File.WriteAllTextAsync(path, receipt, new System.Text.UTF8Encoding(false));
        return path;
    }

    public async Task<string> CreatePrintableReceiptAsync(long paymentId)
    {
        var fields = new List<(string Label, string Value)>();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.id, p.invoice_id, i.patient_name, i.description, p.amount, i.currency, p.method, p.paid_at
            FROM payments p JOIN invoices i ON i.id=p.invoice_id WHERE p.id=$id;
            """;
        command.Parameters.AddWithValue("$id", paymentId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new KeyNotFoundException("Paiement introuvable pour générer le reçu imprimable.");
        fields.Add(("Reçu n°", reader.GetInt64(0).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        fields.Add(("Facture n°", reader.GetInt64(1).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        fields.Add(("Patient", reader.GetString(2)));
        fields.Add(("Description", reader.GetString(3)));
        fields.Add(("Montant reçu", reader.GetDecimal(4).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " " + reader.GetString(5)));
        fields.Add(("Moyen de paiement", reader.GetString(6)));
        fields.Add(("Date", reader.GetString(7)));
        await reader.DisposeAsync();
        return await WritePrintableReceiptAsync("Reçu de paiement", fields, "receipt", paymentId);
    }

    public async Task<string> CreatePrintableRefundReceiptAsync(long refundId)
    {
        var fields = new List<(string Label, string Value)>();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT r.id, p.id, i.patient_name, i.description, r.amount, i.currency, r.method, r.reason, r.refunded_at
            FROM refunds r JOIN payments p ON p.id=r.payment_id JOIN invoices i ON i.id=p.invoice_id
            WHERE r.id=$id;
            """;
        command.Parameters.AddWithValue("$id", refundId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new KeyNotFoundException("Remboursement introuvable pour générer le reçu imprimable.");
        fields.Add(("Remboursement n°", reader.GetInt64(0).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        fields.Add(("Paiement d'origine n°", reader.GetInt64(1).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        fields.Add(("Patient", reader.GetString(2)));
        fields.Add(("Description", reader.GetString(3)));
        fields.Add(("Montant remboursé", reader.GetDecimal(4).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " " + reader.GetString(5)));
        fields.Add(("Moyen de remboursement", reader.GetString(6)));
        fields.Add(("Motif", reader.GetString(7)));
        fields.Add(("Date", reader.GetString(8)));
        await reader.DisposeAsync();
        return await WritePrintableReceiptAsync("Reçu de remboursement", fields, "refund", refundId);
    }

    private static async Task<string> WritePrintableReceiptAsync(string title, IReadOnlyList<(string Label, string Value)> fields, string prefix, long id)
    {
        var rows = string.Join("", fields.Select(field =>
            "<tr><th>" + System.Net.WebUtility.HtmlEncode(field.Label) + "</th><td>" +
            System.Net.WebUtility.HtmlEncode(field.Value) + "</td></tr>"));
        var html = "<!doctype html><html lang=\"fr\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>" +
            System.Net.WebUtility.HtmlEncode(title) + "</title><style>" +
            "body{font-family:Arial,sans-serif;color:#171717;margin:0;padding:32px;background:#f3f4f6}" +
            ".receipt{max-width:760px;margin:auto;background:white;border:1px solid #ddd;padding:32px;border-top:10px solid #A8E66B}" +
            "h1{margin:0;color:#171717}h2{margin-top:8px;font-weight:normal;color:#555}" +
            "table{width:100%;border-collapse:collapse;margin-top:28px}th,td{text-align:left;padding:12px;border-bottom:1px solid #ddd;vertical-align:top}" +
            "th{width:34%;background:#f8f8f8}.print{margin-top:24px;padding:12px 18px;background:#FFD43B;border:0;cursor:pointer}" +
            "@media print{body{background:white;padding:0}.receipt{border:0;border-top:8px solid #A8E66B;max-width:none}.print{display:none}}" +
            "</style></head><body><main class=\"receipt\"><h1>AfriHospital</h1><h2>" +
            System.Net.WebUtility.HtmlEncode(title) + "</h2><table>" + rows +
            "</table><p>Document généré localement. Conservez-le de manière sécurisée.</p>" +
            "<button class=\"print\" onclick=\"window.print()\">Imprimer / Enregistrer en PDF</button>" +
            "</main></body></html>";
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AfriHospital-Receipts");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{prefix}-{id}-{DateTime.UtcNow:yyyyMMddHHmmss}.html");
        await File.WriteAllTextAsync(path, html, new System.Text.UTF8Encoding(false));
        return path;
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

    public async Task WriteAuditAsync(string action, string entity, string? actor = null, string? details = null)
    {
        await ExecuteAsync(
            "INSERT INTO audit_log(action, entity, actor, details) VALUES($action, $entity, $actor, $details)",
            ("$action", action), ("$entity", entity), ("$actor", string.IsNullOrWhiteSpace(actor) ? "unknown" : actor.Trim()),
            ("$details", details ?? ""));
    }

    public async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqliteConnection(ConnectionString); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<string>> GetRowsAsync(string sql, Func<SqliteDataReader,string> map, params (string Name, object Value)[] parameters)
    {
        var rows = new List<string>();
        await using var connection = new SqliteConnection(ConnectionString); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
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

internal sealed record MedicineChoice(long Id, string Name, int Quantity, string Unit, int MinimumQuantity)
{
    public override string ToString() => $"{Name} · stock {Quantity} {Unit} · seuil {MinimumQuantity}";
}

internal sealed record MedicineBatchChoice(long Id, long MedicineId, string BatchNumber, int Quantity, string? ExpiryDate, string Supplier)
{
    public override string ToString() => $"{BatchNumber} · quantité {Quantity} · péremption {ExpiryDate ?? "inconnue"} · {Supplier}";
}

internal sealed record AppointmentRow(long Id, long PatientId, string PatientName, string ScheduledAt, string Reason, string Status)
{
    public override string ToString() => $"#{Id} · {PatientName} · {ScheduledAt} · {Reason} · {Status}";
}

internal sealed record PatientRow(long Id, string Name, string Phone, string BirthDate, string Gender, string Notes)
{
    public override string ToString() => $"#{Id} · {Name} · {Phone}";
}

internal sealed record RefundChoice(long PaymentId, long InvoiceId, string PatientName, decimal OriginalAmount, decimal Refunded, string OriginalMethod)
{
    public decimal Available => OriginalAmount - Refunded;
    public override string ToString() => $"Paiement #{PaymentId} · Facture #{InvoiceId} · {PatientName} · remboursable {Available:0.00} · {OriginalMethod}";
}

internal sealed record InvoiceChoice(long Id, string PatientName, string Description, decimal Total, string Currency, decimal Paid)
{
    public override string ToString() => $"Facture #{Id} · {PatientName} · Solde {(Total - Paid):0.00} {Currency}";
}
