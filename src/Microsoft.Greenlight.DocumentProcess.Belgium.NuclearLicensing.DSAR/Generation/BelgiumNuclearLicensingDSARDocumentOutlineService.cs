using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.Greenlight.DocumentProcess.Shared.Generation;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.DocumentProcess.Belgium.NuclearLicensing.DSAR.Generation;

public class BelgiumNuclearLicensingDSARDocumentOutlineService : IDocumentOutlineService
{
    private readonly ILogger<BelgiumNuclearLicensingDSARDocumentOutlineService> _logger;
    private readonly Kernel _sk;
    private readonly DocGenerationDbContext _dbContext;
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;

    public BelgiumNuclearLicensingDSARDocumentOutlineService(
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        Kernel sk,
        DocGenerationDbContext dbContext,
        ILogger<BelgiumNuclearLicensingDSARDocumentOutlineService> logger
        )
    {
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
        _sk = sk;
        _dbContext = dbContext;
        _logger = logger;

    }

    [Experimental("SKEXP0060")]
    public async Task<List<ContentNode>> GenerateDocumentOutlineForDocument(GeneratedDocument generatedDocument)
    {
        _logger.LogInformation("BelgiumNuclearLicensingDSARDocumentOutlineService : Generating Document Outline for Document ID {DocumentId}", generatedDocument.Id);

        List<string> documentOutlineLines;

        var orderedSectionListFrench = """
                                        0 LISTE DES ABRÉVIATIONS
                                        1 INTRODUCTION ET DESCRIPTION GÉNÉRALE DU SF²
                                        1.1 GÉNÉRALITÉ
                                        1.1.1. CAPACITÉ D’ENTREPOSAGE
                                        1.1.2. DATES D’ACHÈVEMENT ET DE MISE EN SERVICE INDUSTRIELLE
                                        1.1.3. DURÉE D’EXPLOITATION
                                        1.1.4. FONCTIONS DE SURETÉ
                                        1.1.5. RETOUR D'EXPÉRIENCE
                                        1.1.6. ORGANISATION DU RAPPORT PRÉLIMINAIRE DE SÛRETÉ
                                        1.2 DESCRIPTION GÉNÉRALE DE LA LOCALISATION
                                        1.2.1. CARACTÉRISTIQUES PRINCIPALES DU SITE DE TIHANGE
                                        1.2.1.1. Localisation du site
                                        1.2.1.2. Données géographiques
                                        1.2.1.3. Données topographiques
                                        1.2.2. LOCALISATION DU SF² SUR LE SITE
                                        1.3 DESCRIPTION GÉNÉRALE DU SYSTÈME D’ENTREPOSAGE
                                        1.3.1. DESCRIPTION DU SF2
                                        1.3.1.1. Description générale
                                        1.3.1.2. Hall d’entreposage
                                        1.3.1.3. Hall de manutention
                                        1.3.1.4. Halls de surveillance
                                        1.3.1.5. Bâtiment auxiliaire
                                        1.3.1.6. Type d’emballage
                                        1.3.1.7. Bâtiment de stockage des accessoires (ASB)
                                        1.3.2. MANUTENTION DES EMBALLAGES SUR SITE
                                        1.3.2.1. Protection contre les accidents
                                        1.4 ASPECTS RÉGLEMENTAIRES
                                        1.4.1. CADRE REGLEMENTAIRE
                                        1.4.1.1. Législation belge et européenne
                                        1.4.1.2. Réglementation internationale
                                        1.5 IDENTIFICATION DES INTERVENANTS
                                        1.5.1. EXPLOITANT
                                        1.5.2. BUREAU D’ÉTUDES
                                        1.5.3. PRINCIPAUX ENTREPRENEURS
                                        1.5.4. FABRICANTS D’EMBALLAGES
                                        1.6 RÉFÉRENCES
                                        2 CARACTÉRISTIQUES DU SITE
                                        2.1 GÉOGRAPHIE ET DÉMOGRAPHIE
                                        2.1.1 DESCRIPTION DU SITE ET DU VOISINAGE
                                        2.1.1.1 Relief
                                        2.1.1.2 Aménagement de l’ancienne commune de Tihange (actuellement Huy)
                                        2.1.2 UTILISATION DU SOL
                                        2.1.2.1 Production de fruits, légumes et céréales
                                        2.1.2.2 Production laitière
                                        2.1.2.3 Installations industrielles civiles proches
                                        2.1.2.4 Habitat
                                        2.1.3 POPULATION
                                        2.1.3.1 Zones proches (jusqu’à 15 km) de la centrale
                                        2.1.3.2 Zones dans un rayon de 50 km
                                        2.1.4 EAU POTABLE
                                        2.2 VOIES DE COMMUNICATION, INSTALLATIONS INDUSTRIELLES OU MILITAIRES PROCHES
                                        2.3 MÉTÉOROLOGIE
                                        2.3.1 CLIMATOLOGIE RÉGIONALE
                                        2.3.2 MÉTÉOROLOGIE LOCALE
                                        2.3.3 PROGRAMME DE MESURES MÉTÉOROLOGIQUES SUR LE SITE DE TIHANGE
                                        2.3.4 MODÈLE MÉTÉOROLOGIQUE, ESTIMATION DE LA DIFFUSION
                                        2.4 HYDROLOGIE
                                        2.5 GÉOLOGIE, SISMICITÉ ET GÉOTECHNIQUE
                                        2.5.1 ETUDE GÉOLOGIQUE DU SITE
                                        2.5.2 ETUDE DE LA SISMICITE
                                        2.5.3 DÉTERMINATION DE L’ACCÉLÉRATION AU SOL POUR LE SSE
                                        2.5.4 MOUVEMENTS VIBRATOIRES DU SOL
                                        2.5.5 FAILLES DE SURFACE
                                        2.5.6 STABILITÉ DES PENTES
                                        2.5.7 DIGUES ET BARRAGES
                                        2.5.8 STABILITE DU SOUS-SOL ET DES FONDATIONS
                                        2.6 RÉFÉRENCES
                                        3 CRITÈRES DE CONCEPTION ET INFORMATIONS GENERALES SUR LES EMBALLAGES
                                        3.1 OBJECTIFS DE L’EMBALLAGE
                                        3.1.1 ENTREPOSAGE DU COMBUSTIBLE USE
                                        3.1.2 FONCTIONS DE SURETE DE L’EMBALLAGE
                                        3.2 CONCEPTION
                                        3.2.1 CONSIDÉRATIONS GÉNÉRALES
                                        3.2.2 DONNÉES DE CONCEPTION RELATIVES AUX VALEURS D’AMBIANCE ET PHÉNOMÈNES NATURELS
                                        3.2.2.1 Valeurs d’ambiance
                                        3.2.2.2 Séisme
                                        3.2.2.3 Inondation
                                        3.2.2.4 Autres phénomènes naturels
                                        3.2.3 CRITÈRES DE RÉSISTANCE
                                        3.2.3.1 Accidents en condition de transport sur site
                                        3.2.3.2 Accidents liés à la manutention
                                        3.2.3.3 Accident en phase d’entreposage
                                        3.2.3.4 Programme de vieillissement
                                        3.2.4 CRITÈRES DE CONCEPTION FONCTIONNELS
                                        3.2.4.1 Confinement des matières
                                        3.2.4.2 Évacuation de la puissance résiduelle
                                        3.2.4.3 Maintien de la sous-criticité
                                        3.2.4.4 Protection radiologique
                                        3.3 SYSTÈMES DE PROTECTIONS
                                        3.3.1 GÉNÉRAL
                                        3.3.2 PROTECTION PAR INTERPOSITION DE BARRIÈRES ET SYSTÈMES DE CONFINEMENT
                                        3.3.2.1 Barrières et systèmes de confinement
                                        3.3.2.2 Refroidissement de l’emballage
                                        3.3.3 PROTECTION PAR LA SÉLECTION D’INSTRUMENTS ET D’ÉQUIPEMENTS
                                        3.3.3.1 Équipements
                                        3.3.3.2 Instrumentation
                                        3.3.4 SURETÉ-CRITICITÉ DE L’EMBALLAGE
                                        3.3.4.1 Méthodes de contrôle pour la prévention de la criticité
                                        3.3.4.2 Incertitudes
                                        3.3.4.3 Vérifications
                                        3.3.5 PROTECTION RADIOLOGIQUE
                                        3.3.6 PROTECTION CONTRE L’INCENDIE ET L’EXPLOSION
                                        3.3.7 ENTREPOSAGE ET MANUTENTION
                                        3.4 DESCRIPTION DE LA CHAISE D’ENTREPOSAGE
                                        3.4.1 INTRODUCTION
                                        3.4.2 UTILISATION DE LA CHAISE D’ENTREPOSAGE
                                        3.5 RÉFÉRENCES
                                        4 SYSTÈME D’ENTREPOSAGE
                                        4.1 EMPLACEMENT ET AMÉNAGEMENT
                                        4.2 DESCRIPTION DU SF²
                                        4.2.1 DESCRIPTION DE L’INSTALLATION
                                        4.2.1.1 Bâtiment principal
                                        4.2.1.2 Bâtiment auxiliaire
                                        4.2.1.3 Bâtiment de stockage des accessoires (ASB)
                                        4.2.1.4 Pont roulant du bâtiment principal
                                        4.2.1.5 Porte blindée
                                        4.2.1.6 Stand d’inspection
                                        4.2.1.7 Ventilation passive
                                        4.2.2 GÉNIE CIVIL DU SF²
                                        4.2.2.1 Structures
                                        4.2.3 DESCRIPTION DES EMBALLAGES
                                        4.2.4 DESCRIPTION DU SYSTÈME DE SURVEILLANCE
                                        4.2.4.1 Description du système de surveillance de l’étanchéité des emballages
                                        4.2.4.2 Description du système de surveillance autre que de l’étanchéité des emballages
                                        4.2.4.3 Surveillance de la température ambiante dans le hall d’entreposage
                                        4.2.4.4 Système de radioprotection
                                        4.3 SYSTÈME DE TRANSPORT INTERNE DES EMBALLAGES SUR SITE
                                        4.3.1 REMORQUE ET CHASSIS DE TRANSPORT INTERNE
                                        4.4 SYSTÈMES D’EXPLOITATION
                                        4.4.1 CHARGEMENT ET DÉCHARGEMENT DU COMBUSTIBLE USÉ
                                        4.4.2 SYSTÈME DE DÉCONTAMINATION
                                        4.4.3 MAINTENANCE ET RÉPARATION DES EMBALLAGES
                                        4.4.4 CIRCUITS SUPPORT
                                        4.4.4.1 Air comprimé
                                        4.4.4.2 Système électrique
                                        4.4.4.3 Systèmes d’approvisionnement en eau
                                        4.4.4.4 Système d’Égouttage
                                        4.4.4.5 Système de drainage du kérosène
                                        4.4.5 AUTRES SYSTÈMES
                                        4.4.5.1 Ventilation
                                        4.4.5.2 Protection incendie
                                        4.5 CLASSIFICATION DES STRUCTURES, SYSTÈMES ET ÉQUIPEMENTS (SSC)
                                        4.5.1 MÉTHODOLOGIE
                                        4.5.2 EXIGENCES PAR CATÉGORIE
                                        4.5.3 CLASSIFICATION SISMIQUE
                                        4.6 DÉMANTÈLEMENT
                                        4.7 PROGRAMME DE VIEILLISSEMENT
                                        4.8 RÉFÉRENCES
                                        5 DESCRIPTION DES OPÉRATIONS
                                        5.1 DESCRIPTION DES OPÉRATIONS D’EXPLOITATION EN CONDITIONS NORMALES
                                        5.1.1 RÉCEPTION ET PRÉPARATION D’UN EMBALLAGE NEUF
                                        5.1.2 RÉCEPTION ET PRÉPARATION D’UN EMBALLAGE CHARGÉ EN VUE DE L’ENTREPOSAGE
                                        5.1.3 MISE EN PLACE D’UN EMBALLAGE CHARGÉ À SON EMPLACEMENT D’ENTREPOSAGE
                                        5.1.4 CONTRÔLE ET MAINTENANCE DES EMBALLAGES EN COURS D’ENTREPOSAGE
                                        5.1.5 REPRISE DES EMBALLAGES ET MISE EN CONFIGURATION TRANSPORT
                                        5.2 DESCRIPTION DES OPÉRATIONS D’EXPLOITATION EN CONDITIONS INCIDENTELLES ET ACCIDENTELLES
                                        5.2.1 OPÉRATIONS À RÉALISER SUITE À LA DÉTECTION D’UNE PERTE D’ÉTANCHÉITÉ D’UN EMBALLAGE
                                        5.2.2 RENVOI D’UN EMBALLAGE AU BÂTIMENT DE POUR INTERVENTION
                                        5.3 DESCRIPTION DES OPÉRATIONS DE MAINTENANCE LIÉES À LA SÛRETÉ
                                        5.3.1 INSTRUMENTATION
                                        5.3.2 TECHNIQUES D’ENTRETIEN
                                        5.4 PANNEAU D’ALARMES
                                        5.5 COMPTABILITÉ DE L’INVENTAIRE DES ASSEMBLAGES DE COMBUSTIBLE USÉ
                                        5.6 TRANSPORT INTERNE DES ASSEMBLAGES DE COMBUSTIBLE USÉ
                                        5.7 RÉFÉRENCES
                                        6 CONFINEMENT ET GESTION DES DÉCHETS RADIOACTIFS GÉNÉRÉS
                                        6.1 RÉFÉRENCES
                                        7 PROTECTION CONTRE LES RADIATIONS
                                        7.1 DISPOSITION CONDUISANT À DES DOSES AU PERSONNEL AUSSI RÉDUITES QUE POSSIBLE
                                        7.1.1 DISPOSITIONS ADMINISTRATIVES ET ORGANISATIONNELLES
                                        7.1.2 CONSIDÉRATIONS RELATIVES À LA CONCEPTION
                                        7.1.3 CONSIDÉRATIONS RELATIVES À L’EXPLOITATION
                                        7.2 SOURCES RADIOACTIVES
                                        7.2.1 CARACTÉRISATION DES SOURCES
                                        7.2.2 SOURCES DE MATIÈRES RADIOACTIVES DISPERSÉES DANS L’AIR
                                        7.3 CARACTÉRISTIQUES DE CONCEPTION DU SYSTÈME DE PROTECTION CONTRE LES RADIATIONS
                                        7.3.1 CARACTÉRISTIQUES DE LA CONCEPTION
                                        7.3.2 BLINDAGE
                                        7.3.2.1 Débit de dose émis par l’emballage
                                        7.3.2.2 Débit de dose émis par le SF² en conditions normales
                                        7.3.3 INSTRUMENTATION DE CONTRÔLE DES RADIATIONS DES LOCAUX
                                        7.4 ÉVALUATION DES DOSES COLLECTIVES ESTIMÉES AU SEIN DU SF²
                                        7.5 DOSE COLLECTIVE HORS DU SF²
                                        7.6 PROGRAMME DE PROTECTION CONTRE LES RADIATIONS
                                        7.7 PROGRAMME DE MONITORING ENVIRONNEMENTAL
                                        7.8 RÉFÉRENCES
                                        8 ANALYSE DES INCIDENTS ET ACCIDENTS
                                        8.1 CONSIDÉRATIONS GÉNÉRALES
                                        8.1.1 CATÉGORISATION DES ÉVÉNEMENTS SELON L’AFCN
                                        8.1.1.1 Classe C1 : fonctionnement normal
                                        8.1.1.2 Classe C2 : événements opérationels anticipés
                                        8.1.1.3 Classe C3A : événements issus d’une défaillance simple
                                        8.1.1.4 Classe C3B : événements issus de défaillances multiples
                                        8.1.1.5 Classe C4 : accidents graves
                                        8.1.2 OBJECTIFS DE SÛRETÉ
                                        8.1.2.1 Objectifs de sûreté SO1
                                        8.1.2.2 Objectifs de sûreté SO2
                                        8.1.2.3 Objectifs de sûreté SO3
                                        8.1.3 DÉFINITION DE L’ÉTAT SUR À ATTEINDRE APRÈS UN ÉVÉNEMENT INCIDENTEL ET ACCIDENTEL
                                        8.2 ÉVÉNEMENTS INTERNES
                                        8.2.1 SITUATIONS INCIDENTELLES (C2)
                                        8.2.1.1 Perte d’étanchéité de l’emballage
                                        8.2.1.2 Dégradation de la protection radiologique de l’emballage
                                        8.2.1.3 Perte de l’alimentation électrique externe
                                        8.2.1.4 Choc d’un emballage avec un emballage manutentionné
                                        8.2.2 SITUATIONS ACCIDENTELLES (C3a)
                                        8.2.2.1 Dégradation de la fonction d’évacuation de la chaleur résiduelle
                                        8.2.2.2 Inondation interne
                                        8.2.2.3 Incendie interne
                                        8.2.2.4 Explosion interne
                                        8.2.2.5 Missile interne
                                        8.2.3 SITUATIONS ACCIDENTELLES (C3b)
                                        8.2.3.1 Chute de l’emballage
                                        8.2.4 SITUATIONS ACCIDENTELLES (C4a)
                                        8.2.5 SITUATIONS ACCIDENTELLES ÉLIMINÈES PRATIQUEMENT (C4b)
                                        8.3 ÉVÉNEMENTS EXTERNES
                                        8.3.1 APPROCHE GRADUÉE POUR LES ÉVÉNEMENTS EXTERNES
                                        8.3.1.1 Méthodologie
                                        8.3.1.2 Périmètre de l’analyse
                                        8.3.1.3 Événements considérés
                                        8.3.2 ÉVÉNEMENTS GA-1
                                        8.3.2.1 Impact du vent sur la ventilation naturelle
                                        8.3.2.2 Température extrême basse
                                        8.3.2.3 Inondation externe
                                        8.3.2.4 Pluie extrême
                                        8.3.2.5 Foudre
                                        8.3.2.6 Incendie externe
                                        8.3.2.7 Produits toxiques
                                        8.3.2.8 Eaux souterraines
                                        8.3.3 ÉVÉNEMENTS GA-2
                                        8.3.3.1 Grêle
                                        8.3.3.2 Missile externe
                                        8.3.3.3 Neige/glace
                                        8.3.4 ÉVÉNEMENTS GA-3
                                        8.3.4.1 Tornade/vent
                                        8.3.5. ÉVÉNEMENTS GA-4
                                        8.3.5.1 Chute d’avion
                                        8.3.5.2 Explosion externe
                                        8.3.5.3 Séisme
                                        8.3.5.4 Enfouissement de l’emballage
                                        8.3.5.5 Température extrême haute
                                        8.3.6. MESURES DE REMÉDIATIONS
                                        8.3.6.1 Description du plan de remédiation
                                        8.3.6.2 Principaux équipements nécessaires
                                        8.4 COMBINAISONS D’ÉVÉNEMENTS INDEPENDANTS
                                        8.5 RÉFÉRENCES
                                        9 EXPLOITATION
                                        9.1 ORGANISATION
                                        9.2 PROGRAMME D’ESSAI
                                        9.3 PROGRAMME DE FORMATION
                                        9.4 PROCÉDURES D’EXPLOITATION
                                        9.5 PLAN D’URGENCE
                                        9.6 RÉFÉRENCES
                                        10 LIMITES D’EXPLOITATION
                                        10.1 LIMITES ET CONDITIONS D'EXPLOITATION
                                        10.1.1 LIMITES ET CONDITIONS D’EXPLOITATION RELATIVES AUX EMBALLAGES
                                        10.1.2 LIMITES ET CONDITIONS D’EXPLOITATION RELATIVES AU SF²
                                        10.1.3 LIMITES ET CONDITIONS D’EXPLOITATION RELATIVES AUX OPÉRATIONS
                                        10.2 SPÉCIFICATIONS TECHNIQUES D’EXPLOITATION
                                        11 ASSURANCE DE LA QUALITÉ
                                        11.1 PROGRAMME D’ASSURANCE QUALITÉ DES FOURNISSEURS D’EMBALLAGES
                                        11.2 PROGRAMME D’ASSURANCE QUALITÉ CHEZ L’EXPLOITANT
                                        11.3 ASSURANCE DE LA QUALITÉ DURANT LES PHASES DE CONCEPTION ET DE CONSTRUCTION
                                        11.4 EXIGENCES DE QUALITE SELON LA CLASSIFICATION
                                        11.5 RÉFÉRENCES
                                        """;

        
        // This simply uses the example list of sections as the document outline without LLM Processing.
        documentOutlineLines = orderedSectionListFrench.Split("\n").ToList();

        var sectionDictionary = new Dictionary<string, string>();

        // Foreach line in the lines List, remove quotes as well as leading and trailing whitespace
        documentOutlineLines = documentOutlineLines.Select(x => x.Trim([' ', '"', '-'])
                .Replace("[", "")
                .Replace("]", ""))
            .ToList();

        // Remove any empty lines
        documentOutlineLines = documentOutlineLines.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        // Create a dictionary that contains the section number (1, 1.1, 1.1.1, etc) as the key and the rest of the line as the value.
        foreach (var line in documentOutlineLines)
        {
            var sectionNumber = line.Split(' ')[0];
            var sectionTitle = line.Substring(sectionNumber.Length).Trim();
            sectionDictionary.Add(sectionNumber, sectionTitle);
        }

        // Remove any trailing periods from the section numbers
        sectionDictionary = sectionDictionary.ToDictionary(x => x.Key.TrimEnd('.'), x => x.Value);

        // Use the structure of the sections to determine a hierarchy - 1.1 is a child of 1, 1.1.1 is a child of 1.1, etc. Use this to create a tree of ContentNodes.
        // The ContentNodes will have a Text element that should contain the whole title - "1.1.1 Title" for example.
        // The Type should be Title for the top level, and Heading for the rest.
        // The Children should be a list of ContentNodes that are children of the current node.
        var contentNodeList = new List<ContentNode>();
        Dictionary<string, ContentNode> lastNodeAtLevel = new Dictionary<string, ContentNode>();

        foreach (var section in sectionDictionary)
        {
            var levels = section.Key.Split('.');
            var depth = levels.Length;
            var parentNodeKey = string.Join(".", levels.Take(depth - 1)); // Get parent node key by joining all but the last level

            ContentNode? parentNode;
            if (depth == 1)
            {
                // This is a top-level node
                parentNode = null; // No parent
            }
            else if (!lastNodeAtLevel.TryGetValue(parentNodeKey, out parentNode))
            {
                // Parent node does not exist, which should not happen if input is correctly structured
                continue; // Or handle error
            }

            var currentNode = new ContentNode
            {
                Id = Guid.NewGuid(),
                Text = $"{section.Key} {section.Value}",
                Type = depth == 1 ? ContentNodeType.Title : ContentNodeType.Heading,
                GenerationState = ContentNodeGenerationState.OutlineOnly,
                Children = []
            };

            if (parentNode != null)
            {
                parentNode.Children.Add(currentNode);
                currentNode.ParentId = parentNode.Id;
            }
            else
            {
                currentNode.ParentId = null;
                contentNodeList.Add(currentNode);
            }

            lastNodeAtLevel[section.Key] = currentNode; // Update the last node at this level
            _dbContext.ContentNodes.Add(currentNode);
        }

        await _dbContext.SaveChangesAsync();

        _dbContext.Attach(generatedDocument);
        generatedDocument.ContentNodes = contentNodeList;

        // Update the generated document in  the database
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("BelgiumNuclearLicensingDSARDocumentOutlineService : Document Outline Generated for Document ID {DocumentId}", generatedDocument.Id);

        return contentNodeList;
    }
}
