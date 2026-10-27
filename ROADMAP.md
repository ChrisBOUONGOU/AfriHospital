# AfriHospital — Checklist de développement

Les cases cochées indiquent les fonctionnalités ajoutées au dépôt. La compilation, les tests, la sécurité et la préparation à la production ne sont cochés qu'après vérification effective.

## Étape 1 — Tableau de bord

- [x] Définir les couleurs : vert clair (#A8E66B), jaune (#FFD43B) et noir (#171717).
- [x] Créer le tableau de bord avec statistiques calculées depuis la base locale.
- [x] Ajouter un menu latéral pour les modules.
- [x] Distinguer les alertes de stock faible.
- [x] Restaurer les dépendances et compiler le projet via GitHub Actions (build réussi sur le commit de code vérifié).
- [ ] Lancer l'application et vérifier le rendu sur Windows/Linux.
- [ ] Vérifier l'affichage sur petits écrans et l'accessibilité.

## Étape 2 — Gestion des patients

- [x] Ajouter un patient.
- [x] Rechercher les patients par nom ou téléphone.
- [x] Modifier un patient en le sélectionnant dans la liste.
- [x] Enregistrer les fiches dans une base SQLite locale.
- [ ] Ajouter la validation complète des dates et des champs selon les règles locales.
- [ ] Ajouter des tests automatisés et vérifier la protection des données.

## Étape 3 — Consultations et rendez-vous

- [x] Enregistrer et consulter la liste des consultations.
- [x] Créer et consulter les rendez-vous.
- [x] Rendre les champs obligatoires minimaux.
- [x] Relier les nouveaux rendez-vous et consultations à un identifiant de patient sélectionné.
- [x] Ajouter une migration SQLite compatible avec les bases existantes et rattacher automatiquement les anciens enregistrements lorsque le nom correspond exactement.
- [x] Valider le format de date et d’heure à la création des rendez-vous et consultations.
- [x] Ajouter les statuts, la modification, l'annulation et les contrôles de conflits (conflits détectés pour un même patient à date/heure identique).
- [ ] Faire valider les champs cliniques par des professionnels de santé.

## Étape 4 — Facturation et pharmacie

- [x] Créer et consulter des factures avec devise configurable.
- [x] Ajouter des médicaments et quantités en stock.
- [x] Afficher les alertes de stock faible sur le tableau de bord.
- [x] Enregistrer les paiements partiels/complets, calculer le solde et mettre à jour le statut de facture.
- [x] Générer un reçu texte local après l'enregistrement d'un paiement.
- [x] Gérer les remboursements partiels/complets, recalculer les soldes et statuts, et produire des reçus texte et HTML imprimables.
- [x] Gérer les lots, numéros de lot, péremptions, fournisseurs et mouvements d'entrée/sortie de stock.
- [ ] Valider les règles fiscales et monétaires de chaque pays cible.

## Étape 5 — Sauvegarde, sécurité et déploiement

- [x] Ajouter une fabrique de connexions SQLite et un schéma de persistance pour les nouvelles entités GUID.
- [x] Ajouter les interfaces de dépôt pour les patients, consultations et utilisateurs hospitaliers.
- [x] Ajouter les dépôts SQLite pour les nouvelles entités.
- [x] Ajouter un hachage de mot de passe PBKDF2-SHA256 avec sel aléatoire et comparaison en temps constant.
- [x] Ajouter un service applicatif de création de compte et de vérification des identifiants.
- [x] Importer de façon idempotente les patients et consultations historiques vers les tables GUID, avec identifiants de correspondance, en préservant les tables sources.
- [x] Unifier le stockage des patients et consultations : les dépôts métier écrivent dans les tables historiques partagées et réconcilient la projection GUID à l'initialisation/lecture.
- [ ] Refactoriser l'interface Avalonia pour utiliser directement les dépôts métier plutôt que le SQL intégré à MainWindow (en cours : les écrans Patients, Consultations et Rendez-vous sont migrés vers des dépôts dédiés ; Pharmacie, Facturation, Utilisateurs, Audit et Sauvegarde utilisent encore `HospitalDatabase`).
- [x] Migrer les écrans Patients et Consultations vers `IPatientRepository` et `IConsultationRepository`, avec tests de conservation des notes et de lecture de l'historique.
- [x] Créer un parcours de première configuration qui n'autorise le premier administrateur que si aucun compte n'existe.
- [x] Ajouter un filtrage initial des menus selon le rôle.
- [ ] Renforcer et tester les autorisations dans chaque module et dans la couche métier.
- [x] Ajouter une page administrateur pour créer des comptes avec rôles prédéfinis.

- [x] Ajouter une action de sauvegarde locale de la base SQLite.
- [x] Ajouter la restauration SQLite après vérification d'intégrité, avec copie de sécurité de la base active.
- [ ] Tester la restauration sur plusieurs scénarios et automatiser les sauvegardes périodiques.
- [ ] Mettre en place une authentification robuste et des rôles avec autorisations.
- [ ] Chiffrer les données sensibles et les sauvegardes, sécuriser les secrets et les accès.
- [x] Journaliser les connexions réussies/échouées et la création de comptes.
- [x] Ajouter un journal métier et de sauvegarde/restauration avec acteur, détails non cliniques et consultation réservée à l'administrateur.
- [x] Rendre les journaux métier et de sécurité append-only dans SQLite et tester le rejet des mises à jour/suppressions.
- [ ] Étendre l'audit aux exports de reçus et aux futures opérations sensibles; définir les règles de conservation.
- [ ] Définir les politiques de conservation, consentement et conformité du pays cible.
- [ ] Tester le fonctionnement hors ligne et la synchronisation multi-postes.
- [x] Ajouter une suite xUnit et l'exécuter automatiquement dans GitHub Actions.
- [x] Confirmer un build et une exécution de tests réussis après le refactoring partiel de l'interface (19 tests xUnit réussis, build sans avertissement ni erreur).

> **Avertissement :** ce projet reste un prototype de développement. Les écrans Patients et Consultations passent maintenant par les dépôts métier, mais le refactoring global n'est pas terminé et d'autres modules utilisent encore du SQL dans `MainWindow`. L'écran de connexion et le filtrage initial des menus sont en place ; les permissions ne sont toutefois pas encore renforcées et testées dans chaque module. La base locale et les sauvegardes ne sont pas chiffrées. Ne pas l'utiliser avec de vrais dossiers médicaux ou pour des décisions cliniques avant une revue de sécurité, des tests et une validation locale.
