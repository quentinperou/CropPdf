<p align="center">
  <img src="docs/icon-256.png" width="96" alt="Icône de Crop PDF">
</p>

<h1 align="center">Crop PDF</h1>

<p align="center">
  Recadrer une page de PDF en deux clics, directement depuis le menu clic droit de Windows 11.
</p>

<p align="center">
  <a href="README.md">English</a> · <b>Français</b>
</p>

---

## Fonctionnalités

- **Clic droit sur un PDF → « Recadrer le PDF »**, dans le nouveau menu contextuel de Windows 11 (et dans « Afficher plus d'options » / sous Windows 10).
- Toutes les pages s'affichent : on **trace un rectangle** sur la page voulue, puis on peut le déplacer et le redimensionner par ses poignées.
- Deux façons d'enregistrer :
  - **Enregistrer une copie** : crée `nom_crop.pdf` à côté de l'original (`nom_crop_2.pdf`… si le nom est déjà pris) ;
  - **Écraser l'original** : remplace le fichier d'origine (confirmation demandée s'il a plusieurs pages).
- **Le fichier produit ne contient que la page recadrée** : les autres pages sont retirées.
- **Impression directe de la zone recadrée**, sans rien enregistrer (bouton imprimante ou `Ctrl+P`) :
  - *ajuster au papier* : remplit la feuille ou l'étiquette, pivotée d'un quart de tour si elle rentre mieux
    (idéal pour les étiquettes d'expédition sur une imprimante d'étiquettes) ;
  - *taille réelle* : 100 %, centrée, réduite seulement si elle dépasse du papier.
- Recadrage sans perte : le texte reste sélectionnable et le vectoriel reste vectoriel.
- Interface Windows 11 (Fluent, Mica) qui **suit le thème clair/sombre** et la couleur d'accentuation de Windows.
- **Français et anglais** : l'appli, le menu clic droit et l'installeur suivent la langue d'affichage de Windows ;
  la langue de l'appli peut aussi être choisie dans les **Paramètres** (icône d'engrenage).

### Raccourcis

| Action | Raccourci |
|--------|-----------|
| Ouvrir un PDF | `Ctrl+O` (ou glisser-déposer le fichier dans la fenêtre) |
| Enregistrer une copie | `Ctrl+S` |
| Imprimer la zone sélectionnée | `Ctrl+P` (avec le dernier mode d'impression utilisé) |
| Effacer la sélection | `Échap` |
| Zoomer | `Ctrl` + molette, `Ctrl` + `+` ou `Ctrl` + `-` |
| Ajuster à la fenêtre | `Ctrl+0` |
| Paramètres | `Ctrl+,` |

> **Bon à savoir :** comme dans Acrobat, le recadrage *masque* ce qui dépasse de la zone, il ne
> l'efface pas du fichier. Pour supprimer une information confidentielle, il faut un outil de caviardage.

## Installation

1. Téléchargez `CropPdf-Setup-x.y.z.exe` depuis la page [Releases](../../releases).
2. Lancez-le. L'installeur n'étant pas signé, Windows SmartScreen peut afficher « Windows a protégé votre ordinateur » :
   cliquez sur **Informations complémentaires → Exécuter quand même**.
3. Acceptez la demande d'autorisation administrateur (UAC).

L'installeur :

- copie l'application dans `C:\Program Files\Crop PDF` et l'ajoute au menu Démarrer ;
- ajoute l'entrée « Recadrer le PDF » au menu clic droit des fichiers `.pdf` ;
- propose d'installer le **.NET 10 Desktop Runtime** (Microsoft) s'il est absent.

Pour l'enlever : **Paramètres → Applications → Applications installées → Crop PDF**.

**Configuration requise :** Windows 11 x64 pour le nouveau menu contextuel (Windows 10 2004+ fonctionne, avec l'entrée dans le menu classique).

## Développer

### Prérequis

Tout s'installe avec `winget` (inclus dans Windows 11), dans un terminal :

| Outil | Rôle | Installation |
|-------|------|--------------|
| **Git** | Récupérer le code | `winget install Git.Git` |
| **SDK .NET 10** | Compiler l'application (C# / WPF) et lancer les tests | `winget install Microsoft.DotNet.SDK.10` |
| **Visual Studio Build Tools 2026**, charge de travail « Développement Desktop en C++ » | Compiler la DLL du menu contextuel (MSVC, CMake, Ninja) et créer le package MSIX (`makeappx`, Windows SDK 10.0.26100) | voir ci-dessous |
| **Inno Setup 6** | Générer l'installeur | `winget install JRSoftware.InnoSetup` |

```powershell
winget install Microsoft.VisualStudio.BuildTools --override "--quiet --wait --norestart --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended"
```

Visual Studio 2026 (Community ou plus) avec la même charge de travail convient aussi. Un éditeur au choix
(Visual Studio, VS Code, Rider) suffit pour le C#.

### Compiler

```powershell
.\scripts\build.ps1
```

Le script lance les tests puis produit dans `dist\` :

| Fichier | Contenu |
|---------|---------|
| `app\` | l'application (`CropPdf.exe`…) et la DLL du menu (`CropPdfMenu.dll`) |
| `CropPdf.Menu.msix` | le package « sparse » qui déclare l'entrée du menu Windows 11 |
| `CropPdf-Setup-<version>.exe` | l'installeur |

Options : `-SkipTests`, `-NoInstaller`.

### Pendant le développement

```powershell
dotnet test tests\CropPdf.Tests                                 # tests unitaires
dotnet run --project src\CropPdf -- "C:\chemin\fichier.pdf"     # lancer l'appli sur un PDF
```

Pour vérifier l'autre thème ou l'autre langue sans rien changer aux réglages, définissez la variable
d'environnement `CROPPDF_THEME` (`Light` ou `Dark`) ou `CROPPDF_LANG` (`en` ou `fr`) avant de lancer l'appli.

Le menu clic droit ne peut se tester qu'**installé** : lancez `dist\CropPdf-Setup-<version>.exe`
(une réinstallation remplace la précédente).

Deux petits outils aident :

```powershell
dotnet run --project tools\SamplePdf -- C:\temp\exemple.pdf    # PDF de test de 3 pages (page pivotée, CropBox existante)
dotnet run --project tools\InvokeMenu -- C:\temp\exemple.pdf   # lance la commande du menu installé, comme l'Explorateur
```

### Organisation du code

```
src/
  CropPdf.Core/     Logique de recadrage, sans interface (PDFsharp) : conversion des coordonnées, écriture du PDF
  CropPdf/          Application WPF : affichage des pages (Windows.Data.Pdf), sélection, enregistrement, paramètres, textes
  CropPdf.Shell/    DLL C++ du menu contextuel Windows 11 (IExplorerCommand), avec son libellé traduit
package/            Manifeste et logos du package « sparse » (identité de l'appli pour le menu Windows 11)
installer/          Script Inno Setup de l'installeur
scripts/            build.ps1 (compilation complète), make-icons.ps1 (régénère les icônes)
tests/              Tests unitaires (xUnit)
docs/               Icône du README
.github/workflows/  Compilation automatique sur GitHub (et publication des Releases)
tools/              Outils de dev : SamplePdf (PDF de test), InvokeMenu (lance la commande du menu installé)
```

### Traductions

| Quoi | Où |
|------|----|
| Textes de l'appli | `src/CropPdf/Properties/Strings.resx` (anglais, langue par défaut) et `Strings.fr.resx` (français). Les tests vérifient que les deux ont les mêmes clés et les mêmes `{0}`. |
| Libellé du menu clic droit | Table de chaînes de `src/CropPdf.Shell/CropPdfMenu.rc` |
| Messages de l'installeur | `[Languages]` et `[CustomMessages]` dans `installer/CropPdf.iss` |
| Langues proposées dans les Paramètres | `AppSettings.SupportedLanguages` dans `src/CropPdf/AppSettings.cs` |

Ajouter une langue, c'est compléter ces quatre endroits (et, si on veut, un `README.<langue>.md`).

### Comment fonctionne le menu Windows 11

Le nouveau menu contextuel n'accepte que les applications ayant une **identité de package**. L'appli
reste une application classique, mais l'installeur enregistre un petit package « sparse »
(`package/AppxManifest.xml`) qui pointe vers le dossier d'installation et déclare la DLL
`CropPdfMenu.dll`, qui demande à l'Explorateur de lancer `CropPdf.exe` sur le fichier choisi. Ce package
n'est **pas signé** : Windows 11 l'accepte avec `Add-AppxPackage -AllowUnsigned` depuis une session
administrateur, d'où la demande UAC à l'installation.

### Publier une version

1. Mettez à jour `<Version>` dans `Directory.Build.props`.
2. Committez, puis créez et poussez un tag : `git tag v1.2.0` puis `git push origin v1.2.0`.
3. Le workflow GitHub Actions compile, teste et publie l'installeur dans une Release.

## Feuille de route

- [ ] **Mises à jour** : au démarrage (au plus une fois par jour), interroger les Releases GitHub ; si une version plus récente existe, l'annoncer dans l'appli avec un bouton « Mettre à jour » qui télécharge et lance le nouvel installeur.
- [ ] **Signature gratuite** de l'installeur et du package via [SignPath Foundation](https://signpath.org) (réservé aux projets open source ayant déjà une release) : plus d'avertissement SmartScreen, et plus besoin de droits administrateur pour le menu.
- [ ] Éventuellement, publication sur le **Microsoft Store** (gratuit pour les particuliers, le Store signe l'application) ; le [Windows App Certification Kit](https://learn.microsoft.com/fr-fr/windows/uwp/debug-test-perf/windows-app-certification-kit) permet de vérifier l'appli avant soumission ([action GitHub](https://github.com/marketplace/actions/run-the-windows-app-certification-kit)).
- [ ] **Aperçu avant impression** dans la fenêtre d'impression de Windows 11. Elle affiche pour l'instant « Cette application ne prend pas en charge l'aperçu avant impression » : WPF imprime avec l'ancienne interface d'impression de Windows, qui ne peut pas fournir de pages d'aperçu. Il faut passer par l'interface moderne (`PrintManager` + `IPrintDocumentSource`, pages d'aperçu dessinées avec Direct2D).
- [ ] Version ARM64.
- [ ] D'autres langues.

## Licence

[MIT](LICENSE.md). Utilise [PDFsharp](https://github.com/empira/PDFsharp) (MIT).

## Crédits

Développé par [QuentinPerou](https://github.com/quentinperou), avec [Claude Code](https://claude.com/claude-code) (Anthropic).
