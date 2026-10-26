# AfriHospital — Checklist de développement

Les cases cochées indiquent les fonctionnalités ajoutées au dépôt. La compilation, les tests, la sécurité et la préparation à la production ne sont cochés qu'après vérification effective.

## Étape 1 — Tableau de bord

- [x] Définir les couleurs : vert clair (#A8E66B), jaune (#FFD43B) et noir (#171717).
- [x] Créer le tableau de bord avec statistiques calculées depuis la base locale.
- [x] Ajouter un menu latéral pour les modules.
- [x] Distinguer les alertes de stock faible.
- [ ] Restaurer les dépendances et compiler le projet localement ou via GitHub Actions.
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
- [ ] Relier les rendez-vous et consultations à des identifiants de patients plutôt qu'à leur nom.
- [ ] Ajouter les statuts, la modification, l'annulation et les contrôles de conflits.
- [ ] Faire valider les champs cliniques par des professionnels de santé.

## Étape 4 — Facturation et pharmacie

- [x] Créer et consulter des factures avec devise configurable.
- [x] Ajouter des médicaments et quantités en stock.
- [x] Afficher les alertes de stock faible sur le tableau de bord.
- [ ] Gérer les paiements, remboursements et reçus.
- [ ] Gérer les lots, numéros de lot, péremptions, fournisseurs et mouvements de stock.
- [ ] Valider les règles fiscales et monétaires de chaque pays cible.

## Étape 5 — Sauvegarde, sécurité et déploiement

- [x] Ajouter une action de sauvegarde locale de la base SQLite.
- [ ] Tester la restauration et automatiser les sauvegardes périodiques.
- [ ] Mettre en place une authentification robuste et des rôles avec autorisations.
- [ ] Chiffrer les données sensibles et les sauvegardes, sécuriser les secrets et les accès.
- [ ] Ajouter un journal d'audit fiable pour les opérations sensibles.
- [ ] Définir les politiques de conservation, consentement et conformité du pays cible.
- [ ] Tester le fonctionnement hors ligne et la synchronisation multi-postes.
- [ ] Exécuter les tests automatisés et confirmer un build réussi.

> **Avertissement :** ce projet reste un prototype de développement. La base locale et les sauvegardes ne sont pas chiffrées, il n'y a pas encore d'authentification ni de contrôle d'accès. Ne pas l'utiliser avec de vrais dossiers médicaux ou pour des décisions cliniques avant une revue de sécurité, des tests et une validation locale.
