# Captain of the Seas — Launcher (Velopack + GitHub Releases)

Deze map bevat een kant-en-klaar opzet voor een eigen launcher (Avalonia, C#, .NET 8,
cross-platform Windows + Mac), met jouw achtergrond en logo verwerkt. De launcher zelf
update zichzelf via **Velopack**, en downloadt/start de **Unity-game** via een losse
GitHub-Release van de gebouwde game (los van de launcher's eigen releases).

Twee losse dingen die je moet opzetten, hieronder in twee delen:

1. De Unity-build in een GitHub Release krijgen (zodat de launcher 'm kan downloaden)
2. De launcher zelf builden, packagen met Velopack en publiceren

---

## Deel 1 — Unity build in een GitHub Release

Aanbevolen: gebruik een **apart** (eventueel privé) repo puur voor de gebouwde builds,
bijvoorbeeld `captain-seas-builds` — niet dezelfde repo als je broncode, dat houdt de
repo licht (geen dikke zip's in je git-historie via commits, alleen als Release-asset).

### Eenmalig

1. Maak op github.com een nieuwe (leeg) repo aan, bv. `jouwgebruikersnaam/captain-seas-builds`.
   Kan **privé**, gratis account is prima — Releases + assets werken ook op privé repo's
   (je hebt dan wel een GitHub token nodig om te downloaden, zie opmerking onderaan Deel 1).
2. Installeer de GitHub CLI als je 'm nog niet hebt: `brew install gh`, daarna eenmalig
   `gh auth login`.

### Per build die je wilt uitbrengen

1. **Build de Unity-game** voor Windows en/of Mac via `File > Build Settings` in de Unity
   Editor (Platform: Windows of macOS, Build).
2. **Zip elke platform-build** los, met exact de naam die de launcher verwacht
   (zie `Services/GameUpdateService.cs`, standaard ingesteld op):
   - `CaptainSeas-Win64.zip` (hele inhoud van de Windows build-map, inclusief de `.exe`
     en de `_Data`-map, in de root van de zip — niet in een submapje)
   - `CaptainSeas-macOS.zip` (de hele `.app`-bundle in de root van de zip)
3. **Tag en publiceer een release** met de GitHub CLI, vanuit de map met je twee zip's:

   ```bash
   cd pad/naar/de/build-zips
   gh release create v1.0.0 \
     CaptainSeas-Win64.zip \
     CaptainSeas-macOS.zip \
     --repo jouwgebruikersnaam/captain-seas-builds \
     --title "v1.0.0" \
     --notes "Eerste testbuild"
   ```

   Volgende keer verhoog je gewoon het versienummer (`v1.0.1`, `v1.1.0`, ...) — de launcher
   vergelijkt dit tagnummer met wat de speler lokaal heeft en download automatisch de
   nieuwste als het verschilt.

   Zonder GitHub CLI kan het ook via de website: repo → "Releases" → "Draft a new release"
   → tag invullen → de twee zip's er in slepen → "Publish release".

4. Zet in `Services/GameUpdateService.cs` de `GitHubOwner`/`GitHubRepo` op jouw
   `captain-seas-builds`-repo (staat nu op `TODO-...`).

**Opmerking privé repo's:** de GitHub Releases API voor een privé repo vereist een
geauthenticeerde request (een Personal Access Token in de `Authorization`-header). Voor nu
met een paar downloads is het simpelst om de builds-repo gewoon **publiek** te zetten (de
broncode van je game staat er niet in, alleen de gebouwde zip's) — dan werkt de launcher-code
in deze map zonder aanpassing. Wil je 'm toch privé houden, zeg het en dan breid ik
`GameUpdateService.cs` uit met een token.

---

## Deel 2 — De launcher builden en publiceren met Velopack

De launcher-code staat in `CaptainSeasLauncher/` (Avalonia-project). Open deze map met
Rider of Visual Studio (met de Avalonia-extensie), of gebruik puur de `dotnet`-CLI.

### Eenmalig

```bash
# .NET 8 SDK nodig (https://dotnet.microsoft.com/download)
cd Launcher/CaptainSeasLauncher
dotnet restore

# Velopack's packaging-tool (vpk) globaal installeren
dotnet tool install -g vpk
```

### Vul de TODO's in

- `Services/GameUpdateService.cs`: `GitHubOwner`/`GitHubRepo` (zie Deel 1), en de
  asset-/exe-namen als je van de standaardnamen afwijkt.
- Wil je dat de launcher zelf ook via GitHub auto-update? Maak dan ook een repo voor de
  launcher zelf, bv. `captain-seas-launcher`, en gebruik die hieronder bij `vpk upload github`.

### Builden + packagen (Windows-build, macOS-build is los)

Velopack wil een self-contained publish per platform, en pakt die daarna in tot een
installer + auto-update package:

**Windows build (op een Windows-machine of via CI):**
```bash
dotnet publish -c Release -r win-x64 --self-contained -o publish/win-x64

vpk pack -u CaptainSeasLauncher -v 1.0.0 -o releases/win ^
  -p publish/win-x64 -e CaptainSeasLauncher.exe --icon Assets/logo.png
```

**macOS build (op een Mac):**
```bash
dotnet publish -c Release -r osx-arm64 --self-contained -o publish/osx-arm64
# (draai ook -r osx-x64 als je ook Intel-Macs wilt ondersteunen)

vpk pack -u CaptainSeasLauncher -v 1.0.0 -o releases/mac \
  -p publish/osx-arm64 -e CaptainSeasLauncher --icon Assets/logo.png
```

Dit levert per platform een map op (`releases/win`, `releases/mac`) met een installer
(`CaptainSeasLauncherSetup.exe` op Windows, een `.pkg`/`.zip` op Mac) plus de Velopack
update-bestanden.

### Publiceren naar GitHub (zodat de launcher zichzelf kan updaten)

```bash
vpk upload github --repoUrl https://github.com/jouwgebruikersnaam/captain-seas-launcher \
  --tag v1.0.0 --publish --releaseName "v1.0.0" -o releases/win
```

(los herhalen voor `releases/mac`, of los repo per platform als je dat overzichtelijker vindt)

### Uitdelen aan je testers

Voor nu, met een paar downloads: stuur ze gewoon de link naar de laatste Release-asset op
je `captain-seas-launcher`-repo (de Setup.exe / het .pkg-bestand). Ze installeren die ene
keer, en daarna update de launcher zichzelf via Velopack, en downloadt/update hij de game
via de `captain-seas-builds`-repo (Deel 1).

---

## Overzicht van de architectuur

```
Speler installeert eenmalig:  CaptainSeasLauncherSetup.exe / .pkg
                                        │
                          (Velopack: auto-update van de LAUNCHER zelf)
                                        │
                                        ▼
                          Launcher checkt "captain-seas-builds" repo
                                        │
                          (GameUpdateService: download + unzip GAME)
                                        │
                                        ▼
                              CaptainSeas.exe / CaptainSeas.app
```

Twee losse update-kanalen met opzet: de launcher (UI/branding/downloadlogica) en de game
(de Unity-build) hebben een ander release-tempo, dus die verdienen allebei hun eigen
GitHub-repo en versienummer.

---

## Nieuw: GitHub-repo's aanmaken, launcher-zelfupdate en verwijderen

Je hebt nog geen GitHub-repo's staan — hieronder de twee die je nodig hebt, plus wat er nu
bij is gekomen in de launcher: een "Verwijderen"-knop en een zelfupdate-banner.

### Beide repo's aanmaken

Je hebt er **twee** nodig (met opzet gescheiden, zie het architectuur-schema hierboven):

```bash
# 1. Voor de gebouwde Unity-builds (zips)
gh repo create captain-seas-builds --public --description "Gebouwde Windows/Mac builds van Captain of the Seas"

# 2. Voor de launcher zelf (broncode + Velopack-releases)
gh repo create captain-seas-launcher --public --description "Launcher voor Captain of the Seas"
```

(Geen `gh`? Kan ook via github.com → "New repository", zelfde namen, dan hieronder verder.)

Zonder GitHub CLI, of als je liever alles los beheert: geen probleem, de naam maakt niet uit
zolang je 'm consistent hieronder invult.

### De launcher-broncode zelf naar GitHub pushen

```bash
cd Launcher/CaptainSeasLauncher/..      # naar de Launcher-map (met .gitignore)
git init
git add .
git commit -m "Eerste versie van de Captain Seas launcher"
git branch -M main
git remote add origin https://github.com/jouwgebruikersnaam/captain-seas-launcher.git
git push -u origin main
```

### TODO's invullen

Nu je de namen weet, vul ze in op deze twee plekken:

- `Services/GameUpdateService.cs` → `GitHubOwner` / `GitHubRepo` → `captain-seas-builds`
- `Services/LauncherUpdateService.cs` → `RepoUrl` → `https://github.com/jouwgebruikersnaam/captain-seas-launcher`

### Launcher-zelfupdate publiceren

Elke keer dat je de launcher zelf aanpast en een nieuwe versie wilt uitrollen naar mensen die
'm al hebben geïnstalleerd:

```bash
dotnet publish -c Release -r win-x64 --self-contained -o publish/win-x64
vpk pack -u CaptainSeasLauncher -v 1.1.0 -o releases/win -p publish/win-x64 -e CaptainSeasLauncher.exe --icon Assets/AppIcon.ico
vpk upload github --repoUrl https://github.com/jouwgebruikersnaam/captain-seas-launcher --tag v1.1.0 --publish -o releases/win

dotnet publish -c Release -r osx-arm64 --self-contained -o publish/osx-arm64
vpk pack -u CaptainSeasLauncher -v 1.1.0 -o releases/mac -p publish/osx-arm64 -e CaptainSeasLauncher --icon Assets/AppIcon.icns --noInst
vpk upload github --repoUrl https://github.com/jouwgebruikersnaam/captain-seas-launcher --tag v1.1.0 --publish -o releases/mac
```

Spelers die de launcher al open hebben staan, zien dan automatisch (bij de volgende keer
opstarten) rechtsboven een banner "Nieuwe versie van de launcher beschikbaar" met een
knop **UPDATE LAUNCHER**. Pas ná die klik wordt de update gedownload en herstart de launcher
zichzelf — er gebeurt nooit iets stilzwijgend op de achtergrond.

Let op: dit werkt alleen voor de **geïnstalleerde** versie (via Setup.exe/de .app), niet als
je de launcher via `dotnet run` start tijdens ontwikkelen — dan slaat de check zichzelf gewoon
over (`IsInstalled` staat dan op false).

### "Verwijderen"-knop

Onderin, links van de SPEEL-knop, staat nu een kleine "Verwijderen..."-link. Na een
bevestigingsvraag:

1. Worden eerst de gedownloade game-bestanden verwijderd (de `GameData`-map).
2. Daarna de-installeert de launcher zichzelf:
   - **Windows**: via Velopack's eigen `Update.exe -s uninstall` — hetzelfde mechanisme als
     "Programma's en onderdelen", dus snelkoppelingen en registervermeldingen worden netjes
     opgeruimd.
   - **macOS**: omdat we hier bewust een portable `.app` publiceren (`--noInst`, zie eerder
     in dit document over de macOS-pkg-bug), verplaatst de launcher zichzelf naar de
     Prullenbak.

Dit werkt alleen bij een echt geïnstalleerde versie — start je de launcher via `dotnet run`
in development, dan krijg je een duidelijke foutmelding in plaats van een crash.

---

## Nieuw: Windows-build via GitHub Actions (i.p.v. lokaal op je Mac)

Bleek dat `vpk` op macOS alleen macOS-pakketten kan bouwen — écht cross-compilen naar een
Windows-installer vanaf een Mac lukt niet met deze tool. Oplossing: `.github/workflows/release.yml`
laat GitHub zelf een gratis Windows- én Mac-runner starten die alles automatisch bouwt en
publiceert, zodra je een versietag pusht.

### Gebruiken

```bash
cd Launcher
git add .github/workflows/release.yml
git commit -m "Add CI workflow for building and releasing the launcher"
git push

git tag v1.0.0
git push origin v1.0.0
```

Die laatste `git push origin v1.0.0` triggert de workflow. Volg 'm live op:
`github.com/MrLoman/captain-seas-launcher/actions`

Na een paar minuten (Windows + macOS builden parallel) staat de release compleet met beide
platforms op `github.com/MrLoman/captain-seas-launcher/releases` — inclusief het juiste
Windows-icoon (dat lukt nu wél, want deze build draait op een échte Windows-machine).

### Bij een volgende versie

Alleen dit twee-regelige stapje, verder niks lokaal builden/packen/uploaden meer nodig:

```bash
git tag v1.1.0
git push origin v1.1.0
```

Lokaal op je Mac (`dotnet run`) blijft gewoon werken om te testen tijdens het ontwikkelen —
alleen het **uitbrengen** van een release verloopt voortaan via deze workflow.
