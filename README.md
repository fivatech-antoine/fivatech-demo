# Transports Suisses - Dashboard — GTFS Demo

Dashboard temps réel pour les Transports en commun suisses, construit avec **ASP.NET Core (.NET 8)** et **React/Vite/TypeScript**.

## Fonctionnalités

- **Recherche d'arrêts** avec debounce (min. 2 caractères)
- **Tableau des prochains départs** fusionnant données statiques et temps réel (retards GTFS-RT), rafraîchi toutes les 30 s
- **Filtres dynamiques** sur le tableau : ligne, opérateur, direction
- **Carte Leaflet** avec visualisation de trajet : cliquer un départ trace la ligne sur la carte (polyline + arrêts restants jusqu'au terminus) et recentre la vue

## Architecture

```
gtfsdemo/
├── backend/GTFSDemo.Api/       # API REST ASP.NET Core
│   ├── Controllers/
│   │   ├── StopsController     # Recherche et détail d'arrêts
│   │   ├── DeparturesController# Prochains départs (statique + RT)
│   │   └── TripsController     # Arrêts restants d'un trajet
│   ├── Services/
│   │   ├── GtfsStaticService   # Charge & indexe le GTFS statique (ZIP) au démarrage
│   │   ├── GtfsRealtimeService # Refresh GTFS-RT toutes les 30 s (BackgroundService)
│   │   └── CacheService        # Wrapper IMemoryCache
│   ├── Models/                 # Stop, Departure, TripStop
│   ├── Configuration/          # Options OpenTransportData
│   └── Protos/                 # gtfs-realtime.proto → classes C# (Google.Protobuf)
└── frontend/gtfs-demo-app/     # React + Vite + TypeScript
    └── src/
        ├── components/
        │   ├── StopSearch/     # Recherche d'arrêts avec debounce
        │   ├── DepartureBoard/ # Tableau des prochains départs + filtres (refresh 30 s)
        │   └── VehicleMap/     # Carte Leaflet avec tracé du trajet sélectionné
        ├── services/api.ts     # Appels fetch vers le backend
        └── types/index.ts      # Types TS alignés sur les models .NET
```

## Prérequis

- .NET 8 SDK
- Node.js ≥ 20
- Clé API [opentransportdata.swiss](https://opentransportdata.swiss)

## Configuration

Éditez `backend/GTFSDemo.Api/appsettings.json` :

```json
{
  "OpenTransportData": {
    "ApiKey": "<votre_clé_api>",
    "GtfsStaticUrl": "<url_du_zip_gtfs_ou_url_dataset_ckan>",
    "GtfsRtTripUpdatesUrl": "<url_trip_updates>"
  }
}
```

> **Astuce :** `GtfsStaticUrl` accepte aussi l'URL d'une page de dataset CKAN (`.../dataset/{id}`). Le backend interroge alors l'API CKAN pour résoudre automatiquement l'URL de téléchargement du ZIP le plus récent.

## Démarrage

```bash
# Backend (port 5000)
cd backend/GTFSDemo.Api
dotnet run

# Frontend (port 5173, proxy → :5000)
cd frontend/gtfs-demo-app
npm install
npm run dev
```

Ouvrez [http://localhost:5173](http://localhost:5173).

## API Endpoints

| Méthode | Route | Description |
|---------|-------|-------------|
| GET | `/api/stops?query=flon` | Recherche d'arrêts |
| GET | `/api/stops/{stopId}` | Arrêt par ID |
| GET | `/api/departures/{stopId}` | Prochains départs (statique + RT) |
| GET | `/api/trips/{tripId}/stops?fromStopId={stopId}` | Arrêts restants d'un trajet depuis un arrêt donné |

Swagger UI disponible en dev : [http://localhost:5000/swagger](http://localhost:5000/swagger)

## Notes techniques

- **GTFS statique** : téléchargé et mis en cache localement 12h (`%TEMP%/gtfsdemo_static.zip`). Les index sont construits en mémoire au démarrage.
- **Résolution CKAN** : si l'URL configurée pointe vers une page de dataset, l'API CKAN est interrogée pour sélectionner la ressource ZIP la plus récente.
- **GTFS-RT** : rafraîchi toutes les 30 s via `BackgroundService`. Fournit uniquement les données de retard (trip delays). Nécessite une clé API valide.
- **Temps GTFS > 24h** : les services passant minuit (ex. `25:30:00`) sont correctement normalisés.
- **Terminus** : les départs dont l'arrêt sélectionné est le terminus sont automatiquement exclus.
- **CORS** : configuré pour le dev server Vite (`localhost:5173`).
